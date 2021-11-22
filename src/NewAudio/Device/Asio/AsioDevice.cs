using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices.Asio
{
    public class AsioDevice : BaseDevice, IWaveProvider
    {
        public override string Name => "ASIO";
        private readonly string _driverName;
        private AsioOut _asioOut;
        private readonly List<OutputDeviceBlock> _outputs = new();
        private readonly List<InputDeviceBlock> _inputs = new();
        private bool _disposedValue;
        private int _firstInputChannel;
        private int _lastInputChannel;
        private int _firstOutputChannel;
        private int _lastOutputChannel;
        public override bool IsInitialized { get; protected set; }
        public override bool IsProcessing { get; protected  set; }

        public WaveFormat WaveFormat { get; private set; }

        private IConverter _converter;

        public AsioDevice(DeviceManager dm, string driverName): base(dm)
        {
            _driverName = driverName;
            InitLogger<AsioDevice>();
            
            DeviceParams.SampleRate.Value = 48000;
        }

        protected override void UpdateFormat()
        {
            if (DeviceParams.SampleRate.HasChanged)
            {
                if (!_asioOut.IsSampleRateSupported(DeviceParams.SampleRate.Value))
                {
                    DeviceParams.SampleRate.Rollback();
                }
            }

            if (DeviceParams.FramesPerBlock.HasChanged)
            {
                // not implemented
                DeviceParams.FramesPerBlock.Rollback();
            }
            DeviceParams.Commit();

            var isProcessing = IsProcessing;
            DisableProcessing();


            if (isProcessing)
            {
                EnableProcessing();
            }
        }

        public override void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            _asioOut = new AsioOut(_driverName);
            MaxNumberOfInputChannels = _asioOut.DriverInputChannelCount;
            MaxNumberOfOutputChannels = _asioOut.DriverOutputChannelCount;

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(DeviceParams.SampleRate.Value, 2);
            _asioOut.Init(this);
            DeviceParams.FramesPerBlock.Value = _asioOut.FramesPerBuffer;
            _asioOut.Dispose();
            _asioOut = new AsioOut(_driverName);
            
            IsInitialized = true;
        }

        public override void Uninitialize()
        {
            if (!IsInitialized)
            {
                return;
            }
            _asioOut.Stop();
            _asioOut.Dispose();
            _asioOut = null;

            IsInitialized = false;
        }

        public override void EnableProcessing()
        {
            if (IsProcessing)
            {
                return;
            }
            _firstInputChannel = _inputs.Count>0 ? _inputs.Min(d => d.InputChannelOffset) : 0;
            _firstOutputChannel = _outputs.Count>0 ? _outputs.Min(d => d.ChannelOffset) : 0;
            
            _lastInputChannel = _inputs.Count>0 ? _inputs.Max(d => d.InputChannelOffset+d.NumberOfChannels) : 0;
            _lastOutputChannel = _outputs.Count>0 ? _outputs.Max(d => d.ChannelOffset+d.NumberOfChannels) : 0;
            var inputChannelsUsed = _lastInputChannel - _firstInputChannel;
            var outputChannelsUsed = _lastOutputChannel - _firstOutputChannel;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(DeviceParams.SampleRate.Value, outputChannelsUsed);

            inputChannelsUsed = Math.Max(outputChannelsUsed, inputChannelsUsed);
            _asioOut.InitRecordAndPlayback(this, inputChannelsUsed, 0);
            _asioOut.AudioAvailable += OnAudioAvailable;
            _asioOut.DriverResetRequest += OnDriverResetRequest;
            _asioOut.ChannelOffset = _firstOutputChannel;
            _asioOut.InputChannelOffset = _firstInputChannel;
            NumberOfInputChannels = inputChannelsUsed;
            NumberOfOutputChannels = outputChannelsUsed;
            _asioOut.Play();

            Logger.Information("Started ASIO {Driver}, {SampleRate}, {Frames}, in: {In}, out: {Out}", _driverName, WaveFormat.SampleRate, DeviceParams.FramesPerBlock.Value, _asioOut.NumberOfInputChannels, _asioOut.NumberOfOutputChannels);

            IsProcessing = true;
        }

        public override void DisableProcessing()
        {
            if (!IsProcessing)
            {
                return;
            }
            _asioOut.DriverResetRequest -= OnDriverResetRequest;
            _asioOut.AudioAvailable -= OnAudioAvailable;
            _asioOut.Stop();


            IsProcessing = false;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        void OnDriverResetRequest(object sender, EventArgs args)
        {
            Logger.Warning("Driver Reset");
            DeviceParamsWillChange?.Invoke();
            
            DisableProcessing();
            Uninitialize();
            Initialize();
            EnableProcessing();
            
            DeviceParamsDidChange?.Invoke();
        }
        void OnAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            DeviceParams.FramesPerBlock.Value = e.SamplesPerBuffer;
            _converter ??= e.AsioSampleType switch
            {
                AsioSampleType.Float32LSB => new Converter<Float32Sample>(),
                AsioSampleType.Int16LSB => new Converter<Int16LsbSample>(),
                AsioSampleType.Int24LSB => new Converter<Int24LsbSample>(),
                AsioSampleType.Int32LSB => new Converter<Int32LsbSample>(),
                _ => throw new NotImplementedException()
            };
            
            try
            {
                foreach (var device in _outputs)
                {
                    var buffer = device.RenderInputs();
                    Trace.Assert(buffer.NumberOfFrames==FramesPerBlock, $"Buffer: {buffer.NumberOfFrames}, Driver: {FramesPerBlock}");
                    Trace.Assert(buffer.NumberOfChannels==device.NumberOfChannels);
                    _converter.ConvertTo(buffer.Data, e.OutputBuffers, buffer.NumberOfFrames, device.ChannelOffset, buffer.NumberOfChannels);
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                Logger.Error(exception, "Error in ASIO Thread");
            }

            e.WrittenToOutputBuffers = true;
        }
        
        public override OutputDeviceBlock CreateOutput(IResourceHandle<IDevice> handle, DeviceBlockFormat format)
        {
            var output = new AsioOutputDevice(handle, format);
            _outputs.Add(output);
            return output;
        }

        public override InputDeviceBlock CreateInput(IResourceHandle<IDevice> handle, DeviceBlockFormat format)
        {
            var input = new AsioInputDevice(handle, format);
            _inputs.Add(input);
            return input;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    DisableProcessing();
                    Uninitialize();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}