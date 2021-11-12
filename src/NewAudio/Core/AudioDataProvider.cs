using System;
using System.Threading;
using NAudio.Wave;
using Serilog;
using SharedMemory;

namespace NewAudio.Core
{
    public sealed class AudioDataProvider : IWaveProvider
    {
        private readonly ILogger _logger;
        private readonly CircularBuffer _buffer;
        private bool _firstLoop = true;
        public bool GenerateSilence { get; set; }


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

        public AudioDataProvider(ILogger logger, WaveFormat waveFormat, CircularBuffer buffer)
        {
            _logger = logger;
            _buffer = buffer;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
            GenerateSilence = true;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio output reader thread started (Reading from {Read} ({Owner}))",
                        _buffer.Name,
                        _buffer.IsOwnerOfSharedMemory);
                    _firstLoop = false;
                }

                _logger.Verbose("IN AUDIO THREAD: {Count}", count / 4);
                // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(count/4/WaveFormat.Channels));

                var pos = 0;
                if (!GenerateSilence)
                {
                    while (pos < count && !CancellationToken.IsCancellationRequested && !GenerateSilence)
                    {
                        var x = _buffer.Read(buffer, pos + offset, 1);
                        pos += x;
                    }
                }

                if (GenerateSilence)
                {
                    Array.Clear(buffer, pos + offset, count - pos);
                    pos += count - pos;
                }
                else
                {
                    if (CancellationToken.IsCancellationRequested)
                    {
                        _logger.Information("Audio output reader thread finished");
                    }
                }

                if (!CancellationToken.IsCancellationRequested && pos != count)
                {
                    _logger.Warning("pos!=count: {Pos}!={Count}", pos, count);
                }

                return pos;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Exception in AudioDataProvider!");
                return count;
            }
        }
    }
}