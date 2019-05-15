using System;
using VL.Lib.Collections;

namespace VL.NewAudio
{
    public class AudioSampleHistoryBuffer
    {
        private float[] buffer;
        private float[] outBuffer = new float[16];
        private int outPos = 0;
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
            if (outPos >= outBuffer.Length)
            {
                int distance = (int) (time * WaveOutput.InternalFormat.SampleRate);
                int readFrom = writePos - distance;
                int consume = Math.Min(16, distance);
                if (readFrom < 0)
                {
                    readFrom += buffer.Length;
                }

                int readTo = readFrom + consume;
                if (readTo < buffer.Length)
                {
                    for (int i = 0; i < consume; i++)
                    {
                        outBuffer[i] = buffer[readFrom + i];
                    }
                }
                else
                {
                    var remain = readTo - buffer.Length;
                    for (int i = 0; i < remain; i++)
                    {
                        outBuffer[i] = buffer[readFrom + i];
                    }

                    for (int i = 0; i < consume - remain; i++)
                    {
                        outBuffer[i + remain] = buffer[i];
                    }
                }

                outPos = 0;
            }

            return outBuffer[outPos++];
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