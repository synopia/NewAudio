using NUnit.Framework;
using VL.NewAudio.Dsp;

namespace VL.NewAudioTest.Dsp
{
    [TestFixture]
    public class VectorOpTest
    {
        public static float Rms(float[] array, int length)
        {
            var result = 0.0f;
            for (var i = 0; i < length; i++)
            {
                var value = array[i];
                result += value * value;
            }

            return result;
        }

        [Test]
        public void TestMulScalar()
        {
            float[] arr = new float[32];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = i + 1;
            }
            VectorOp.Mul.Scalar(new AudioChannel(arr, 0, 32), 2, 32);
            for (int i = 0; i < arr.Length; i++)
            {
                Assert.AreEqual((float)2*(i+1), arr[i]);
            }
        }

        [Test]
        public void TestMulScalarUneven()
        {
            float[] arr = new float[33];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = i + 1;
            }
            VectorOp.Mul.Scalar(new AudioChannel(arr, 0, 33), 2, 33);
            for (int i = 0; i < arr.Length; i++)
            {
                Assert.AreEqual((float)2*(i+1), arr[i]);
            }
        }

        [Test]
        public void TestRms()
        {
            float[] arr = new float[32];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = i + 1;
            }

            var expected = Rms(arr, arr.Length);
            var actual = VectorOp.Rms.Accu(new AudioChannel(arr, 0, 32), 32);
            Assert.AreEqual(expected, actual);
        }
    }
}