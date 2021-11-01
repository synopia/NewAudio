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
        private readonly ILogger _logger;
        private IDevice _device;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;
        private readonly AudioOutputBlock _audioOutputBlock;

        public OutputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<OutputDevice>();
            _logger.Information("Output device created");
            _format = new AudioFormat(48000, 256, 2, true);

            try
            {
                _audioOutputBlock = new AudioOutputBlock(AudioService.Instance.Flow, _format);
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }

            Connect += link =>
            {
                _logger.Information("New connection to output device");
                AddLink(link.SourceBlock.LinkTo(_audioOutputBlock));
            };
            Reconnect += (old, link) =>
            {
                DisposeLinks();
                AddLink(link.SourceBlock.LinkTo(_audioOutputBlock));
                _logger.Information("New connection to output device (removing old one)");
            };
            Disconnect += link =>
            {
                DisposeLinks();
                _logger.Information("Disconnected from output device");
            };
        }

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
            return Utils.CalculateBufferStats(_audioOutputBlock.Buffer);
        }

        public void ChangeDevice(WaveOutputDevice device, int desiredLatency)
        {
            try
            {
                _device?.Dispose();

                _device = (IDevice)device.Tag;
                _device.InitPlayback(desiredLatency, _audioOutputBlock.Buffer, WaveFormat);
                _logger.Information("Device changed: {device}", _device);
                
                if (Phase == LifecyclePhase.Playing)
                {
                    Start();
                }
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