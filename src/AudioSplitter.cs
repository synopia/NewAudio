using System.Collections.Generic;
using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSplitter
    {
        private AudioSampleBuffer input;
        private int[] channelMap;
        private bool hasChanges;
        private AudioSampleBuffer[] outputs;
        private Spread<AudioSampleBuffer> output;

        public Spread<AudioSampleBuffer> Update(AudioSampleBuffer input, Spread<int> channelMap)
        {
            var arrayMap = channelMap?.ToArray();
            hasChanges = input != this.input || !AudioEngine.ArrayEquals(this.channelMap, arrayMap);

            this.input = input;
            this.channelMap = arrayMap;

            if (hasChanges)
            {
                AudioEngine.Log($"AudioSplitter configuration changed!");

                if (IsValid())
                {
                    var outputsBefore = outputs?.Length ?? 0;

                    Build();

                    // Send nulls to update connected pins
                    var fillOutputs = outputsBefore - (outputs?.Length ?? 0);
                    if (fillOutputs > 0)
                    {
                        var builder = output.ToBuilder();
                        for (int i = 0; i < fillOutputs; i++)
                        {
                            builder.Add(null);
                        }

                        output = builder.ToSpread();
                    }
                }
                else
                {
                    foreach (var buffer in outputs)
                    {
                        buffer.Dispose();
                    }

                    // Send nulls to update connected pins
                    var results = outputs.Select(o => (AudioSampleBuffer) null);
                    return results.ToSpread();
                }
            }

            return output;
        }

        private bool IsValid()
        {
            return input != null;
        }


        private int[] BuildChannelMap(int[] channelMap)
        {
            if (channelMap == null || channelMap.Length == 0)
            {
                var channels = input.WaveFormat.Channels;
                channelMap = new int[channels];
                for (int i = 0; i < channels; i++)
                {
                    channelMap[i] = 1;
                }
            }

            return channelMap;
        }

        private void Build()
        {
            var channelMap = BuildChannelMap(this.channelMap);

            var outputBuffers = channelMap.Length;
            var outputChannels = channelMap.Sum();

            outputs = new AudioSampleBuffer[outputBuffers];
            var buffers = new CircularSampleBuffer[outputBuffers];
            var buffersMapped = new CircularSampleBuffer[outputChannels];
            var tempBuffer = new float[0];

            var inputChannels = input.WaveFormat.Channels;
            var index = 0;
            for (var i = 0; i < outputBuffers; i++)
            {
                buffers[i] = new CircularSampleBuffer(4096);
                for (var j = 0; j < channelMap[i]; j++)
                {
                    buffersMapped[index] = buffers[i];
                    index++;
                }
            }

            for (var i = 0; i < outputBuffers; i++)
            {
                outputs[i] = new AudioSplitBufferProcessor(input, inputChannels, outputChannels, channelMap[i],
                    buffers[i],
                    buffersMapped, tempBuffer).Build();
            }

            output = outputs.ToSpread();
        }

        private class AudioSplitBufferProcessor : IAudioProcessor
        {
            private AudioSampleBuffer input;
            private readonly int inputChannels;
            private readonly int outputChannels;
            private readonly int channels;

            private readonly CircularSampleBuffer buffer;
            private readonly CircularSampleBuffer[] buffersMapped;
            private float[] tempBuffer;

            public AudioSplitBufferProcessor(AudioSampleBuffer input, int inputChannels, int outputChannels,
                int channels, CircularSampleBuffer buffer, CircularSampleBuffer[] buffersMapped, float[] tempBuffer)
            {
                this.input = input;
                this.inputChannels = inputChannels;
                this.outputChannels = outputChannels;
                this.channels = channels;
                this.buffer = buffer;
                this.buffersMapped = buffersMapped;
                this.tempBuffer = tempBuffer;
            }

            public List<AudioSampleBuffer> GetInputs()
            {
                return new List<AudioSampleBuffer> {input};
            }

            public AudioSampleBuffer Build()
            {
                return new AudioSampleBuffer(
                    WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate, channels))
                {
                    Processor = this
                };
            }

            public int Read(float[] b, int offset, int count)
            {
                var samplesToRead = count / channels;

                while (buffer.Count < samplesToRead * channels)
                {
                    if (tempBuffer.Length < samplesToRead * inputChannels)
                    {
                        tempBuffer = new float[samplesToRead * inputChannels];
                    }

                    var dataRead = input.Read(tempBuffer, offset, samplesToRead * inputChannels);
                    int outputIndex = 0;
                    for (int j = 0; j < dataRead; j++)
                    {
                        buffersMapped[outputIndex].Add(tempBuffer[j]);
                        outputIndex++;
                        outputIndex %= outputChannels;
                    }
                }

                return buffer.Read(b, offset, count);
            }
        }
    }
}