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

    public class InputDevice : IAudioNode<IInputDeviceConfig>
    {
        private readonly AudioInputBlock _audioInputBlock;
        private readonly ILogger _logger;

        private IDevice _device;
        private AudioFormat _format;
        private AudioNodeSupport<IInputDeviceConfig> _support;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();

            _logger.Information("Input device created");
            _support = new AudioNodeSupport<IInputDeviceConfig>(this);
            
            _format = new AudioFormat(48000, 512, 2);

            _support.RegisterCallback<WaveInputDevice>("Device", OnDeviceChange);
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

        public AudioParams AudioParams => _support.AudioParams;
        public IInputDeviceConfig Config => _support.Config;
        public IInputDeviceConfig LastConfig => _support.LastConfig;
        public AudioLink Output => _support.Output;
        public WaveFormat WaveFormat => _format.WaveFormat;

        // protected override bool IsInputValid(AudioLink link)
        // {
        // return true;
        // }

        public string DebugInfo()
        {
            return Utils.CalculateBufferStats(_audioInputBlock.Buffer);
        }

        public void OnStart()
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

        public bool IsInputValid(IInputDeviceConfig next)
        {
            return true;
        }

        public void OnAnyChange()
        {
        }

        public void OnConnect(AudioLink input)
        {
        }

        public void OnDisconnect(AudioLink link)
        {
        }

        public void Update(WaveInputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Device = device;
            Config.SamplingFrequency = samplingFrequency;
            Config.DesiredLatency = desiredLatency;
            Config.ChannelOffset = channelOffset;
            Config.Channels = channels;

            _support.Update();
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

        public void OnStop()
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

        public void Dispose()
        {
            try
            {
                _audioInputBlock.Stop();
                _device.Stop();
                
                _audioInputBlock.Dispose();
                _device?.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose {e}", e);
            }

            _support.Dispose();
        }
    }
}