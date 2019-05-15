using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSplitter
    {
        private AudioSampleBuffer currentInput;
        private int[] currentOutputMap;
        private AudioSampleBuffer[] outputs;
        private CircularSampleBuffer[] buffers;
        private float[] tempBuffer;
        private float[] tempBuffer2;

        private Spread<AudioSampleBuffer> output;

        public Spread<AudioSampleBuffer> Update(AudioSampleBuffer input, Spread<int> outputMap)
        {
            var array = outputMap?.ToArray();
            if (input != currentInput || !AudioEngine.ArrayEquals(array, currentOutputMap))
            {
                currentInput = input;
                currentOutputMap = array;
                AudioEngine.Log($"AudioSplitter configuration changed!");

                if (input != null && array != null && array.Length > 0)
                {
                    var outputBuffers = array.Length;

                    outputs = new AudioSampleBuffer[outputBuffers];
                    buffers = new CircularSampleBuffer[outputBuffers];

                    for (int i = 0; i < outputBuffers; i++)
                    {
                        var reader = i;
                        buffers[i] = new CircularSampleBuffer(4096);

                        var outputBuffer =
                            new AudioSampleBuffer(WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate, 1));
                        outputBuffer.Update = (b, o, len) =>
                        {
                            if (buffers[reader].Count < len)
                            {
                                if (tempBuffer == null || tempBuffer.Length < len || tempBuffer2 == null ||
                                    tempBuffer2.Length < len)
                                {
                                    tempBuffer = new float[len];
                                    tempBuffer2 = new float[len];
                                }

                                for (int j = 0; j < outputBuffers; j++)
                                {
                                    input.Read(tempBuffer, o, len);
                                    for (int l = 0; l < outputBuffers; l++)
                                    {
                                        for (int k = 0; k < len / outputBuffers; k++)
                                        {
                                            tempBuffer2[k] = tempBuffer[k * outputBuffers + l];
                                        }

                                        buffers[l].Write(tempBuffer2, o, len / outputBuffers);
                                    }
                                }
                            }

                            buffers[reader].Read(b, o, len);
                        };
                        outputs[i] = outputBuffer;
                    }

                    output = outputs.ToSpread();
                }
            }

            return output;
        }
    }
}