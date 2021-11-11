using System;
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
    public readonly struct AudioInputBlockConfig
    {
        public readonly int FirstChannel;
        public readonly int LastChannel;
        public readonly ITargetBlock<AudioDataMessage> TargetBlock;

        public int Channels => LastChannel - FirstChannel;

        public AudioInputBlockConfig(int firstChannel, int lastChannel, ITargetBlock<AudioDataMessage> targetBlock)
        {
            FirstChannel = firstChannel;
            LastChannel = lastChannel;
            TargetBlock = targetBlock;
        }
    }

    public class AudioInputBlock 
    {
        private readonly ILogger _logger;
        private readonly IResourceHandle<AudioService> _audioService;

        private CircularBuffer _buffer;
        public CircularBuffer Buffer { get; private set; }
        public AudioFormat OutputFormat { get; private set; }
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _token;
        private Thread _thread;
        private long _messagesSent;
        private AudioInputBlockConfig[] _config;
        private float[] _temp;

        public AudioInputBlock() : this(Factory.Instance)
        {
        }

        private AudioInputBlock(IFactory api)
        {
            _audioService = api.GetAudioService();
            _logger = _audioService.Resource.GetLogger<AudioInputBlock>();
        }

        public void Create(AudioInputBlockConfig[] config, AudioFormat audioFormat, int nodeCount)
        {
            _config = config;
            OutputFormat = audioFormat;

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

            _temp = ArrayPool<float>.Shared.Rent(OutputFormat.BufferSize);

            _thread = new Thread(Loop)
            {
                Priority = ThreadPriority.AboveNormal,
                IsBackground = true
            };
            _thread.Start();
        }

        private bool Stop()
        {
            if (_thread == null)
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
            ArrayPool<float>.Shared.Return(_temp);
            _logger.Information("Audio input reading thread finished (Reading from {reading} ({owner}))", _buffer?.Name,
                _buffer?.IsOwnerOfSharedMemory);
            return true;
        }

        private void Loop()
        {
            try
            {
                _logger.Information("Audio input reading thread started (Reading from {reading} ({owner}))",
                    _buffer?.Name,
                    _buffer?.IsOwnerOfSharedMemory);
                if (_buffer == null)
                {
                    throw new Exception("Buffer == null !");
                }

                while (!_token.IsCancellationRequested)
                {
                    var pos = 0;

                    while (pos < OutputFormat.BufferSize && !_token.IsCancellationRequested)
                    {
                        var read = _buffer.Read(_temp, pos, 1);
                        pos += read;
                    }

                    if (!_token.IsCancellationRequested)
                    {
                        if (pos != OutputFormat.BufferSize)
                        {
                            _logger.Warning("pos!=buf {p} {b} {t}", pos, OutputFormat.BufferSize,
                                _token.IsCancellationRequested);
                        }

                        PostMessages();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error("{e}", e);
            }
        }

        private double _dTime;
        private int _sTime;
        private void PostMessages()
        {
            var time = new AudioTime(_sTime, _dTime);
            for (int i = 0; i < _config.Length; i++)
            {
                var targetChannels = _config[i].Channels;
                var message = new AudioDataMessage(OutputFormat.WithChannels(targetChannels),
                    OutputFormat.SampleCount)
                {
                    Time = time
                };
                for (int s = 0; s < OutputFormat.SampleCount; s++)
                {
                    for (int ch = 0; ch < targetChannels; ch++)
                    {
                        message.Data[s * targetChannels + ch] =
                            _temp[s * OutputFormat.Channels + _config[i].FirstChannel + ch];
                    }
                }
                var res = _config[i].TargetBlock.Post(message);
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

            _sTime += OutputFormat.SampleCount;
            _dTime += OutputFormat.SampleCount / (double)OutputFormat.SampleRate;
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
/*
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
        }*/
    }
}