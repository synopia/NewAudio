using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Xt;

namespace VL.NewAudio.Internal
{
    public sealed class SafeBuffer : IDisposable
    {
        public static readonly Dictionary<XtSample, Type> Types
            = new()
            {
                { XtSample.UInt8, typeof(byte) },
                { XtSample.Int16, typeof(short) },
                { XtSample.Int24, typeof(byte) },
                { XtSample.Int32, typeof(int) },
                { XtSample.Float32, typeof(float) }
            };

        private readonly int _inputs;
        private readonly int _outputs;
        private readonly Array _input;
        private readonly Array _output;
        private readonly XtFormat _format;
        private readonly bool _interleaved;
        private readonly XtAttributes _attrs;

        public Array GetInput()
        {
            return _input;
        }

        public Array GetOutput()
        {
            return _output;
        }

        public void Dispose()
        {
        }

        private int _frames;

        public SafeBuffer(bool interleaved, XtFormat format, int frames)
        {
            _frames = frames;

            _interleaved = interleaved;
            _format = format;
            _inputs = _format.channels.inputs;
            _outputs = _format.channels.outputs;
            _attrs = XtAudio.GetSampleAttributes(_format.mix.sample);
            _input = CreateBuffer(_inputs);
            _output = CreateBuffer(_outputs);
        }

        private Array CreateBuffer(int channels)
        {
            var type = Types[_format.mix.sample];
            var elems = _frames * _attrs.count;
            if (_interleaved)
            {
                return Array.CreateInstance(type, channels * elems);
            }

            var result = Array.CreateInstance(type.MakeArrayType(), channels);
            for (var i = 0; i < channels; i++)
            {
                result.SetValue(Array.CreateInstance(type, elems), i);
            }

            return result;
        }

        public void Lock(in XtBuffer buffer)
        {
            if (buffer.input == IntPtr.Zero)
            {
                return;
            }

            if (_interleaved)
            {
                LockInterleaved(buffer);
            }
            else
            {
                for (var i = 0; i < _inputs; i++)
                {
                    LockChannel(buffer, i);
                }
            }
        }

        public void Unlock(in XtBuffer buffer)
        {
            if (buffer.output == IntPtr.Zero)
            {
                return;
            }

            if (_interleaved)
            {
                UnlockInterleaved(buffer);
            }
            else
            {
                for (var i = 0; i < _outputs; i++)
                {
                    UnlockChannel(buffer, i);
                }
            }
        }

        private void LockInterleaved(in XtBuffer buffer)
        {
            var elems = _inputs * buffer.frames * _attrs.count;
            FromNative(buffer.input, _input, elems);
        }

        private void UnlockInterleaved(in XtBuffer buffer)
        {
            var elems = _outputs * buffer.frames * _attrs.count;
            ToNative(_output, buffer.output, elems);
        }

        private unsafe void LockChannel(in XtBuffer buffer, int channel)
        {
            var elems = buffer.frames * _attrs.count;
            var channelBuffer = ((IntPtr*)buffer.input)[channel];
            FromNative(channelBuffer, (Array)_input.GetValue(channel), elems);
        }

        private unsafe void UnlockChannel(in XtBuffer buffer, int channel)
        {
            var elems = buffer.frames * _attrs.count;
            var channelBuffer = ((IntPtr*)buffer.output)[channel];
            ToNative((Array)_output.GetValue(channel), channelBuffer, elems);
        }

        private void ToNative(Array source, IntPtr dest, int count)
        {
            switch (_format.mix.sample)
            {
                case XtSample.UInt8:
                    Marshal.Copy((byte[])source, 0, dest, count);
                    break;
                case XtSample.Int16:
                    Marshal.Copy((short[])source, 0, dest, count);
                    break;
                case XtSample.Int24:
                    Marshal.Copy((byte[])source, 0, dest, count);
                    break;
                case XtSample.Int32:
                    Marshal.Copy((int[])source, 0, dest, count);
                    break;
                case XtSample.Float32:
                    Marshal.Copy((float[])source, 0, dest, count);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private void FromNative(IntPtr source, Array dest, int count)
        {
            switch (_format.mix.sample)
            {
                case XtSample.UInt8:
                    Marshal.Copy(source, (byte[])dest, 0, count);
                    break;
                case XtSample.Int16:
                    Marshal.Copy(source, (short[])dest, 0, count);
                    break;
                case XtSample.Int24:
                    Marshal.Copy(source, (byte[])dest, 0, count);
                    break;
                case XtSample.Int32:
                    Marshal.Copy(source, (int[])dest, 0, count);
                    break;
                case XtSample.Float32:
                    Marshal.Copy(source, (float[])dest, 0, count);
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}