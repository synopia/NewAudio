using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSplitter
    {
        private struct Configuration
        {
            public AudioSampleBuffer Input;
            public int[] ChannelMap;
            public bool HasChanges;

            public void Update(AudioSampleBuffer input, Spread<int> channelMap)
            {
                var arrayMap = channelMap?.ToArray();
                HasChanges = input != Input || !AudioEngine.ArrayEquals(ChannelMap, arrayMap);

                Input = input;
                ChannelMap = arrayMap;
            }

            public bool IsValid()
            {
                return Input != null;
            }
        }

        private Configuration config = new Configuration();
        private AudioSampleBuffer[] outputs;
        private CircularSampleBuffer[] buffers;
        private float[] tempBuffer;

        private Spread<AudioSampleBuffer> output;

        public Spread<AudioSampleBuffer> Update(AudioSampleBuffer input, Spread<int> channelMap)
        {
            config.Update(input, channelMap);

            if (config.HasChanges)
            {
                AudioEngine.Log($"AudioSplitter configuration changed!");

                if (config.IsValid())
                {
                    Build();
                }
                else
                {
                    output = Spread<AudioSampleBuffer>.Empty;
                }
            }

            return output;
        }

        private void Build()
        {
            var channelMap = GetChannelMap();

            var outputBuffers = channelMap.Length;
            var outputChannels = channelMap.Sum();

            outputs = new AudioSampleBuffer[outputBuffers];
            buffers = new CircularSampleBuffer[outputBuffers];
            var buffersMapped = new CircularSampleBuffer[outputChannels];

            var input = config.Input;
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
                var reader = i;

                var outputBuffer =
                    new AudioSampleBuffer(
                        WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate, channelMap[i]));
                outputBuffer.Update = (b, o, len) =>
                {
                    var samplesToRead = len / channelMap[reader];

                    while (buffers[reader].Count < samplesToRead * channelMap[reader])
                    {
                        if (tempBuffer == null || tempBuffer.Length < samplesToRead * inputChannels)
                        {
                            tempBuffer = new float[samplesToRead * inputChannels];
                        }

                        var dataRead = input.Read(tempBuffer, o, samplesToRead * inputChannels);
                        int outputIndex = 0;
                        for (int j = 0; j < dataRead; j++)
                        {
                            buffersMapped[outputIndex].Add(tempBuffer[j]);
                            outputIndex++;
                            outputIndex %= outputChannels;
                        }
                    }

                    return buffers[reader].Read(b, o, len);
                };
                outputs[i] = outputBuffer;
            }

            output = outputs.ToSpread();
        }

        private int[] GetChannelMap()
        {
            var channelMap = config.ChannelMap;
            if (channelMap == null || channelMap.Length == 0)
            {
                var channels = config.Input.WaveFormat.Channels;
                channelMap = new int[channels];
                for (int i = 0; i < channels; i++)
                {
                    channelMap[i] = 1;
                }
            }

            return channelMap;
        }
    }
}