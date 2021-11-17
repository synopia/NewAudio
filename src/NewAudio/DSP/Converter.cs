using System;

namespace NewAudio.Dsp
{
    public static class Converter
    {
        public static unsafe void Interleave(float[] source, byte[] interleaved, int frames, int channels, int framesToCopy )
        {
            fixed(byte* ptr= interleaved)
            {
                var intPtr = new IntPtr(ptr);
                
                for (int ch = 0; ch < channels; ch++)
                {
                    var s = new Span<float>(source, ch * frames, frames);
                    for (int i = 0, j = ch; i < framesToCopy; i++, j += channels)
                    {
                        *(float*)(intPtr+j*4) = s[i];
                    }
                }
            }
        }
    }
}