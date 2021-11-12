﻿using System;
using System.Buffers;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using VL.Lib.Collections;

namespace NewAudio.Nodes
{
    public enum AudioBufferOutType
    {
        Skip,
        SkipHalf,
        Max
    }

    public class AudioBufferOutInitParams : AudioNodeInitParams
    {
        public AudioParam<int> OutputSize;
        public AudioParam<int> BlockSize;
        public AudioParam<AudioBufferOutType> Type;
    }

    public class AudioBufferOutPlayParams : AudioNodePlayParams
    {
    }

    public class AudioBufferOut : AudioNode<AudioBufferOutInitParams, AudioBufferOutPlayParams>
    {
        public override string NodeName => "Buffer Out";
        private float[] _outBuffer;
        private float[] _tempBuffer;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private IDisposable _link1;
        private ActionBlock<AudioDataMessage[]> _processor;
        private SpreadBuilder<float> _spreadBuilder = new();
        private int _updated;

        public AudioBufferOut()
        {
            InitLogger<AudioBufferOut>();
            Logger.Information("AudioBufferOut created");
        }

        public override bool IsPlayValid()
        {
            return PlayParams.Input.Value != null && PlayParams.Input.Value.Format.BufferSize>0;
        }

        public override bool IsInitValid()
        {
            return InitParams.OutputSize.Value > 0 &&
                   InitParams.BlockSize.Value > 0;
        }

        /// <summary>
        /// Takes audio samples from an audio link and returns a spread of float32
        /// </summary>
        /// <param name="input">The audio link to get samples from</param>
        /// <param name="outputSize">The resulting total size of the spread (needs to be a power of two)</param>
        /// <param name="blockSize">Number of samples from the input, to be merged into one output sample</param>
        /// <param name="bufferSize">Size of input buffer</param>
        /// <param name="type">Skip: take first sample, skip until blockSize is reached, SkipHalf: take samples until 1/blocksize of input is reached, skip the rest, Max: output the max of a block</param>
        /// <returns></returns>
        public Spread<float> Update(AudioLink input, int outputSize = 1024, int blockSize = 8,
            AudioBufferOutType type = AudioBufferOutType.Max, int bufferSize = 4)
        {
            PlayParams.BufferSize.Value = bufferSize;
            PlayParams.Input.Value = input;
            InitParams.OutputSize.Value = (int)Utils.UpperPow2((uint)outputSize);
            InitParams.BlockSize.Value = blockSize;
            InitParams.Type.Value = type;

            Update();

            if (_updated > 0)
            {
                _spreadBuilder.Clear();
                _spreadBuilder.AddRangeArray(_outBuffer, InitParams.OutputSize.Value, 0, false);
                _updated--;
            }

            return _spreadBuilder.ToSpread();
        }

        public override Task<bool> Init()
        {
       
            return Task.FromResult(true);
        }

        public override bool Play()
        {
            var type = InitParams.Type.Value;
            _batchSize = 0;
            if (type == AudioBufferOutType.Skip)
            {
                _batchSize = CreateTypeSkip();
            }
            else if (type == AudioBufferOutType.SkipHalf)
            {
                _batchSize = CreateTypeSkipHalf();
            }
            else
            {
                _batchSize = CreateTypeMax();
            }

            _batchBlock = new BatchBlock<AudioDataMessage>(_batchSize);
            _link1 = _batchBlock.LinkTo(_processor);

            TargetBlock = _batchBlock;
            return true;
        }

        public override bool Stop()
        {
            if (_processor == null)
            {
                Logger.Error("ActionBlock == null!");
            }

            if (_link1 == null)
            {
                Logger.Error("Link == null!");
            }

            _link1?.Dispose();
            _link1 = null;
            _processor?.Complete();
            ArrayPool<float>.Shared.Return(_outBuffer);
            ArrayPool<float>.Shared.Return(_tempBuffer);
            _outBuffer = null;
            TargetBlock = null;
             _processor?.Completion.ContinueWith(t =>
            {
                _processor = null;
                Logger.Information("ActionBlock stopped, status={Status}", t.Status);
                return true;
            });
             return true;
        }

        public override Task<bool> Free()
        {
            return Task.FromResult(true);

            
        }

        public override string DebugInfo()
        {
            return $"[{this}, batchSize={_batchSize}, {base.DebugInfo()}]";
        }

        private int CreateTypeSkip()
        {
            var bufferSize = InitParams.OutputSize.Value;
            var skipSize = InitParams.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                var k = 0;
                for (int i = 0, j = 0; i < bufferSize; i++, j += skipSize)
                {
                    if (j >= inputBufferSize)
                    {
                        j -= inputBufferSize;
                        k++;
                    }

                    _tempBuffer[i] = input[k].Data[j];
                }

                Array.Copy(_tempBuffer, _outBuffer, _outBuffer.Length);
                _updated++;
            });
            return skipSize * bufferSize / inputBufferSize;
        }

        private int CreateTypeSkipHalf()
        {
            var bufferSize = InitParams.OutputSize.Value;
            var skipSize = InitParams.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;
            if (inputBufferSize == 0)
            {
                Logger.Error("SHOULD NOT HAPPEN!!");
            }
            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                var k = 0;
                for (int i = 0, j = 0; i < bufferSize; i++, j++)
                {
                    if (j >= inputBufferSize)
                    {
                        j -= inputBufferSize;
                        k++;
                    }

                    _tempBuffer[i] = input[k].Data[j];
                }

                Array.Copy(_tempBuffer, _outBuffer, _outBuffer.Length);
                _updated++;
            });
            return skipSize * bufferSize / inputBufferSize;
        }

        private int CreateTypeMax()
        {
            var bufferSize = InitParams.OutputSize.Value;
            var blockSize = InitParams.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                var count = blockSize * bufferSize;
                var skip = count / bufferSize;
                var k = 0;
                var j = 0;
                for (var i = 0; i < bufferSize; i++)
                {
                    var min = 0.0f;
                    var max = 0.0f;
                    for (var b = 0; b < skip; b++)
                    {
                        max = Math.Max(max, input[k].Data[j]);
                        min = Math.Min(min, input[k].Data[j]);
                        j++;
                        if (j >= inputBufferSize)
                        {
                            j -= inputBufferSize;
                            k++;
                        }
                    }

                    _tempBuffer[i] = max > -min ? max : min;
                    _updated++;
                }

                Array.Copy(_tempBuffer, _outBuffer, _outBuffer.Length);
            });
            return blockSize * bufferSize / inputBufferSize;
        }

        private bool _disposedValue;
        private int _batchSize;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}