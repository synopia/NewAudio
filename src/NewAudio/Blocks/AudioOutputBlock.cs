using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudio.Blocks
{
    public struct AudioOutputBlockConfig
    {
        public int FirstChannel;
        public int LastChannel;
        public ISourceBlock<AudioDataMessage> SourceBlock;

        public int Channels => LastChannel - FirstChannel;

        public AudioOutputBlockConfig(int firstChannel, int lastChannel, ISourceBlock<AudioDataMessage> sourceBlock)
        {
            FirstChannel = firstChannel;
            LastChannel = lastChannel;
            SourceBlock = sourceBlock;
        }
    }

    public sealed class AudioOutputBlock : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IResourceHandle<AudioService> _audioService;

        private ActionBlock<AudioDataMessage>[] _actionBlocks;
        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }

        public AudioFormat InputFormat { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;

        private bool _firstLoop;
        private long _messagesReceived;
        private AudioOutputBlockConfig[] _config;
        private float[] _temp;

        public AudioOutputBlock() 
        {
            _audioService = Factory.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioOutputBlock>();
        }

        public void Create(AudioOutputBlockConfig[] config, AudioFormat audioFormat, int nodeCount)
        {
            _config = config;
            InputFormat = audioFormat;

            var name = $"Output Buffer {_audioService.Resource.GetNextId()}";
            _buffer = new CircularBuffer(name, nodeCount, 4 * InputFormat.BufferSize);
            Buffer = new CircularBuffer(name);

            Start();
        }

        private void Start()
        {
            if (_actionBlocks != null)
            {
                _logger.Warning("ActionBlock != null!");
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _token = _cancellationTokenSource.Token;
            _firstLoop = true;
            _temp = ArrayPool<float>.Shared.Rent(InputFormat.BufferSize);

            _actionBlocks = new ActionBlock<AudioDataMessage>[_config.Length];
            _receiveStatus = new bool[_config.Length];
            for (var i = 0; i < _config.Length; i++)
            {
                var config = _config[i];
                _actionBlocks[i] = CreateActionBlock(i);
                config.SourceBlock.LinkTo(_actionBlocks[i]);
            }
        }

        private void Stop()
        {
            if (_actionBlocks == null)
            {
                _logger.Error("ActionBlock == null!");
                return;
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }

            _cancellationTokenSource.Cancel();
            var tasks = _actionBlocks.Select(b => b.Completion).ToArray();
            ArrayPool<float>.Shared.Return(_temp);

            if (tasks.Any(t => t.Status == TaskStatus.Running))
            {
                Task.WaitAll(tasks);
                _logger.Information("ActionBlock stopped");
                _actionBlocks = null;
            }

            _logger.Information("Audio Output writer finished (Writing to {Writer} ({Owner}))", _buffer?.Name,
                _buffer?.IsOwnerOfSharedMemory);
        }

        private ActionBlock<AudioDataMessage> CreateActionBlock(int index)
        {
            return new ActionBlock<AudioDataMessage>(message =>
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio Output writer started (Writing to {Writer} ({Owner}))", _buffer.Name,
                        _buffer.IsOwnerOfSharedMemory);
                    _firstLoop = false;
                }

                _logger.Verbose("Writing data to Main Buffer Out {Message} {Size}", message.Data?.Length,
                    message.BufferSize);

                var pos = 0;

                var result = WriteMessage(message, index);
                if (result)
                {
                    while (pos < InputFormat.BufferSize && !_token.IsCancellationRequested)
                    {
                        var v = _buffer.Write(_temp, pos, 1);
                        pos += v;
                    }

                    for (var i = 0; i < _config.Length; i++)
                    {
                        _receiveStatus[i] = false;
                    }

                    if (!_token.IsCancellationRequested && pos != message.BufferSize)
                    {
                        _logger.Warning("pos!=msg {Pos}!={Msg}", pos, InputFormat.BufferSize);
                    }
                    else
                    {
                        _messagesReceived++;
                    }
                }
            }, new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1,
                BoundedCapacity = 1,
                CancellationToken = _token
            });
        }

        private double _dTime;
        private int _sTime;
        private bool[] _receiveStatus;

        private bool WriteMessage(AudioDataMessage message, int index)
        {
            var targetChannels = _config[index].Channels;

            for (var s = 0; s < InputFormat.NumberOfFrames; s++)
            {
                for (var ch = 0; ch < targetChannels; ch++)
                {
                    _temp[s * InputFormat.NumberOfChannels + _config[index].FirstChannel + ch] =
                        message.Data[s * targetChannels + ch];
                }
            }

            ArrayPool<float>.Shared.Return(message.Data);

            _receiveStatus[index] = true;
            return _receiveStatus.None(b => !b);
        }

        public string DebugInfo()
        {
            return $"{_buffer?.Name}, recv={_messagesReceived}";
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Stop();
                    Buffer.Dispose();
                    _buffer.Dispose();
                    _audioService.Dispose();
                }

                _disposedValue = disposing;
            }
        }
    }
}