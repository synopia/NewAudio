using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using SilenceProvider = NewAudio.Internal.SilenceProvider;

namespace NewAudio
{
    public class AudioGenerator : IWaveIn
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioGenerator");

        private ActionBlock<int> _worker;
        private IDisposable _requestLink;

        public AudioGenerator(WaveFormat format, int latency)
        {
            WaveFormat = format;

            _logger.Info($"Started generating, format: {WaveFormat}");

            byte[] buf = new byte[1];
            _worker = new ActionBlock<int>(count =>
            {
                var bytes = count * 4;
                if (bytes > buf.Length)
                {
                    buf = new byte[bytes];
                }

                if (DataAvailable != null)
                {
                    DataAvailable(this, new WaveInEventArgs(buf, bytes));
                }
            });
        }

        public void StartRecording()
        {
            _requestLink = AudioCore.Instance.Requests.LinkTo(_worker);
        }

        public void StopRecording()
        {
            _requestLink?.Dispose();
        }

        public void Dispose()
        {
            _requestLink?.Dispose();
        }

        public WaveFormat WaveFormat { get; set; }
        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;
    }
}