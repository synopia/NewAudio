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
        byte[] buf = new byte[1];

        public AudioGenerator(WaveFormat format, int latency)
        {
            WaveFormat = format;

            _logger.Info($"Started generating, format: {WaveFormat}");

            _worker = new ActionBlock<int>(count =>
            {
                _logger.Trace($"Received Request for {count} samples");
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
            StopRecording();
            _requestLink?.Dispose();
        }

        public WaveFormat WaveFormat { get; set; }
        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;
    }
    
    public class AudioTestGenerator : IWaveIn
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioTestGenerator");

        private ActionBlock<int> _worker;
        private IDisposable _requestLink;
        byte[] buf = new byte[1];

        public AudioTestGenerator(WaveFormat format, int latency)
        {
            WaveFormat = format;

            _logger.Info($"Started generating, format: {WaveFormat}");

            _worker = new ActionBlock<int>(count =>
            {
                _logger.Trace($"Received Request for {count} samples");
                var bytes = count * 4;
                if (bytes > buf.Length)
                {
                    buf = new byte[bytes];
                }
                var wb = new WaveBuffer(buf);

                for (int i = 0; i < count; i++)
                {
                    wb.FloatBuffer[i] = i;
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
            StopRecording();
            _requestLink?.Dispose();
        }

        public WaveFormat WaveFormat { get; set; }
        public event EventHandler<WaveInEventArgs> DataAvailable;
        public event EventHandler<StoppedEventArgs> RecordingStopped;
    }
    
}