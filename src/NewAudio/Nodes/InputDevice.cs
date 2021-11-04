using System;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public interface IInputDeviceConfig : IAudioNodeConfig
    {
        public WaveInputDevice Device { get; set; }
        public SamplingFrequency SamplingFrequency { get; set; }
        public int DesiredLatency { get; set; }
        public int ChannelOffset { get; set; }
        public int Channels { get; set; }
    }

    public class InputDevice : AudioNode<IInputDeviceConfig>
    {
        private readonly AudioInputBlock _audioInputBlock;
        private readonly ILogger _logger;

        private IDevice _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();

            _logger.Information("Input device created");
            
            _format = new AudioFormat(48000, 512, 2);

            RegisterCallback<WaveInputDevice>("Device", OnDeviceChange);
            try
            {
                _audioInputBlock = new AudioInputBlock(_format);
                Output.SourceBlock = _audioInputBlock;
                Output.Format = _format;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }


        public override string DebugInfo()
        {
            return $"INPUT=[{Output?.SourceBlock?.Completion.Status}, {_device?.AudioDataProvider?.CancellationToken.IsCancellationRequested}]";
        }

        protected override void OnStart()
        {
            try
            {
                _audioInputBlock.Play();
                _device.Record();
            }
            catch (Exception e)
            {
                _logger.Error("Start: {e}", e);
            }
        }

        public AudioLink Update(WaveInputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Device = device;
            Config.SamplingFrequency = samplingFrequency;
            Config.DesiredLatency = desiredLatency;
            Config.ChannelOffset = channelOffset;
            Config.Channels = channels;

            return Update();
        }

        private void OnDeviceChange(WaveInputDevice old, WaveInputDevice current)
        {
            try
            {
                if (Config.Phase == LifecyclePhase.Playing)
                {
                    _device.Stop();
                    _audioInputBlock.Stop();
                }

                _device = (IDevice)Config.Device.Tag;
                // _format = WaveFormat.CreateIeeeFloatWaveFormat(_paramSamplingFreq.Value, );
                _device.InitRecording(Config.DesiredLatency, _audioInputBlock.Buffer, WaveFormat);
                _logger.Information("Device changed: {device}", _device);

                
                if (Config.Phase == LifecyclePhase.Playing)
                {
                    _audioInputBlock.Play();
                    _device.Play();
                }
            }
            catch (Exception e)
            {
                _logger.Error("ChangeDevice({device}): {e}", _device, e);
            }
        }

        protected override void OnStop()
        {
            try
            {
                _audioInputBlock?.Stop();
                _device?.Stop();
            }
            catch (Exception e)
            {
                _logger.Error("Stop({device}): {e}", _device, e);
            }
        }

        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _audioInputBlock?.Dispose();
                    _device?.Dispose();                    
                }

                _disposedValue = disposing;
            }
            base.Dispose(disposing);
        }
    }
}