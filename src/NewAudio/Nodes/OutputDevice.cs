using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public interface IOutputDeviceConfig: IAudioNodeConfig
    {
        WaveOutputDevice Device { get; set; }
        public SamplingFrequency SamplingFrequency { get; set; }
        public int DesiredLatency { get; set; }
        public int ChannelOffset { get; set; }
        public int Channels { get; set; }

    }
    public class OutputDevice : IAudioNode<IOutputDeviceConfig>
    {
        private readonly AudioOutputBlock _audioOutputBlock;
        private readonly ILogger _logger;
        private int _counter;
        private IDevice _device;
        private AudioFormat _format;
        private long _lag;
        private double _lagMs;
        private TransformBlock<AudioDataMessage, AudioDataMessage> _processor;
        public WaveFormat WaveFormat => _format.WaveFormat;
        private AudioNodeSupport<IOutputDeviceConfig> _support;
        public AudioParams AudioParams => _support.AudioParams;
        public IOutputDeviceConfig Config => _support.Config;
        public IOutputDeviceConfig LastConfig => _support.LastConfig;
        public AudioLink Output => _support.Output;


        public OutputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("Output device created");
            _format = new AudioFormat(48000, 512, 2);
            _support = new AudioNodeSupport<IOutputDeviceConfig>(this);
            _support.RegisterCallback<WaveOutputDevice>("Device", OnDeviceChange);

            _processor = new TransformBlock<AudioDataMessage, AudioDataMessage>(msg =>
            {
                var now = DateTime.Now.Ticks;
                var span = now - msg.Time.RealTime;
                var l = TimeSpan.FromTicks(span).TotalMilliseconds;

                _lag += span;
                _counter++;
                if (_counter > 100)
                {
                    _lagMs = TimeSpan.FromTicks(_lag / 100).TotalMilliseconds;
                    _lag = 0;
                    _counter = 0;
                }

                return msg;
            });

            try
            {
                _audioOutputBlock = new AudioOutputBlock(_format);
                _processor.LinkTo(_audioOutputBlock);
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }

        }

        public bool IsInputValid(IOutputDeviceConfig next)
        {
            return true;
        }

        public void OnAnyChange()
        {
        }

        public void OnConnect(AudioLink input)
        {
            _logger.Information("New connection to output device");
            _support.AddLink(input.SourceBlock.LinkTo(_processor));
        }

        public void OnDisconnect(AudioLink link)
        {
            _support.DisposeLinks();
            _logger.Information("Disconnected from output device");
        }

        public void OnStart()
        {
            try
            {
                _audioOutputBlock.Play();
                _device.Play();
            }
            catch (Exception e)
            {
                _logger.Error("Start: {e}", e);
            }
        }

        public string DebugInfo()
        {
            return $"LAG {_lagMs} ";
        }

        // protected override bool IsInputValid(AudioLink link)
        // {
            // return true;
        // }

        public void Update(AudioLink input, WaveOutputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Input = input;
            Config.Device = device;
            Config.SamplingFrequency = samplingFrequency;
            Config.DesiredLatency = desiredLatency;
            Config.ChannelOffset = channelOffset;
            Config.Channels = channels;
            _support.Update();
        }
        

        private void OnDeviceChange(WaveOutputDevice old, WaveOutputDevice current)
        {
            try{
                if (Config.Phase == LifecyclePhase.Playing)
                {
                    _device.Stop();
                    _audioOutputBlock.Stop();
                }

                _device = (IDevice)Config.Device.Tag;
                _device.InitPlayback(Config.DesiredLatency, _audioOutputBlock.Buffer, WaveFormat);
                _logger.Information("Device changed: {device}", _device);

                if (Config.Phase == LifecyclePhase.Playing)
                {
                    _audioOutputBlock.Play();
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
                _audioOutputBlock.Stop();
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
                _audioOutputBlock?.Stop();
                _device?.Dispose();

                _audioOutputBlock?.Dispose();
                _device?.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose: {e}", e);
            }

            _support.Dispose();
        }
    }
}