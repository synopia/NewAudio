﻿using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Serilog;

namespace VL.NewAudio.Dsp
{
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _length;

        public UnmanagedMemoryManager(T* pointer, int length)
        {
            _pointer = pointer;
            _length = length;
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex >= _length || elementIndex < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            return new MemoryHandle(_pointer + elementIndex);
        }

        public override void Unpin()
        {
        }

        public override Span<T> GetSpan()
        {
            return new Span<T>(_pointer, _length);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    public class AudioBuffer : IDisposable
    {
        private int _numberOfChannels;
        private int _numberOfFrames;
        private int _allocatedSamples;
        private IMemoryOwner<float>? _data;
        private Memory<float>[] _preallocated = new Memory<float>[32];
        private Memory<float>[] _channels;

        public int Size => _numberOfChannels * _numberOfFrames;
        public bool IsClear { get; private set; }

        public virtual int NumberOfFrames
        {
            get => _numberOfFrames;
            set => throw new Exception();
        }

        public virtual int NumberOfChannels
        {
            get => _numberOfChannels;
            set => throw new Exception();
        }

        private bool _disposedValue;

        public AudioBuffer()
        {
            _channels = _preallocated;
        }

        public AudioBuffer(int numberOfChannels, int numberOfFrames)
        {
            Trace.Assert(numberOfChannels >= 0 && numberOfFrames >= 0);
            _numberOfFrames = numberOfFrames;
            _numberOfChannels = numberOfChannels;

            _channels = AllocateData();
        }

        public AudioBuffer(Memory<float>[] data, int numberOfChannels, int numberOfFrames)
        {
            Trace.Assert(data.Length >= numberOfChannels && numberOfChannels >= 0 && numberOfFrames >= 0);
            _numberOfChannels = numberOfChannels;
            _numberOfFrames = numberOfFrames;

            _channels = AllocateChannels(data, 0);
        }

        public AudioBuffer(Memory<float>[] data, int numberOfChannels, int frameOffset, int numberOfFrames)
        {
            Trace.Assert(data.Length >= numberOfChannels && numberOfChannels >= 0 && numberOfFrames >= 0);
            _numberOfChannels = numberOfChannels;
            _numberOfFrames = numberOfFrames;

            _channels = AllocateChannels(data, frameOffset);
        }

        public AudioBuffer(AudioBuffer other)
        {
            _numberOfChannels = other._numberOfChannels;
            _numberOfFrames = other._numberOfFrames;
            _allocatedSamples = other._allocatedSamples;

            if (_allocatedSamples == 0)
            {
                _channels = AllocateChannels(other._channels, 0);
            }
            else
            {
                _channels = AllocateData();
                if (other.IsClear)
                {
                    Zero();
                }
                else
                {
                    for (var i = 0; i < _numberOfChannels; i++)
                    {
                        other._channels[i].CopyTo(_channels[i]);
                    }
                }
            }
        }

        public void ApplyGain(int channel, int startFrame, int numFrames, float gain)
        {
            if (Math.Abs(gain - 1) > float.Epsilon)
            {
                Dsp.Mul(_channels[channel].Slice(startFrame, numFrames).Span, gain,
                    _channels[channel].Slice(startFrame, numFrames).Span, numFrames);
            }
        }

        public bool HasBeenCleared()
        {
            return IsClear;
        }

        public void SetNotClear()
        {
            IsClear = false;
        }

        public float this[int channel, int index]
        {
            get => GetSample(channel, index);
            set => SetSample(channel, index, value);
        }

        public Memory<float> this[int channel] => GetWriteChannel(channel);

        public float GetSample(int channel, int index)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels);
            Trace.Assert(index >= 0 && index < _numberOfFrames);

            return _channels[channel].Span[index];
        }

