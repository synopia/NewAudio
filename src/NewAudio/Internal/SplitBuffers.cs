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
            var msg = new AudioDataMessage(format, _recordedFormat.SampleCount);
            fixed (byte* ptr = _data)
            {
                var intPtr = new IntPtr(ptr);
                intPtr += offset * _recordedFormat.BytesPerSample;
                
                for (int i = 0; i < _recordedFormat.SampleCount; i++)
                {
                    for (int ch = 0; ch < format.Channels; ch++)
                    {
                        msg.Data[i * format.Channels + ch] = *((float*)intPtr);
                        intPtr += _recordedFormat.BytesPerSample;
                    }
                    intPtr += _recordedFormat.BytesPerSample*(_recordedFormat.Channels-format.Channels);
                }
            }

            return msg;
        }
    }
}