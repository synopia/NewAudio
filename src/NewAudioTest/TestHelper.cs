using System.Collections.Generic;
using VL.NewAudio;

namespace NewAudioTest
{
    public class TestHelper
    {
        public static float[] GenerateBuffer(float[] levels, int len)
        {
            float[] buf = new float[len];
            for (int i = 0; i < len; i++)
            {
                buf[i] = levels[i % levels.Length];
            }

            return buf;
        }

        
    }
    
}