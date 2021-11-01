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
    public class InputDevice: BaseNode<AudioInputBlock>
    {
        private IDevice _device;
        private CircularBuffer _mainBuffer;
        private CircularBuffer _sharedBuffer;
        private AudioFormat _format;
        private PlayPauseStop _playPauseStop;
        private AudioInputBlock _inputBlock;
        public WaveFormat WaveFormat => _format.WaveFormat;
        public override AudioInputBlock AudioBlock => _inputBlock;

        public InputDevice()
        {
            _playPauseStop = new PlayPauseStop();
            Logger.Information("Input device created");
            _format = new AudioFormat(48000, 256, 2, true);
            try
            {
                var name = $"Buffer {Graph.GetBufferId()}";
                _mainBuffer = new CircularBuffer(name, 1024, 4*_format.BufferSize);
                _sharedBuffer = new CircularBuffer(name);
                Logger.Information("Buffer [{name}] created, buffer size", name);
            }
            catch (Exception e)
            {
                Logger.Error("{e}", e);
            }
            _inputBlock = new AudioInputBlock(_sharedBuffer, AudioService.Instance.Flow, _format, _playPauseStop);
            _inputBlock.PhaseChanged = (old, newPhase) =>
            {
                if (newPhase == LifecyclePhase.Playing)
                {
                    Play();
                } else 
                {
                    Stop();
                }
            };
            Output.SourceBlock = _inputBlock;
        }

        private bool _needsRestart;

        public void Update()
        {
            if (_needsRestart && Phase==LifecyclePhase.Playing)
            {
                _needsRestart = false;
                _device.InitRecording(200, _mainBuffer, WaveFormat, _playPauseStop);
                _playPauseStop.Play();
                _device?.Record();
            }
        }
        public void ChangeDevice(WaveInputDevice device)
        {
            try
            {
                if (_device != null)
                {
                    Stop();
                    _device.Dispose();
                }
                _device = (IDevice)device.Tag;
                // _device.InitRecording(200, _mainBuffer, WaveFormat, _playPauseStop);
                Logger.Information("Device changed: {device}", _device);
                if (Phase == LifecyclePhase.Playing)
                {
                    // Play();
                }

                _needsRestart = true;
            }
            catch (Exception e)
            {
                Logger.Error("InputDevice.Init({device}): {e}", _device, e);

            }
        }
        public void Play()
        {
            try
            {
                // _playPauseStop.Play();
                // _device?.Record();
                _needsRestart = true;
            }
            catch (Exception e)
            {
                Logger.Error("OutputDevice.Init({device}): {e}", _device, e);
            }

        }

        public void Stop()
        {
            try
            {
                _playPauseStop.Stop();
                _device?.Stop();
            }
            catch (Exception e)
            {
                Logger.Error("OutputDevice.Init({device}): {e}", _device, e);
            }

        }

        public override void Dispose()
        {
            _playPauseStop.Stop();
            _device?.Stop();
            _device?.Dispose();
            base.Dispose();
        }
    }
}