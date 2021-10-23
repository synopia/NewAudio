using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    public class BlockingSampleProvider : ISampleProvider, IDisposable
    {
        private readonly Logger _logger = LogFactory.Instance.Create("BlockingSampleProvider");
        private AudioFlowBuffer _input;
        public AudioFormat Format;
        public WaveFormat WaveFormat => Format.WaveFormat;

        public BlockingSampleProvider(AudioFormat format, AudioFlowBuffer input)
        {
            Format = format;
            _input = input;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            AudioCore.Instance.Requests.SendAsync(count);

            while (_input.Buffer.BufferedSamples<count)
            {
                _input.ReceiveAsync();
            }
            return _input.Buffer.Read(buffer, offset, count);
        }

        public void Dispose()
        {
            
        }
    }
}