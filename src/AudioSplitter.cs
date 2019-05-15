using System.Linq;
using NAudio.Wave;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSplitter
    {
        private AudioSampleBuffer currentInput;
        private int[] currentOutputMap;
        private AudioSampleBuffer output;
        private AudioSampleBuffer[] outputs;

        public Spread<AudioSampleBuffer> Update(AudioSampleBuffer input, Spread<int> outputMap)
        {
            var array = outputMap?.ToArray();
            if (input != currentInput || array != currentOutputMap)
            {
                currentInput = input;
                currentOutputMap = array;


                if (input != null && array != null && array.Length > 0)
                {
                    var outputChannels = array.Length;
                    output = new AudioSampleBuffer(
                        WaveFormat.CreateIeeeFloatWaveFormat(input.WaveFormat.SampleRate, outputChannels));

//                    output = new AudioSampleBuffer();
                    for (int i = 0; i < outputChannels; i++)
                    {
                    }
                }
            }

            return outputs.ToSpread();
        }
    }
}