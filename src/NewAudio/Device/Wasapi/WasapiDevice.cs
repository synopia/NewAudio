using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NewAudio.Block;
using NewAudio.Dsp;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices.Wasapi
{
    public class WasapiDevice : BaseDevice, IWaveProvider
    {
        private string _name;
        public override string Name => _name;
        private readonly string _wasapiId;
        public string Id => _wasapiId;
        private readonly List<OutputDeviceBlock> _outputs = new();
        private readonly List<InputDeviceBlock> _inputs = new();
 
        public override bool IsInitialized { get; protected set; }
        public override bool IsProcessing { get; protected  set; }
        public WaveFormat WaveFormat { get; private set; }
        private IConverter _converter;
        private WasapiOut _wasapiOut;
        private MMDevice _device;

        public WasapiDevice(DeviceManager deviceManager, string wasapiId, string name) : base(deviceManager)
        {
            _wasapiId = wasapiId;
            _name = name;
            InitLogger<WasapiDevice>();
            DeviceParams.SampleRate.Value = 48000;

        }

        public int Read(byte[] buffer, int offset, int count)
        {
            Logger.Information("Read {offset} {count}", offset, count);
            return count - offset;
        }

        public override OutputDeviceBlock CreateOutput(IResourceHandle<IDevice> device, DeviceBlockFormat format)
        {
            var output = new WasapiOutputDevice(device, format);
            _outputs.Add(output);
            return output;
        }

        public override InputDeviceBlock CreateInput(IResourceHandle<IDevice> device, DeviceBlockFormat format)
        {
            throw new System.NotImplementedException();
        }

        protected override void UpdateFormat()
        {
            throw new System.NotImplementedException();
        }

        public override void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            _device = new MMDeviceEnumerator().GetDevice(_wasapiId);
            _wasapiOut = new WasapiOut(_device, AudioClientShareMode.Shared, true, 0);
            WaveFormat = _wasapiOut.OutputWaveFormat;
            _wasapiOut.Init(this);
            DeviceParams.FramesPerBlock.Value = 512;
            MaxNumberOfOutputChannels = WaveFormat.Channels;
            MaxNumberOfInputChannels = 0;
        }

        public override void Uninitialize()
        {
            if (!IsInitialized)
            {
                return;
            }
            _wasapiOut.Stop();
            _wasapiOut.Dispose();
            _wasapiOut = null;

            IsInitialized = false;
        }

        public override void EnableProcessing()
        {
            if (IsProcessing)
            {
                return;
            }
            NumberOfInputChannels = 0;
            NumberOfOutputChannels = 2;
            Logger.Information("{Buffer}", _device.AudioClient.BufferSize);
            _wasapiOut.Play();
            IsProcessing = true;
            Logger.Information("Started Wasapi {Driver}, {SampleRate}, {Frames}, in: {In}, out: {Out}", Name, WaveFormat.SampleRate, DeviceParams.FramesPerBlock.Value, NumberOfInputChannels, NumberOfOutputChannels);

        }

        public override void DisableProcessing()
        {
            if (!IsProcessing)
            {
                return;
            }
            _wasapiOut.Stop();
        }
    }
}