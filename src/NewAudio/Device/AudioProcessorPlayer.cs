using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Dsp;
using NewAudio.Processor;

namespace NewAudio.Device
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
        private struct ChannelInfo
        {
            public ChannelInfo(Memory<float>[] data, int numChannels)
            {
                _data = data;
                this.numChannels = numChannels;
            }

            public ChannelInfo(AudioBuffer buffer) : this(buffer.GetWriteChannels(), buffer.NumberOfChannels)
            {
            }

            public Memory<float>[] _data;
            public int numChannels;
        }

        private static void InitializeBuffers(ChannelInfo ins, ChannelInfo outs, int framesPerBlock, int processorIns,
            int processorOuts, AudioBuffer tempBuffer, Memory<float>[] channels)
        {
            Trace.Assert(channels.Length>=Math.Max(processorIns, processorOuts));
            int totalNumChannels = 0;
            Action<int> prepareInputChannel = (index) =>
            {
                if (ins.numChannels == 0)
                {
                    channels[totalNumChannels].Slice(0, framesPerBlock).Span.Clear();
                }
                else
                {
                    ins._data[index % ins.numChannels].Slice(0, framesPerBlock).Span
                        .CopyTo(channels[totalNumChannels].Span);
                }
            };
            if (processorIns > processorOuts)
            {
                Trace.Assert(tempBuffer.NumberOfChannels>=processorIns-processorOuts);
                Trace.Assert(tempBuffer.NumberOfFrames>=framesPerBlock);

                for (int i = 0; i < processorOuts; i++)
                {
                    channels[totalNumChannels] = outs._data[i];
                    prepareInputChannel(i);
                    totalNumChannels++;
                }

                for (int i = processorOuts; i < processorIns; i++)
                {
                    channels[totalNumChannels] = tempBuffer.GetWriteChannel(i - outs.numChannels);
                    prepareInputChannel(i);
                    totalNumChannels++;
                }
            }
            else
            {
                for (int i = 0; i < processorIns; i++)
                {
                    channels[totalNumChannels] = outs._data[i];
                    prepareInputChannel(i);
                    totalNumChannels++;
                }

                for (int i = processorIns; i < processorOuts; i++)
                {
                    channels[totalNumChannels] = outs._data[i];
                    channels[totalNumChannels].Slice(0, framesPerBlock).Span.Clear();
                    totalNumChannels++;
                }
            }
        }
        
        private NumChannels FindMostSuitableLayout(AudioProcessor processor)
        {
            var layouts = new List<NumChannels>();
            layouts.Add(_deviceChannels);
            if (_deviceChannels.ins == 0 || _deviceChannels.ins == 1)
            {
                layouts.Add(new NumChannels(_defaultProcessorChannels.ins, _deviceChannels.outs));
                layouts.Add(new NumChannels(_deviceChannels.outs, _deviceChannels.outs));
            }

            return layouts.First(l => processor.IsBusStateSupported(l.ToLayout()));
        }

        private void ResizeChannels()
        {
            var maxChannels = new int[]{_deviceChannels.ins, _deviceChannels.outs, _actualProcessorChannels.ins,
                _actualProcessorChannels.outs}.Max();
             _tempBuffer.SetSize(maxChannels, _framesPerBlock);
            // _channels
        }
        
        
        private AudioProcessor _processor;
        private object _lock = new();
        private int _sampleRate;
        private int _framesPerBlock;
        private bool _isPrepared;
        private NumChannels _deviceChannels;
        private NumChannels _defaultProcessorChannels;
        private NumChannels _actualProcessorChannels;
        private Memory<float>[] _channels = new Memory<float>[32];
        private AudioBuffer _tempBuffer = new AudioBuffer();
        
        public AudioProcessor CurrentProcessor { get=>_processor; }
        
        public AudioProcessorPlayer()
        {
        }

        public void SetProcessor(AudioProcessor processor)
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

                AudioProcessor old = null;
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

        public void AudioDeviceCallback(AudioBuffer input, AudioBuffer output, int numFrames)
        {
            lock (_lock)
            {
                Trace.Assert(_sampleRate>0 && _framesPerBlock>0 );
                InitializeBuffers(new ChannelInfo(input), new ChannelInfo(output),
                    numFrames, _actualProcessorChannels.ins, _actualProcessorChannels.outs, _tempBuffer, _channels);
                var totalNumChannels = Math.Max(_actualProcessorChannels.ins, _actualProcessorChannels.outs);
                var buffer = new AudioBuffer(_channels, totalNumChannels, numFrames);
                if (_processor != null)
                {
                    Trace.Assert(output.NumberOfChannels==_actualProcessorChannels.outs);
                    lock (_processor.ProcessLock)
                    {
                        if (!_processor.SuspendProcessing)
                        {
                            _processor.Process(buffer);
                        }
                        return;
                    }
                }

                output.Zero();
            }
        }

        public void AudioDeviceAboutToStart(IAudioDevice device)
        {
            var newSampleRate = device.CurrentSampleRate;
            var newFramesPerBlock = device.CurrentFramesPerBlock;
            var numChannelIn = device.ActiveInputChannels.Count;
            var numChannelOut = device.ActiveOutputChannels.Count;

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