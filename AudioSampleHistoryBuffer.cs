using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSampleHistoryBuffer
    {
        private float[] buffer;
        private int size;

        private int writePos;
        private SpreadBuilder<float> spreadBuilder = new SpreadBuilder<float>();

        public AudioSampleHistoryBuffer(float bufferTime)
        {
            size = (int) (bufferTime * WaveOutput.InternalFormat.SampleRate);
            AudioEngine.Log($"AudioSampleHistoryBuffer: Create buffer for {bufferTime}s -> {size} samples");
            buffer = new float[size];
        }

        public void AddSample(float sample)
        {
            buffer[writePos++] = sample;
            writePos %= size;
        }

        public float GetSample(float time)
        {
            int index = (int) (time * WaveOutput.InternalFormat.SampleRate);
            int readPos = writePos - index - 8;
            if (readPos < 0)
            {
                readPos += size;
            }

            readPos %= size;

            return buffer[readPos];
        }

        public Spread<float> GetSamples(float time, int downsample = 1)
        {
            int readTo = writePos;
            int distance = (int) (time * WaveOutput.InternalFormat.SampleRate);
            int readFrom = readTo - distance;
            spreadBuilder.Clear();
            if (readFrom >= 0)
            {
                for (int i = readFrom; i < readTo; i += downsample)
                {
                    spreadBuilder.Add(buffer[i]);
                }
            }
            else
            {
                for (int i = size + readFrom; i < size; i += downsample)
                {
                    spreadBuilder.Add(buffer[i]);
                }

                for (int i = 0; i < readTo; i += downsample)
                {
                    spreadBuilder.Add(buffer[i]);
                }
            }

            return spreadBuilder.ToSpread();
        }
    }
}