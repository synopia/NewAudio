using System;
using System.IO.Packaging;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using SharedMemory;
using Stride.Core;
using VL.NewAudio.Core;

namespace NewAudio.Nodes
{
    public class OutputDevice : BaseNode<AudioOutputBlock>
    {
        private IDevice _device;
        private CircularBuffer _mainBuffer;
        private CircularBuffer _sharedBuffer;
        private AudioFormat _format;
        public WaveFormat WaveFormat => _format.WaveFormat;
        public override AudioOutputBlock AudioBlock => _outputBlock;

        private PlayPauseStop _playPauseStop;
        private AudioOutputBlock _outputBlock;

        public OutputDevice()
        {
            _playPauseStop = new PlayPauseStop();
            Logger.Information("Output device created");
            
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

            _outputBlock = new AudioOutputBlock(_sharedBuffer, AudioService.Instance.Flow, _format, _playPauseStop);
            Output.SourceBlock = _outputBlock;
            _outputBlock.PhaseChanged = (old, newPhase) =>
            {
                if (newPhase == LifecyclePhase.Playing)
                {
                    Play();
                } else 
                {
                    Stop();
                }
            };
            Connect += link =>
            {
                Logger.Information("New connection to output device");
                AddLink(link.SourceBlock.LinkTo(_outputBlock));
            };
            Reconnect += (old, link) =>
            {
                DisposeLinks();
                AddLink(link.SourceBlock.LinkTo(_outputBlock));
                Logger.Information("New connection to output device (removing old one)");

            };
            Disconnect += link =>
            {
                DisposeLinks();
                Logger.Information("Disconnected from output device");
            };
        }

        private bool _needsRestart;
        
        public void Update()
        {
            if (_needsRestart && Phase==LifecyclePhase.Playing)
            {
                _needsRestart = false;
                _device.InitPlayback(250, _mainBuffer, WaveFormat, _playPauseStop);
                _playPauseStop.Play();
                _device.Play();
            }
        }
        
        public void ChangeDevice(WaveOutputDevice device, int desiredLatency)
        {
            Logger.Information("OUTPUT Phase={phase}", Phase);
            try
            {
                if (_device != null)
                {
                    Stop();
                    _device.Dispose();
                }
                _device = (IDevice)device.Tag;
                _needsRestart = true;
                // _device.InitPlayback(desiredLatency, _sharedBuffer, WaveFormat, _playPauseStop);
                Logger.Information("Device changed: {device}", _device);
                if (Phase == LifecyclePhase.Playing)
                {
                    // Play();
                } 
            }
            catch (Exception e)
            {
                Logger.Error("OutputDevice.Init({device}): {e}", _device, e);
            }
        }

        public void Play()
        {
            try
            {
                _needsRestart = true;
                // _playPauseStop.Play();
                // _device?.Play();
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
            try
            {
                _mainBuffer.Dispose();
                _sharedBuffer.Dispose();
            }
            catch (Exception e)
            {
                Logger.Error("{e}",e);
            }
            base.Dispose();
        }
    }
}