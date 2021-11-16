using System;
using System.Threading;
using NAudio.Wave;
using NewAudio.Internal;
using Serilog;
using SharedMemory;

namespace NewAudio.Core
{
    public sealed class AudioDataProvider : IWaveProvider
    {
        private readonly ILogger _logger;
        private readonly MixBuffers _buffer;
        private bool _firstLoop = true;

        private CancellationToken _token;

        public CancellationToken CancellationToken
        {
            get => _token;
            set
            {
                _firstLoop = true;
                _token = value;
            }
        }

        public AudioDataProvider(ILogger logger, WaveFormat waveFormat, MixBuffers buffer)
        {
            _logger = logger;
            _buffer = buffer;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio output reader thread started");
                    _firstLoop = false;
                }

                _logger.Verbose("IN AUDIO THREAD: {Count}", count / 4);
                // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(count/4/WaveFormat.Channels));

                var pos = 0;
                while (pos < count && !CancellationToken.IsCancellationRequested )
                {
                    var read = _buffer.FillBuffer(buffer, pos + offset, count, CancellationToken);
                    pos += read;
                }
                if (CancellationToken.IsCancellationRequested)
                {
                    _logger.Information("Audio output reader thread finished");
                }

                if (!CancellationToken.IsCancellationRequested && pos != count)
                {
                    _logger.Warning("pos!=count: {Pos}!={Count}", pos, count);
                }

                return pos;
            }
            catch (OperationCanceledException e)
            {
                return 0;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception in AudioDataProvider!");
                return 0;
            }
        }
    }
}