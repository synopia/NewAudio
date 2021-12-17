using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace VL.NewAudio.Dsp
{
    public abstract class VectorOp
    {
        public static VectorOp Add = new VectorOpAdd();
        public static VectorOp Sub = new VectorOpSub();
        public static VectorOp Mul = new VectorOpMul();
        public static VectorOp Div = new VectorOpDiv();
        public static VectorOp Rms = new VectorOpRms();
        
        private static int SimdLength = Vector<float>.Count;

        protected abstract Vector<float> CalculateVector(Vector<float> target, Vector<float> value);
        protected abstract float CalculateSingle(float target, float value);

        protected virtual float Reduce(Vector<float> source)
        {
            return Vector.Dot(source, Vector<float>.One);
        }
        public unsafe void Scalar(AudioChannel target, float value, int numFrames)
        {
            int ceiling = numFrames / SimdLength * SimdLength;
            
            var v = new Vector<float>(value);
            fixed (float* t = target.Buffer)
            {
                for (int i = 0; i < ceiling; i += SimdLength)
                {
                    Unsafe.Write(t + i + target.TotalOffset,
                        CalculateVector(Unsafe.Read<Vector<float>>(t + i + target.TotalOffset), v));
                        
                }
            }
            
            for (int i = ceiling; i < numFrames; i++)
            {
                target[i] = CalculateSingle(target[i],value);
            }
        }
        
        public unsafe void Op(AudioChannel target, AudioChannel source, int numFrames)
        {
            int ceiling = numFrames / SimdLength * SimdLength;
            fixed (float* t = target.Buffer, s = source.Buffer)
            {
                for (int i = 0; i < ceiling; i += SimdLength)
                {
                    Unsafe.Write(t + i + target.TotalOffset,
                        CalculateVector(Unsafe.Read<Vector<float>>(s + i + source.TotalOffset),
                            Unsafe.Read<Vector<float>>(t + i + target.TotalOffset)));
                }
            }
            
            for (int i = ceiling; i < numFrames; i++)
            {
                target[i] = CalculateSingle(target[i], source[i]);
            }
        }

        public unsafe float Accu(AudioChannel source, int numFrames)
        {
            int ceiling = numFrames / SimdLength * SimdLength;
            Vector<float> accu = Vector<float>.Zero;
            fixed (float* s = source.Buffer)
            {
                for (int i = 0; i < ceiling; i += SimdLength)
                {
                    accu = CalculateVector(accu, Unsafe.Read<Vector<float>>(s + i + source.TotalOffset));
                }
            }

            float result = Reduce(accu);
            
            for (int i = ceiling; i < numFrames; i++)
            {
                result = CalculateSingle(result, source[i]);
            }

            return result;
        }
    }

    public class VectorOpRms : VectorOp
    {
        protected override Vector<float> CalculateVector(Vector<float> target, Vector<float> value)
        {
            return target + value * value;
        }

        protected override float CalculateSingle(float target, float value)
        {
            return target + value*value;
        }
    }

    public class VectorOpAdd : VectorOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Vector<float> CalculateVector(Vector<float> target, Vector<float> value)
        {
            return target + value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override float CalculateSingle(float target, float value)
        {
            return target + value;
        }
    }
    public class VectorOpSub : VectorOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Vector<float> CalculateVector(Vector<float> target, Vector<float> value)
        {
            return target - value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override float CalculateSingle(float target, float value)
        {
            return target - value;
        }
    }

    public class VectorOpMul : VectorOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Vector<float> CalculateVector(Vector<float> target, Vector<float> value)
        {
            return target * value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override float CalculateSingle(float target, float value)
        {
            return target * value;
        }
    }

    public class VectorOpDiv : VectorOp
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override Vector<float> CalculateVector(Vector<float> target, Vector<float> value)
        {
            return target / value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override float CalculateSingle(float target, float value)
        {
            return target / value;
        }
    }

}