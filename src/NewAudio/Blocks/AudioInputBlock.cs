﻿using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudio.Blocks
{
    
    public class AudioInputBlock : ISourceBlock<AudioDataMessage>
    {
        private readonly ILogger _logger;
        private readonly IResourceHandle<AudioService> _audioService;
        private ITargetBlock<AudioDataMessage> _outputBlock;

        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Thread _thread;
        private long _messagesSent;

        public AudioInputBlock(): this(VLApi.Instance){}

        private AudioInputBlock(IVLApi api)
        {
            _audioService = api.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioInputBlock>();
        }

        public void Create(ITargetBlock<AudioDataMessage> outputBlock, AudioFormat audioFormat, int nodeCount)
        {
            OutputFormat = audioFormat;
            _outputBlock = outputBlock;
            
            var name = $"Input Block {_audioService.Resource.GetNextId()}";
            Buffer = new CircularBuffer(name, nodeCount, 4 * audioFormat.BufferSize);
            _buffer = new CircularBuffer(name);
            
            Start();
        }

        private void Start()
        {
            if (_thread != null)
            {
                _logger.Warning("Thread != null {task}", _thread);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            _token = _cancellationTokenSource.Token;
            
            _thread = new Thread(Loop)
            {
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            _thread.Start();
        }

        private bool Stop()
        {
            if (_thread== null)
            {
                _logger.Error("Thread == null {task}", _thread);
                return false;
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }
            _logger.Warning("STOP IN READING");
            _cancellationTokenSource.Cancel();
            _thread.Join();
            _logger.Information("Audio input reading thread finished (Reading from {reading} ({owner}))", _buffer?.Name,
                _buffer?.IsOwnerOfSharedMemory);
            return true;
        }

        private void Loop()
        {
            try
            {
                _logger.Information("Audio input reading thread started (Reading from {reading} ({owner}))", _buffer?.Name,
                    _buffer?.IsOwnerOfSharedMemory);
                if (_buffer == null)
                {
                    throw new Exception("Buffer == null !");
                }

                while (!_token.IsCancellationRequested)
                {
                    var message = new AudioDataMessage(OutputFormat, OutputFormat.SampleCount);
                    var pos = 0;

                    while (pos < OutputFormat.BufferSize && !_token.IsCancellationRequested)
                    {
                        var read = _buffer.Read(message.Data, pos, 1);
                        pos += read;
                    }

                    if (!_token.IsCancellationRequested)
                    {
                        if (pos != OutputFormat.BufferSize)
                        {
                            _logger.Warning("pos!=buf {p} {b} {t}", pos, OutputFormat.BufferSize,
                                _token.IsCancellationRequested);
                        }
                        var res = _outputBlock.Post(message);
                        if (!res)
                        {
                            ArrayPool<float>.Shared.Return(message.Data);
                        }
                        else
                        {
                            _messagesSent++;
                        }
                        _logger.Verbose("Posted {samples} ", message.BufferSize);
                    }

                }
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }

        }

        public string DebugInfo()
        {
            return $"{Buffer?.Name}, thread={_thread?.IsAlive}, sent={_messagesSent}";
        }
        
        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            _logger.Information("Dispose called for InputBlock {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _logger.Warning("WAIT FOR IN READING");
                    Stop();
                    Buffer.Dispose();
                    _thread = null;
                    Buffer = null;
                    _audioService.Dispose();
                }

                _disposedValue = disposing;
            }
        }

        public void Complete()
        {
            Stop();
        }

        public void Fault(Exception exception)
        {
            _logger.Error("{e}", exception);
            _thread.Abort();
        }

        public Task Completion => Task.Run(() => _thread.Join());

        public IDisposable LinkTo(ITargetBlock<AudioDataMessage> target, DataflowLinkOptions linkOptions)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).LinkTo(target, linkOptions);
        }

        public AudioDataMessage ConsumeMessage(DataflowMessageHeader messageHeader,
            ITargetBlock<AudioDataMessage> target, out bool messageConsumed)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).ConsumeMessage(messageHeader, target,
                out messageConsumed);
        }

        public bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            return ((ISourceBlock<AudioDataMessage>)_outputBlock).ReserveMessage(messageHeader, target);
        }

        public void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<AudioDataMessage> target)
        {
            ((ISourceBlock<AudioDataMessage>)_outputBlock).ReleaseReservation(messageHeader, target);
        }
    }
}