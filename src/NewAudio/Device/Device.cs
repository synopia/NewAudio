using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.SqlServer.Server;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using Xt;

namespace NewAudio.Devices
{
    public struct DeviceFormat
    {
        public int SampleRate;
        public XtSample SampleType;

        public float BufferSizeMs;

        public DeviceFormat WithSampleRate(int sr)
        {
            SampleRate = sr;
            return this;
        }

        public DeviceFormat WithBufferSize(float ms)
        {
            BufferSizeMs = ms;
            return this;
        }
    }

    public class Device : IDisposable
    {
        private static readonly IEnumerable<XtSample> FormatList = new[]
        {
            XtSample.Float32,
            XtSample.Int32,
            XtSample.Int16,
            XtSample.Int24,
        };

        private readonly IAudioService _audioService = Resources.GetAudioService();
        private readonly ILogger _logger = Resources.GetLogger<Device>();

        private bool _disposedValue;

        private List<OutputDeviceBlock> _outputDeviceBlocks = new();
        private List<InputDeviceBlock> _inputDeviceBlocks = new();

        public Action DeviceFormatWillChange { get; set; }
        public Action DeviceFormatDidChange { get; set; }

        public int MaxNumberOfInputChannels => XtDevice.GetChannelCount(false);
        public int MaxNumberOfOutputChannels => XtDevice.GetChannelCount(true);

        public int NumberOfInputChannels { get; protected set; }
        public int NumberOfOutputChannels { get; protected set; }
        public bool SupportInterleaved => XtDevice.SupportsAccess(true);
        public bool SupportNonInterleaved => XtDevice.SupportsAccess(false);
        public int SampleRate => FormatInUse.mix.rate;
        public int FramesPerBlock { get; private set; }
        public string Name { get; }
        public bool IsInitialized { get; protected set; }
        public bool IsProcessing { get; protected set; }
        private IResourceHandle<IXtDevice> _xtDevice;
        private IXtDevice XtDevice => _xtDevice.Resource;

        private IConvertWriter _converter;
        private IXtStream _stream;
        public XtFormat FormatInUse { get; private set; }
        private bool _formatValid;
        private XtDeviceStreamParams _deviceParams;
        private ulong _error;
        private int _internalBufferPos;
        private AudioBuffer _internalOutputBuffer;
        private XtBufferSize _bufferSize;
        private float _chosenBufferSize;
        private bool _wasEnabledBeforeFormatChange;

        public bool IsDisposed => _disposedValue;

        public Device(string deviceName, IResourceHandle<IXtDevice> xtDevice)
        {
            Name = deviceName;
            _xtDevice = xtDevice;
        }

        public void UpdateFormat(DeviceFormat deviceFormat)
        {
            _wasEnabledBeforeFormatChange = IsProcessing;
            
            DeviceFormatWillChange?.Invoke();
            DisableProcessing();
            Uninitialize();

            NumberOfOutputChannels =
                _outputDeviceBlocks.Count > 0 ? _outputDeviceBlocks.Max(b => b.NumberOfChannels) : 0;
            NumberOfInputChannels = _inputDeviceBlocks.Count > 0 ? _inputDeviceBlocks.Max(b => b.NumberOfChannels) : 0;

            var testFormat = FormatList.Select(sample =>
            {
                var mix = new XtMix(deviceFormat.SampleRate, sample);
                return new XtFormat(mix, new XtChannels(MaxNumberOfInputChannels, 0, 2, 0));
            }).FirstOrDefault(format =>
            {
                _logger.Information("Testing SampleRate={SampleRate}, SampleType={SampleType}", format.mix.rate,
                    format.mix.sample);
                return XtDevice.SupportsFormat(format);
            });

            if (testFormat.mix.rate == 0)
            {
                _logger.Warning("No format found!");
                _formatValid = false;
                FramesPerBlock = 0;
                DeviceFormatDidChange?.Invoke();
                return;
            }


            if (!SupportNonInterleaved)
            {
                _converter = testFormat.mix.sample switch
                {
                    XtSample.Float32 => new ConvertWriter<Float32Sample, Interleaved>(),
                    XtSample.Int16 => new ConvertWriter<Int16LsbSample, Interleaved>(),
                    XtSample.Int24 => new ConvertWriter<Int24LsbSample, Interleaved>(),
                    XtSample.Int32 => new ConvertWriter<Int32LsbSample, Interleaved>(),
                    _ => throw new NotImplementedException()
                };
            }
            else
            {
                _converter = testFormat.mix.sample switch
                {
                    XtSample.Float32 => new ConvertWriter<Float32Sample, NonInterleaved>(),
                    XtSample.Int16 => new ConvertWriter<Int16LsbSample, NonInterleaved>(),
                    XtSample.Int24 => new ConvertWriter<Int24LsbSample, NonInterleaved>(),
                    XtSample.Int32 => new ConvertWriter<Int32LsbSample, NonInterleaved>(),
                    _ => throw new NotImplementedException()
                };
            }

            _bufferSize = XtDevice.GetBufferSize(testFormat);
            _chosenBufferSize =
                AudioMath.Clamp(deviceFormat.BufferSizeMs, (float)_bufferSize.min, (float)_bufferSize.max);
            FormatInUse = new XtFormat(testFormat.mix,
                new XtChannels(NumberOfInputChannels, 0, NumberOfOutputChannels, 0));
            var streamParams = new XtStreamParams(!SupportNonInterleaved, OnBuffer, OnRun, OnRunning);
            _deviceParams = new XtDeviceStreamParams(streamParams, FormatInUse, _chosenBufferSize);
            var testStream = XtDevice.OpenStream(_deviceParams, null);
            FramesPerBlock = testStream.GetFrames();
            testStream.Dispose();
            _formatValid = true;

            _logger.Information(
                "Format: SampleRate={SampleRate}, SampleType={SampleType}, InCh={InCh}, OutCh={OutCh}, Frames={Frames}, Interleaved={Interleaved}",
                FormatInUse.mix.rate, FormatInUse.mix.sample, FormatInUse.channels.inputs, FormatInUse.channels.outputs,
                FramesPerBlock, streamParams.interleaved);
            DeviceFormatDidChange?.Invoke();

            Initialize();
            if (_wasEnabledBeforeFormatChange)
            {
                EnableProcessing();
            }
        }

        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }

