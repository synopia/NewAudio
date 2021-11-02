using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public class OutputDevice : BaseNode
    {
        private readonly AudioOutputBlock _audioOutputBlock;
        private readonly ILogger _logger;
        private int _counter;
        private IDevice _device;
        private AudioFormat _format;
        private long _lag;
        private double _lagMs;

        public OutputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("Output device created");
            _format = new AudioFormat(48000, 512, 2);
            var buf = new TransformBlock<AudioDataMessage, AudioDataMessage>(msg =>
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
                _audioOutputBlock = new AudioOutputBlock(AudioService.Instance.Flow, _format);
                buf.LinkTo(_audioOutputBlock);
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }


            OnConnect += link =>
            {
                _logger.Information("New connection to output device");
                AddLink(link.SourceBlock.LinkTo(buf));
            };
            OnDisconnect += link =>
            {
                DisposeLinks();
                _logger.Information("Disconnected from output device");
            };
        }

        public WaveFormat WaveFormat => _format.WaveFormat;

        protected override void Start()
        {
            try
            {
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

        public void ChangeDevice(WaveOutputDevice device, int desiredLatency)
        {
            try
            {
                _device?.Dispose();

                _device = (IDevice)device.Tag;
                _device.InitPlayback(desiredLatency, _audioOutputBlock.Buffer, WaveFormat);
                _logger.Information("Device changed: {device}", _device);

                if (Phase == LifecyclePhase.Playing) Start();
            }
            catch (Exception e)
            {
                _logger.Error("ChangeDevice({device}): {e}", _device, e);
            }
        }

        protected override void Stop()
        {
            try
            {
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
                Stop();
                _audioOutputBlock.Dispose();
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