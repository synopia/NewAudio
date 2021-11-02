using System;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;

namespace NewAudio.Nodes
{
    public class InputDevice : BaseNode
    {
        private readonly AudioInputBlock _audioInputBlock;
        private readonly ILogger _logger;
        private IDevice _device;
        private AudioFormat _format;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();
            _logger.Information("Input device created");
            _format = new AudioFormat(48000, 512, 2);

            try
            {
                _audioInputBlock = new AudioInputBlock(AudioService.Instance.Flow, _format);
                Output.SourceBlock = _audioInputBlock;
                Output.Format = _format;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
        }

        public WaveFormat WaveFormat => _format.WaveFormat;
        protected override bool IsInputValid(AudioLink link)
        {
            return true;
        }

        public string DebugInfo()
        {
            return Utils.CalculateBufferStats(_audioInputBlock.Buffer);
        }

        protected override void Start()
        {
            try
            {
                _device.Record();
            }
            catch (Exception e)
            {
                _logger.Error("Start: {e}", e);
            }
        }

        public void ChangeDevice(WaveInputDevice device)
        {
            try
            {
                _device?.Dispose();

                _device = (IDevice)device.Tag;
                _device.InitRecording(200, _audioInputBlock.Buffer, WaveFormat);
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
                _audioInputBlock.Dispose();
                _device?.Dispose();
            }
            catch (Exception e)
            {
                _logger.Error("Dispose {e}", e);
            }

            base.Dispose();
        }
    }
}