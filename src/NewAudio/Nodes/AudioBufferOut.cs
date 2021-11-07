using System;
using System.Buffers;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Lib.Collections;

namespace NewAudio.Nodes
{
    public enum AudioBufferOutType
    {
        Skip,
        SkipHalf,
        Max,
    }

    public class AudioBufferOutInitParams : AudioNodeInitParams
    {
        public AudioParam<int> BufferSize;
        public AudioParam<int> BlockSize;
        public AudioParam<AudioBufferOutType> Type;
    }

    public class AudioBufferOutPlayParams : AudioNodePlayParams
    {
    }

    public class AudioBufferOut : AudioNode<AudioBufferOutInitParams, AudioBufferOutPlayParams>
    {
        private readonly ILogger _logger;

        private float[] _outBuffer;
        private float[] _tempBuffer;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private IDisposable _link1;
        private IDisposable _inputBufferLink;
        private ActionBlock<AudioDataMessage[]> _processor;
        private SpreadBuilder<float> _spreadBuilder = new SpreadBuilder<float>();
        private int _updated;
        
        public AudioBufferOut()
        {
            _logger = AudioService.Instance.Logger.ForContext<AudioBufferOut>();
            _logger.Information("AudioBufferOut created");
        }

        public override bool IsPlayValid()
        {
            return PlayParams.Input.Value != null;
        }

        public override bool IsInitValid()
        {
            return InitParams.BufferSize.Value>0 &&
                   InitParams.BlockSize.Value > 0;
        }

        /// <summary>
        /// Takes audio samples from an audio link and returns a spread of float32
        /// </summary>
        /// <param name="input">The audio link to get samples from</param>
        /// <param name="bufferSize">The resulting total size of the spread (needs to be a power of two)</param>
        /// <param name="blockSize">Number of samples from the input, to be merged into one output sample</param>
        /// <param name="type">Skip: take first sample, skip until blockSize is reached, SkipHalf: take samples until 1/blocksize of input is reached, skip the rest, Max: output the max of a block</param>
        /// <returns></returns>
        public Spread<float> Update(AudioLink input, int bufferSize = 1024, int blockSize = 8,
            AudioBufferOutType type = AudioBufferOutType.Max)
        {
            PlayParams.Input.Value = input;
            InitParams.BufferSize.Value = (int)Utils.UpperPow2((uint)bufferSize);
            InitParams.BlockSize.Value = blockSize;
            InitParams.Type.Value = type;

            Update();

            if (_updated > 0)
            {
                _spreadBuilder.Clear();
                _spreadBuilder.AddRangeArray(_outBuffer, InitParams.BufferSize.Value, 0, false);
                _updated--;
            }

            return _spreadBuilder.ToSpread();
        }

        public override Task<bool> Init()
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

            return Task.FromResult(true);
        }

        public override bool Play()
        {
            _inputBufferLink = InputBufferBlock.LinkTo(_batchBlock);
            return true;
        }

        public override bool Stop()
        {
            _inputBufferLink.Dispose();
            return true;
        }

        public override Task<bool> Free()
        {
            if (_processor == null)
            {
                _logger.Error("ActionBlock == null!");
            }

            if (_link1 == null)
            {
                _logger.Error("Link == null!");
            }

            _link1?.Dispose();
            _link1 = null;
            _processor?.Complete();
            ArrayPool<float>.Shared.Return(_outBuffer);
            ArrayPool<float>.Shared.Return(_tempBuffer);
            _outBuffer = null;
            return _processor?.Completion.ContinueWith(t =>
            {
                _processor = null;
                _logger.Information("ActionBlock stopped, status={status}", t.Status);
                return true;
            });
        }

        public override string DebugInfo()
        {
            return $"[updates={_updated}, input samples={PlayParams.Input.Value?.Format.BufferSize}, batchSize={_batchSize}, output size={InitParams.BufferSize.Value}]";
        }

        private int CreateTypeSkip()
        {
            var bufferSize = InitParams.BufferSize.Value;
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
            return skipSize*bufferSize/inputBufferSize;
        }
        private int CreateTypeSkipHalf()
        {
            var bufferSize = InitParams.BufferSize.Value;
            var skipSize = InitParams.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                var k = 0;
                for (int i = 0, j = 0; i < bufferSize; i++, j ++)
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
            return skipSize*bufferSize/inputBufferSize;
        }

        private int CreateTypeMax()
        {
            var bufferSize = InitParams.BufferSize.Value;
            var blockSize = InitParams.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                var count = blockSize * bufferSize;
                var skip = count/bufferSize;
                var k = 0;
                var j = 0;
                for (int i = 0; i < bufferSize; i++)
                {
                    var min = 0.0f;
                    var max = 0.0f;
                    for (int b = 0; b < skip; b++)
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
            return blockSize * bufferSize/inputBufferSize;
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