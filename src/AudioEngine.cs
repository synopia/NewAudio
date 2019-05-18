using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VL.Core;

namespace VL.NewAudio
{
    public interface IAudioProcessor
    {
        List<AudioSampleBuffer> GetInputs();
        int Read(float[] buffer, int offset, int count);
    }

    public abstract class BaseAudioNode : INotifyHotSwapped
    {
        public bool HotSwapped;

        public void Swapped(object newInstance)
        {
            try
            {
                var s = RuntimeReflectionExtensions.GetRuntimeField(newInstance.GetType(), "HotSwapped");
                s.SetValue(newInstance, true);
            }
            catch (Exception e)
            {
                AudioEngine.Log(e.Message);
            }
        }
    }

    public static class AudioEngine
    {
        public static float PI = (float) Math.PI;

        public static float TanH(float v)
        {
            return (float) Math.Tanh(v);
        }

        public static float SinF(float v)
        {
            return (float) Math.Sin(v);
        }

        public static float CosF(float v)
        {
            return (float) Math.Cos(v);
        }

        public static float SinC(float v)
        {
            if (Math.Abs(v) < 0.0000001f)
            {
                return 1.0f;
            }

            v *= PI;
            return SinF(v) / v;
        }

        public static bool SequenceEquals<T>(IEnumerable<T> first, IEnumerable<T> second)
        {
            if (first == second)
                return true;
            if (first == null && second == null)
                return true;
            if (first == null || second == null)
                return false;
            return first.SequenceEqual(second);
        }

        public static bool ArrayEquals<T>(T[] first, T[] second)
        {
            if (first == second)
                return true;
            if (first == null && second == null)
                return true;
            if (first == null || second == null)
                return false;
            if (first.Length != second.Length)
                return false;
            for (var i = 0; i < first.Length; i++)
            {
                if (first[i]?.GetHashCode() != second[i]?.GetHashCode())
                    return false;
            }

            return true;
        }

        private static StreamWriter log = File.CreateText("out.log");
        private static string lastEntry;

        public static void Log(Exception e)
        {
            Log($"{e.Message}\n{e.StackTrace}");
        }

        public static void Log(string line)
        {
            if (line != lastEntry)
            {
                log.WriteLine(line);
                log.Flush();
                lastEntry = line;
            }
        }
    }
}