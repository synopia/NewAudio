using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    public class BlockingSampleProvider : ISampleProvider, IDisposable
    {
        private readonly Logger _logger = LogFactory.Instance.Create("BlockingSampleProvider");
        private BufferedSampleProvider _input;
        public AudioFormat Format;
        public WaveFormat WaveFormat => Format.WaveFormat;
        private CancellationTokenSource _cancellation;
        private bool warmup = true;

        public BlockingSampleProvider(AudioFormat format, BufferedSampleProvider input, CancellationTokenSource cancel=null)
        {
            Format = format;
            _input = input;
            _cancellation = cancel;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (warmup)
            {
                if (_input.BufferedSamples >= Format.BufferSize * Format.BlockCount / 2)
                {
                    warmup = false;
                }

                Array.Clear(buffer, offset, count);
                return count;
            }
            if (count > Format.BufferSize * Format.BlockCount)
            {
                _logger.Warn($"Requested to much data.. {count} < {Format.BufferSize * Format.BlockCount}");
                return _input.Read(buffer, offset, Format.BufferSize * Format.BlockCount);
            }
            var counter = 0;
            try
            {
                var token = _cancellation.Token;
                while (_input.BufferedSamples < count && !token.IsCancellationRequested)
                {
                    Task.Delay(1, token);
                    counter++;
                }
            }
            catch (TaskCanceledException e)
            {
                _logger.Error(e);
            }

            if (_cancellation.IsCancellationRequested)
            {
                _logger.Warn($"CANCELED {_input.BufferedSamples} {count} {counter}");
                return count;
            }
            return _input.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            _cancellation?.Cancel();
        }
    }
}