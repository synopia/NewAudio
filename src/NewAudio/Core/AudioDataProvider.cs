using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using SharedMemory;

namespace VL.NewAudio.Core
{
    public class AudioDataProvider : IWaveProvider, IDisposable
    {
        private CircularBuffer _buffer;
        public WaveFormat WaveFormat { get; }

        private PlayPauseStop _playPauseStop;
        public AudioDataProvider(WaveFormat waveFormat, CircularBuffer buffer, PlayPauseStop playPauseStop)
        {
            _buffer = buffer;
            WaveFormat = waveFormat;
            _playPauseStop = playPauseStop;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                AudioService.Instance.Logger.Verbose("IN AUDIO THREAD: {count}", count);
                // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(count/4/WaveFormat.Channels));

                int pos = 0;
                var token = _playPauseStop.GetToken();
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