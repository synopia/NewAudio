using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VL.NewAudio.Dsp;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Device
{
    public class AudioProcessorPlayer: IAudioDeviceCallback, IDisposable
    {
        private struct NumChannels
        {
            public NumChannels(int ins, int outs)
            {
                this.ins = ins;
                this.outs = outs;
            }

            public AudioBusState ToLayout()
            {
                return new AudioBusState(new[] { AudioChannels.Channels(ins) },
                    new[] { AudioChannels.Channels(outs) });
            }

            public int ins;
            public int outs;
        }

        private AudioProcessor? _processor;
        private readonly object _lock = new();
        private int _sampleRate;
        private int _framesPerBlock;
        private bool _isPrepared;
        private NumChannels _deviceChannels;
        private NumChannels _defaultProcessorChannels;
        private NumChannels _actualProcessorChannels;
        private readonly AudioBuffer _tempBuffer = new();
        
        private NumChannels FindMostSuitableLayout(AudioProcessor processor)
        {
            var layouts = new List<NumChannels> { _deviceChannels };
            
            if (_deviceChannels.ins is 0 or 1)
            {
                layouts.Add(new NumChannels(_defaultProcessorChannels.ins, _deviceChannels.outs));
                layouts.Add(new NumChannels(_deviceChannels.outs, _deviceChannels.outs));
            }

            return layouts.First(l => processor.IsBusStateSupported(l.ToLayout()));
        }

        private void ResizeChannels()
        {
            var maxChannels = new[]{
                _deviceChannels.ins, _deviceChannels.outs,
                _actualProcessorChannels.ins,_actualProcessorChannels.outs
            }.Max();
            
            _tempBuffer.SetSize(maxChannels, _framesPerBlock);
        }


        public AudioProcessor? Processor
        {
            get => _processor;
            set => SetProcessor(value);
        }
        public void SetProcessor(AudioProcessor? processor)
        {
            lock (_lock)
            {
                if (_processor == processor)
                {
                    return;
                }

                if (processor != null && _sampleRate > 0 && _framesPerBlock > 0)
                {
                    _defaultProcessorChannels = new NumChannels(processor.MainBusInputChannels, processor.MainBusOutputChannels);
                    _actualProcessorChannels = FindMostSuitableLayout(processor);
                    processor.SetPlayConfig(_actualProcessorChannels.ins, _actualProcessorChannels.outs,_sampleRate, _framesPerBlock);
                    processor.PrepareToPlay(_sampleRate, _framesPerBlock);
                }

                AudioProcessor? old = null;
                old = _isPrepared ? processor : null;
                _processor = processor;
                _isPrepared = true;
                ResizeChannels();
                
                if (old != null)
                {
                    old.ReleaseResources();
                }
            }
        }

        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            lock (_lock)
            {
                Trace.Assert(_sampleRate>0 && _framesPerBlock>0 );
                _tempBuffer.Merge(input, output, _actualProcessorChannels.ins, _actualProcessorChannels.outs);

                if (_processor != null)
                {
                    Trace.Assert(output.NumberOfChannels==_actualProcessorChannels.outs);
                    lock (_processor.ProcessLock)
                    {
                        if (!_processor.SuspendProcessing)
                        {
                            _processor.Process(_tempBuffer);
                            return;
                        }
                    }
                }

                output.Zero();
            }
        }

        public void AudioDeviceAboutToStart(IAudioSession session)
        {
            var newSampleRate = session.CurrentSampleRate;
            var newFramesPerBlock = session.CurrentFramesPerBlock;
            var numChannelIn = session.ActiveInputChannels.Count;
            var numChannelOut = session.ActiveOutputChannels.Count;

            lock (_lock)
            {
                _sampleRate = newSampleRate;
                _framesPerBlock = newFramesPerBlock;
                _deviceChannels = new NumChannels(numChannelIn, numChannelOut);
                ResizeChannels();
                
                if (_processor != null)
                {
                    if (_isPrepared)
                    {
                        _processor.ReleaseResources();
                    }

                    var old = _processor;
                    SetProcessor(null);
                    SetProcessor(old);
                }
            }
        }

        public void AudioDeviceStopped()
        {
            lock (_lock)
            {
                if (_processor != null && _isPrepared)
                {
                    _processor.ReleaseResources();
                }

                _sampleRate = 0;
                _framesPerBlock = 0;
                _isPrepared = false;
                _tempBuffer.SetSize(1,1);
            }
        }

        public void AudioDeviceError(string errorMessage)
        {
            
        }

        public void Dispose()
        {
            SetProcessor(null);
        }
    }
}