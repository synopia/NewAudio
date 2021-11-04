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
    public class OutputDevice : AudioNode<IOutputDeviceConfig>
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

        public OutputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("Output device created");
            _format = new AudioFormat(48000, 512, 2);
            RegisterCallback<WaveOutputDevice>("Device", OnDeviceChange);

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

        protected override void OnConnect(AudioLink input)
        {
            _logger.Information("New connection to output device");
            AddLink(input.SourceBlock.LinkTo(_processor));
        }

        protected override void OnStart()
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

        public override string DebugInfo()
        {
            return $"OUTPUT=[{_processor?.Completion.Status}, {_device?.AudioDataProvider?.CancellationToken.IsCancellationRequested}]";
        }

        public AudioLink Update(AudioLink input, WaveOutputDevice device, SamplingFrequency samplingFrequency = SamplingFrequency.Hz44100,
            int channelOffset = 0, int channels = 2, int desiredLatency = 250)
        {
            Config.Input = input;
            Config.Device = device;
            Config.SamplingFrequency = samplingFrequency;
            Config.DesiredLatency = desiredLatency;
            Config.ChannelOffset = channelOffset;
            Config.Channels = channels;
            return Update();
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

        protected override void OnStop()
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

        public override void Dispose()
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

            base.Dispose();
        }
    }
}