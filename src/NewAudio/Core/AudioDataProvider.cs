using System;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Core
{
    public class AudioDataProvider : IWaveProvider, IDisposable
    {
        private readonly CircularBuffer _buffer;
        private readonly ILogger _logger;
        private bool _firstLoop = true;
        public CancellationToken CancellationToken { get; set; }

        public AudioDataProvider(WaveFormat waveFormat, CircularBuffer buffer)
        {
            _buffer = buffer;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, waveFormat.Channels);
            _logger = AudioService.Instance.Logger.ForContext<AudioDataProvider>();
        }

        public void Dispose()
        {
        }

        public WaveFormat WaveFormat { get; }

        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio output thread started (Reading from {read} ({owner}))", _buffer.Name,
                        _buffer.IsOwnerOfSharedMemory);
                    _firstLoop = false;
                }

                AudioService.Instance.Logger.Verbose("IN AUDIO THREAD: {count}", count / 4);
                // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(count/4/WaveFormat.Channels));

                var pos = offset;
                while (pos < count && !CancellationToken.IsCancellationRequested)
                {
                    var x = _buffer.Read(buffer, pos, 1);
                    pos += x;
                }

                if (!CancellationToken.IsCancellationRequested && pos != count)
                {
                    _logger.Warning("pos!=count: {p}!={c}", pos, count);
                }

                return pos;
            }
            catch (Exception e)
            {
                AudioService.Instance.Logger.Error("{e}", e);
                return count;
            }
        }
    }
}