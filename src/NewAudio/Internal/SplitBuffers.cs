using System;
using NewAudio.Core;

namespace NewAudio.Internal
{
    public class SplitBuffers
    {
        private AudioFormat _recordedFormat;
        private byte[] _data;
        public SplitBuffers(AudioFormat recordedFormat)
        {
            _recordedFormat = recordedFormat;

        }

        public void SetBuffer(byte[] data)
        {
            _data = data;
        }

        public unsafe AudioDataMessage CreateMessage(AudioFormat format, int offset)
        {
            var msg = new AudioDataMessage(format, _recordedFormat.NumberOfFrames);
            fixed (byte* ptr = _data)
            {
                var intPtr = new IntPtr(ptr);
                intPtr += offset * _recordedFormat.BytesPerSample;
                
                for (int i = 0; i < _recordedFormat.NumberOfFrames; i++)
                {
                    for (int ch = 0; ch < format.NumberOfChannels; ch++)
                    {
                        msg.Data[i * format.NumberOfChannels + ch] = *((float*)intPtr);
                        intPtr += _recordedFormat.BytesPerSample;
                    }
                    intPtr += _recordedFormat.BytesPerSample*(_recordedFormat.NumberOfChannels-format.NumberOfChannels);
                }
            }

            return msg;
        }
    }
}