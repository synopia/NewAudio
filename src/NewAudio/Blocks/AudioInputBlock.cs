using System;
using System.Buffers;
using System.Threading;
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

        public AudioInputBlock() 
        {
            _audioService = Factory.GetAudioService();
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
                _logger.Warning("Thread != null {Task}", _thread.Name);
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

        private void Stop()
        {
            if (_thread == null)
            {
                _logger.Error("Thread == null");
                return;
            }

            if (_token.IsCancellationRequested)
            {
                _logger.Warning("Already stopping!");
            }

            _cancellationTokenSource.Cancel();
            _thread.Join();
            ArrayPool<float>.Shared.Return(_temp);
            _logger.Information("Audio input reading thread finished (Reading from {Reading} ({Owner}))", _buffer?.Name,
                _buffer?.IsOwnerOfSharedMemory);
        }

        private void Loop()
        {
            try
            {
                _logger.Information("Audio input reading thread started (Reading from {Reading} ({Owner}))",
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
                            _logger.Warning("pos!=buf {Pos} {Buf} {Token}", pos, OutputFormat.BufferSize,
                                _token.IsCancellationRequested);
                        }

                        PostMessages();
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception in AudioInput Loop!");
            }
        }

        private double _dTime;
        private int _sTime;

        private void PostMessages()
        {
            var time = new AudioTime(_sTime, _dTime);
            for (var i = 0; i < _config.Length; i++)
            {
                var targetChannels = _config[i].Channels;
                var message = new AudioDataMessage(OutputFormat.WithChannels(targetChannels),
                    OutputFormat.NumberOfFrames)
                {
                    Time = time
                };
                for (var s = 0; s < OutputFormat.NumberOfFrames; s++)
                {
                    for (var ch = 0; ch < targetChannels; ch++)
                    {
                        message.Data[s * targetChannels + ch] =
                            _temp[s * OutputFormat.NumberOfChannels + _config[i].FirstChannel + ch];
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

                _logger.Verbose("Posted {Samples} ", message.BufferSize);
            }

            _sTime += OutputFormat.NumberOfFrames;
            _dTime += OutputFormat.NumberOfFrames / (double)OutputFormat.SampleRate;
        }

        public string DebugInfo()
        {
            return $"{Buffer?.Name}, thread={_thread?.IsAlive}, sent={_messagesSent}";
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
                    Buffer?.Dispose();
                    _buffer?.Dispose();
                    _thread = null;
                    Buffer = null;
                    _buffer = null;
                    _audioService.Dispose();
                }

                _disposedValue = disposing;
            }
        }
    }
}