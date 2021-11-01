using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace VL.NewAudio.Core
{
    public class AudioDataProvider : IWaveProvider, IDisposable
    {
        private ILogger _logger;
        private CircularBuffer _buffer;
        public WaveFormat WaveFormat { get; }
        private bool _firstLoop = true;
        
        public AudioDataProvider(WaveFormat waveFormat, CircularBuffer buffer)
        {
            _buffer = buffer;
            WaveFormat = waveFormat;
            _logger = AudioService.Instance.Logger.ForContext<AudioDataProvider>();
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                if (_firstLoop)
                {
                    _logger.Information("Audio output thread started");
                    _firstLoop = false;
                }
                AudioService.Instance.Logger.Verbose("IN AUDIO THREAD: {count}", count);
                // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(count/4/WaveFormat.Channels));

                int pos = 0;
                var token = AudioService.Instance.Lifecycle.GetToken();
                while (pos<count && !token.IsCancellationRequested)
                {
                    var x = _buffer.Read(buffer, pos);
                    pos += x;
                    // if (x == 0)
                    // {
                        // return pos;
                    // }
                }

                return pos;
            }
            catch (Exception e)
            {
                AudioService.Instance.Logger.Error("{e}", e);
                return count;

            }
        }

        public void Dispose()
        {
            
        }
    }
}