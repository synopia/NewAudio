using System;
using System.Buffers;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using static NewAudio.Dsp.AudioMath;
using VL.Lib.Collections;

namespace NewAudio.Nodes
{
    public enum AudioBufferOutType
    {
        Skip,
        SkipHalf,
        Max
    }

    public class AudioBufferOutParams : AudioParams
    {
        public AudioParam<int> OutputSize;
        public AudioParam<int> BlockSize;
        public AudioParam<AudioBufferOutType> Type;
    }

    public class AudioBufferOut : AudioNode
    {
        public override string NodeName => "Buffer Out";
        private float[] _outBuffer;
        private float[] _tempBuffer;
        private BatchBlock<AudioDataMessage> _batchBlock;
        private IDisposable _link1;
        private ActionBlock<AudioDataMessage[]> _processor;
        private SpreadBuilder<float> _spreadBuilder = new();
        public AudioBufferOutParams Params { get; }
        private int _updated;

        public AudioBufferOut()
        {
            InitLogger<AudioBufferOut>();
            Params = AudioParams.Create<AudioBufferOutParams>();
            Logger.Information("AudioBufferOut created");
        }

        public Spread<float> Update(AudioLink input, int outputSize = 1024, int blockSize = 8,
            AudioBufferOutType type = AudioBufferOutType.Max, int bufferSize = 1)
        {
            Params.OutputSize.Value = (int)UpperPow2((uint)outputSize);
            Params.BlockSize.Value = blockSize;
            Params.Type.Value = type;
            PlayParams.Update(input, Params.HasChanged, bufferSize);

            base.Update(Params);

            if (_updated > 0)
            {
                _spreadBuilder.Clear();
                _spreadBuilder.AddRangeArray(_outBuffer, Params.OutputSize.Value, 0, false);
                _updated--;
            }

            return _spreadBuilder.ToSpread();
        }

        public override bool Play()
        {
            if (PlayParams.InputFormat.Value.BufferSize>0 && Params.OutputSize.Value > 0 &&
                  Params.BlockSize.Value > 0)
            {
                var type = Params.Type.Value;
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

                if (_batchSize <= 0)
                {
                    return false;
                }
                _batchBlock = new BatchBlock<AudioDataMessage>(_batchSize);
                _link1 = _batchBlock.LinkTo(_processor);

                TargetBlock = _batchBlock;
                return true;

            }

            return false;
        }

        public override void Stop()
        {
            _link1?.Dispose();
            _link1 = null;
            _processor?.Complete();
            if (_outBuffer != null)
            {
                ArrayPool<float>.Shared.Return(_outBuffer);
            }

            if (_tempBuffer != null)
            {
                ArrayPool<float>.Shared.Return(_tempBuffer);
            }

            _outBuffer = null;
            TargetBlock = null;
             _processor?.Completion.ContinueWith(t =>
            {
                _processor = null;
                Logger.Information("ActionBlock stopped, status={Status}", t.Status);
                return true;
            });
        }

        public override string DebugInfo()
        {
            return $"[{this}, batchSize={_batchSize}, {base.DebugInfo()}]";
        }

        private int CreateTypeSkip()
        {
            var bufferSize = Params.OutputSize.Value;
            var skipSize = Params.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                try
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
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "AudioBufferOut.TypeSkip");
                }
            });
            return skipSize * bufferSize / inputBufferSize;
        }

        private int CreateTypeSkipHalf()
        {
            var bufferSize = Params.OutputSize.Value;
            var skipSize = Params.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;
            if (inputBufferSize == 0)
            {
                Logger.Error("SHOULD NOT HAPPEN!!");
            }
            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                try
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
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "AudioBufferOut.TypeSkipHalf");
                }
            });
            return skipSize * bufferSize / inputBufferSize;
        }

        private int CreateTypeMax()
        {
            var bufferSize = Params.OutputSize.Value;
            var blockSize = Params.BlockSize.Value;
            var inputBufferSize = PlayParams.Input.Value.Format.BufferSize;

            _outBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            _tempBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            _processor = new ActionBlock<AudioDataMessage[]>(input =>
            {
                try
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
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "AudioBufferOut.TypeMax");
                }
            });
            return blockSize * bufferSize / inputBufferSize;
        }

        
        private int _batchSize;
        private bool _disposedValue;
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