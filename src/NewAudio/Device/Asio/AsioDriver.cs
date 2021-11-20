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

namespace NewAudio.Devices
{
    public class AsioDriver : BaseDriver, IWaveProvider
    {
        public override string Name => "ASIO";
        private string _driverName;
        private AsioOut _asioOut;
        private List<AsioDevice> _devices = new();
        private bool _disposedValue;
        private int _firstInputChannel;
        private int _lastInputChannel;
        private int _firstOutputChannel;
        private int _lastOutputChannel;
        public override bool IsInitialized { get; protected set; }
        public override bool IsProcessing { get; protected  set; }

        public WaveFormat WaveFormat { get; private set; }

        private IConverter _converter;

        public AsioDriver(DriverManager dm, string driverName): base(dm)
        {
            _driverName = driverName;
            InitLogger<AsioDriver>();
            
            DriverParams.SampleRate.Value = 48000;
        }

        protected override void UpdateFormat()
        {
            if (DriverParams.SampleRate.HasChanged)
            {
                if (!_asioOut.IsSampleRateSupported(DriverParams.SampleRate.Value))
                {
                    DriverParams.SampleRate.Rollback();
                }
            }

            if (DriverParams.FramesPerBlock.HasChanged)
            {
                // not implemented
                DriverParams.FramesPerBlock.Rollback();
            }
            DriverParams.Commit();

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

            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(DriverParams.SampleRate.Value, 2);
            _asioOut.Init(this);
            DriverParams.FramesPerBlock.Value = _asioOut.FramesPerBuffer;
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
            _firstInputChannel = _devices.Min(d => d.InputChannelOffset);
            _firstOutputChannel = _devices.Min(d => d.OutputChannelOffset);
            
            _lastInputChannel = _devices.Max(d => d.InputChannelOffset+d.NumberOfOutputChannels);
            _lastOutputChannel = _devices.Max(d => d.OutputChannelOffset+d.NumberOfOutputChannels);
            var inputChannelsUsed = _lastInputChannel - _firstInputChannel;
            var outputChannelsUsed = _lastOutputChannel - _firstOutputChannel;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(DriverParams.SampleRate.Value, outputChannelsUsed);

            _asioOut.InitRecordAndPlayback(this, inputChannelsUsed, 0);
            _asioOut.AudioAvailable += OnAudioAvailable;
            _asioOut.DriverResetRequest += OnDriverResetRequest;
            _asioOut.ChannelOffset = _firstOutputChannel;
            _asioOut.InputChannelOffset = _firstInputChannel;
            _asioOut.Play();

            Logger.Information("Started ASIO {Driver}, {SampleRate}, {Frames}, in: {In}, out: {Out}", _driverName, WaveFormat.SampleRate, DriverParams.FramesPerBlock.Value, _asioOut.NumberOfInputChannels, _asioOut.NumberOfOutputChannels);

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
            DriverParams.FramesPerBlock.Value = e.SamplesPerBuffer;
            _converter ??= e.AsioSampleType switch
            {
                AsioSampleType.Float32LSB => new Converter<Float32Sample>(),
                AsioSampleType.Int16LSB => new Converter<Int16LsbSample>(),
                AsioSampleType.Int24LSB => new Converter<Int24LsbSample>(),
                AsioSampleType.Int32LSB => new Converter<Int32LsbSample>(),
                _ => throw new NotImplementedException()
            };
            
            // try
            // {
                foreach (var device in _devices)
                {
                    var buffer = device.Output.RenderInputs();
                    Trace.Assert(buffer.NumberOfFrames==FramesPerBlock, $"Buffer: {buffer.NumberOfFrames}, Driver: {FramesPerBlock}");
                    Trace.Assert(buffer.NumberOfChannels==device.NumberOfOutputChannels);
                    _converter.ConvertTo(buffer.Data, e.OutputBuffers, buffer.NumberOfFrames, device.OutputChannelOffset, buffer.NumberOfChannels);
                }
            // }
            // catch (Exception exception)
            // {
                // Console.WriteLine(exception);
                // Logger.Error(exception, "Error in ASIO Thread");
            // }

            e.WrittenToOutputBuffers = true;
        }
        
        protected override IDevice CreateDevice(string name, AudioBlockFormat format)
        {
            var device = new AsioDevice(this, name, format);
            _devices.Add(device);
            return device;
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