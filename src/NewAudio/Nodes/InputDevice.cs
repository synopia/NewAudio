using System;
using System.Threading;
using NewAudio.Core;
using Serilog;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Devices;
using SharedMemory;
using VL.NewAudio.Core;

namespace NewAudio.Nodes
{
    public class InputDevice: BaseNode
    {
        private readonly ILogger _logger;
        private IDevice _device;
        private AudioFormat _format;
        private readonly AudioInputBlock _audioInputBlock;
        
        public WaveFormat WaveFormat => _format.WaveFormat;

        public InputDevice()
        {
            _logger = AudioService.Instance.Logger.ForContext<InputDevice>();
            _logger.Information("Input device created");
            _format = new AudioFormat(48000, 256, 2, true);

            try
            {
                _audioInputBlock = new AudioInputBlock(AudioService.Instance.Flow, _format);
                Output.SourceBlock = _audioInputBlock;
            }
            catch (Exception e)
            {
                _logger.Error("Ctor: {e}", e);
            }
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
                _logger.Error("Start: {e}",e);
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