            if (!_formatValid)
            {
               return;
            }

            _stream = XtDevice.OpenStream(_deviceParams, null);
            IsInitialized = true;
        }

        public void AttachOutput(OutputDeviceBlock block)
        {
            if (!_outputDeviceBlocks.Contains(block))
            {
                _outputDeviceBlocks.Add(block);
            }

            _formatValid = false;
        }

        public void DetachOutput(OutputDeviceBlock block)
        {
            _outputDeviceBlocks.Remove(block);
            _formatValid = false;
        }

        public void AttachInput(InputDeviceBlock block)
        {
            if (!_inputDeviceBlocks.Contains(block))
            {
                _inputDeviceBlocks.Add(block);
            }

            _formatValid = false;
        }

        private int OnBuffer(XtStream stream, in XtBuffer targetBuffer, object user)
        {
            if (targetBuffer.output == IntPtr.Zero || _outputDeviceBlocks.Count != 1)
            {
                return 0;
            }

            Trace.Assert(this == _outputDeviceBlocks[0].Device);

            int remainingFrames = targetBuffer.frames;
            while (remainingFrames > 0)
            {
                if (_internalOutputBuffer == null)
                {
                    _internalOutputBuffer = _outputDeviceBlocks[0].RenderInputs();
                    _internalBufferPos = 0;
                }

                if (_internalBufferPos == 0)
                {
                    _converter.Write(_internalOutputBuffer, _internalBufferPos, targetBuffer, 0, remainingFrames);
                    _internalBufferPos += remainingFrames;
                    remainingFrames = 0;
                }

                if (_internalBufferPos >= FramesPerBlock)
                {
                    _internalOutputBuffer = null;
                }
            }

            return 0;
        }

        private void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            if (error != 0)
            {
                IsProcessing = false;
                _error = error;
                _logger.Error("XtAudio Error: {Error}", XtAudio.GetErrorInfo(error));
            }

            _logger.Information("OnRunning {Running}", running);
            if (!running)
            {
                IsProcessing = false;
            }
        }

        private void OnRun(XtStream stream, int index, object user)
        {
            _logger.Information("OnRun {Index}", index);
        }

        public void EnableProcessing()
        {
            if (IsProcessing)
            {
                return;
            }

            if (!IsInitialized)
            {
                return;
            }

            _stream.Start();

            IsProcessing = true;
        }

        public void DisableProcessing()
        {
            if (!IsProcessing)
            {
                return;
            }

            _stream.Stop();

            IsProcessing = false;
        }

        public void Uninitialize()
        {
            if (!IsInitialized)
            {
                return;
            }

            _stream.Stop();
            _stream.Dispose();
            _stream = null;

            IsInitialized = false;
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _logger.Information("Dispose called for device {This}", Name);
                _stream?.Stop();
                _stream?.Dispose();
                _xtDevice.Dispose();
                _disposedValue = true;
            }
        }
    }
}