        public void SetSample(int channel, int index, float value)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels);
            Trace.Assert(index >= 0 && index < _numberOfFrames);

            _channels[channel].Span[index] = value;
        }

        public void CopyFrom(AudioBuffer other)
        {
            if (other == this)
            {
                return;
            }

            SetSize(other.NumberOfChannels, other.NumberOfFrames);
            if (other.IsClear)
            {
                Zero();
            }
            else
            {
                IsClear = false;
                for (var i = 0; i < _numberOfChannels; i++)
                {
                    other._channels[i].CopyTo(_channels[i]);
                }
            }
        }


        public void SetData(Memory<float>[] data, int numberOfChannels, int frameOffset, int numberOfFrames)
        {
            Trace.Assert(data.Length >= numberOfFrames * numberOfChannels && numberOfChannels >= 0 &&
                         frameOffset >= 0 && numberOfFrames >= 0);

            if (_allocatedSamples > 0)
            {
                _allocatedSamples = 0;
                _data?.Dispose();
            }

            _numberOfChannels = numberOfChannels;
            _numberOfFrames = numberOfFrames;
            AllocateChannels(data, frameOffset);
            Trace.Assert(!IsClear);
        }

        public void SetData(Memory<float>[] data, int numberOfChannels, int numberOfFrames)
        {
            SetData(data, numberOfChannels, 0, numberOfFrames);
        }

        public void CopyFrom(int destChannel, int destFrame, AudioBuffer source, int sourceChannel, int sourceFrame,
            int numFrames)
        {
            Trace.Assert(source != this || destChannel != sourceChannel || destFrame >= sourceFrame + numFrames ||
                         sourceFrame >= destFrame + numFrames);
            Trace.Assert(destChannel >= 0 && destChannel < _numberOfChannels);
            Trace.Assert(destFrame >= 0 && destFrame + numFrames <= _numberOfFrames);
            Trace.Assert(sourceChannel >= 0 && sourceChannel < source.NumberOfChannels);
            Trace.Assert(sourceFrame >= 0 && sourceFrame + numFrames <= source.NumberOfFrames);

            var s = source._channels[sourceChannel].Slice(sourceFrame, numFrames);
            var d = _channels[destChannel].Slice(destFrame, numFrames);
            s.CopyTo(d);
        }

        private void PrepareChannel(int channel, AudioBuffer? input, int index)
        {
            if (input == null || input.NumberOfChannels == 0)
            {
                ZeroChannel(channel);
            }
            else
            {
                input[index % input.NumberOfChannels].Span.CopyTo(_channels[channel].Span);
            }
        }

        public void Merge(AudioBuffer? input, AudioBuffer output, int inputChannels, int outputChannels)
        {
            Trace.Assert(input == null || input.NumberOfFrames == output.NumberOfFrames);
            var totalAfterMerge = new[]
                { input?.NumberOfChannels ?? 0, output.NumberOfChannels, inputChannels, outputChannels }.Max();

            SetSize(totalAfterMerge, output.NumberOfFrames, false, false, true);
            var channelNumber = 0;

            if (inputChannels > outputChannels)
            {
                Trace.Assert(NumberOfChannels >= inputChannels - outputChannels);
                Trace.Assert(NumberOfFrames >= (input?.NumberOfFrames ?? 0));
                Trace.Assert(NumberOfFrames >= output.NumberOfFrames);

                for (var i = 0; i < outputChannels; i++)
                {
                    _channels[channelNumber] = output[i];
                    PrepareChannel(channelNumber, input, i);
                    channelNumber++;
                }

                for (var i = outputChannels; i < inputChannels; i++)
                {
                    PrepareChannel(channelNumber, input, i);
                    channelNumber++;
                }
            }
            else
            {
                for (var i = 0; i < inputChannels; i++)
                {
                    _channels[channelNumber] = output[i];
                    PrepareChannel(channelNumber, input, i);
                    channelNumber++;
                }

                for (var i = inputChannels; i < outputChannels; i++)
                {
                    _channels[channelNumber] = output[i];
                    ZeroChannel(channelNumber);
                    channelNumber++;
                }
            }
        }


        public void SetSize(int newNumChannels, int newNumFrames, bool keep = false, bool clearExtra = false,
            bool avoidReallocating = false)
        {
            Trace.Assert(newNumChannels >= 0 && newNumFrames >= 0);

            if (newNumChannels != _numberOfChannels || newNumFrames != _numberOfFrames)
            {
                var newSize = newNumChannels * newNumFrames;
                if (keep)
                {
                    if (avoidReallocating && newNumChannels <= _numberOfChannels && newNumFrames <= _numberOfFrames)
                    {
                    }
                    else
                    {
                        var data = MemoryPool<float>.Shared.Rent(newSize);

                        var numFramesToCopy = Math.Min(newNumFrames, _numberOfFrames);
                        if (clearExtra || IsClear)
                        {
                            data.Memory.Span.Clear();
                        }

                        var newChannels = new Memory<float>[newNumChannels + 1];
                        for (var j = 0; j < newNumChannels; j++)
                        {
                            newChannels[j] = data.Memory.Slice(newNumFrames * j);
                        }

                        if (!IsClear)
                        {
                            var numChannelsToCopy = Math.Min(newNumChannels, _numberOfChannels);
                            for (var i = 0; i < numChannelsToCopy; i++)
                            {
                                _channels[i].CopyTo(newChannels[i]);
                            }
                        }

                        _allocatedSamples = newSize;
                        _data?.Dispose();
                        _data = data;
                        _numberOfChannels = newNumChannels;
                        _numberOfFrames = newNumFrames;
                    }
                }
                else
                {
                    if (avoidReallocating && _allocatedSamples >= newSize)
                    {
                        if (clearExtra || IsClear)
                        {
                            _data?.Memory.Span.Clear();
                        }
                    }
                    else
                    {
                        if (_allocatedSamples > 0)
                        {
                            _data!.Dispose();
                        }

                        _allocatedSamples = newSize;
                        _data = MemoryPool<float>.Shared.Rent(newSize);
                    }

                    _channels = new Memory<float>[newNumChannels];
                    for (var j = 0; j < newNumChannels; j++)
                    {
                        _channels[j] = _data!.Memory.Slice(newNumFrames * j, newNumFrames);
                    }
                }

                _numberOfChannels = newNumChannels;
                _numberOfFrames = newNumFrames;
            }
        }

        private Memory<float>[] AllocateData()
        {
            _allocatedSamples = _numberOfChannels * _numberOfFrames;
            _data = MemoryPool<float>.Shared.Rent(_allocatedSamples);
            _channels = new Memory<float>[_numberOfChannels];

            for (var i = 0; i < _numberOfChannels; i++)
            {
                _channels[i] = _data.Memory.Slice(i * _numberOfFrames, _numberOfFrames);
            }

            IsClear = false;

            return _channels;
        }

        private Memory<float>[] AllocateChannels(Memory<float>[] data, int offset)
        {
            Trace.Assert(offset >= 0);
            if (_numberOfChannels <= _preallocated.Length)
            {
                _channels = _preallocated;
            }
            else
            {
                _channels = new Memory<float>[_numberOfChannels];
            }

            for (var i = 0; i < _numberOfChannels; i++)
            {
                _channels[i] = data[i].Slice(offset, _numberOfFrames);
            }


            IsClear = false;

            return _channels;
        }

        public Span<float> GetReadChannel(int channel)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels);
            return _channels[channel].Span;
        }

        public Span<float> GetReadChannel(int channel, int frameOffset)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels && frameOffset >= 0 &&
                         frameOffset <= _numberOfFrames);
            return _channels[channel].Span.Slice(frameOffset);
        }

        public Memory<float> GetWriteChannel(int channel)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels);
            IsClear = false;
            return _channels[channel];
        }

        public Memory<float> GetWriteChannel(int channel, int frameOffset)
        {
            Trace.Assert(channel >= 0 && channel < _numberOfChannels && frameOffset >= 0 &&
                         frameOffset <= _numberOfFrames);
            IsClear = false;
            return _channels[channel].Slice(frameOffset);
        }

        public Memory<float>[] GetReadChannels()
        {
            return _channels;
        }

        public Memory<float>[] GetWriteChannels()
        {
            IsClear = false;
            return _channels;
        }


        public void ZeroChannel(int ch)
        {
            Trace.Assert(ch >= 0 && ch < _numberOfChannels);
            _channels[ch].Span.Clear();
        }

        public void Zero()
        {
            for (var ch = 0; ch < NumberOfChannels; ch++)
            {
                _channels[ch].Span.Clear();
            }
        }

        public void Zero(int start, int frames)
        {
            for (var ch = 0; ch < NumberOfChannels; ch++)
            {
                _channels[ch].Slice(start, frames).Span.Clear();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_allocatedSamples > 0)
                    {
                        _data.Dispose();
                        _allocatedSamples = 0;
                    }

                    _data = null;
                }

                _disposedValue = true;
            }
        }
    }
}