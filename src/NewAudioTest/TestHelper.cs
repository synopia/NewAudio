using System.Collections.Generic;

namespace NewAudioTest
{
    public class TestHelper
    {
        public static float[] GenerateBuffer(float[] levels, int len)
        {
            var buf = new float[len];
            for (var i = 0; i < len; i++)
            {
                buf[i] = levels[i % levels.Length];
            }

            return buf;
        }
    }
}