using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Stride.Core;

namespace NewAudio
{
    public class WaveInput : AudioNodeInput
    {
        private readonly Logger _logger = LogFactory.Instance.Create("WaveInput");
        private readonly AudioBufferFactory _audioBufferFactory = new AudioBufferFactory();

        private AudioFlowBuffer _buffer;

        private readonly BufferBlock<AudioBuffer> _bufferIn =
            new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
                BoundedCapacity = 2,
                MaxMessagesPerTask = 2
            });

        private IWaveIn _waveIn;
        private IDisposable _link;

        public WaveInput()
        {
            AudioCore.Instance.AddInput(this);
        }

        public void Update(out AudioFormat format, out int overflows, out int underruns, out int bufferedSamples)
        {
            overflows = _buffer.Buffer.Overflows;
            underruns = _buffer.Buffer.UnderRuns;
            bufferedSamples = _buffer.Buffer.BufferedSamples;
            format = Output.Format;
        }

        public void ChangeSettings(WaveInputDevice device, AudioSampleRate sampleRate = AudioSampleRate.Hz44100,
            int channelOffset = 0, int channels = 2, int bufferSize = 256, int blockCount = 64, int driverLatency = 200)
        {
            _logger.Info(
                $"WaveInput: Configuration changed, Device: {device?.Value}, SampleRate: {(int)sampleRate}, Channel offset: {channelOffset}, Channels: {channels}, Buffer size: {bufferSize}, Internal buffer size: {bufferSize*blockCount}, Driver latency: {driverLatency}");

            Stop();
            if (device == null)
            {
                return;
            }
    
            var outputFormat = new AudioFormat(channels, (int)sampleRate, bufferSize);
            _buffer = new AudioFlowBuffer(outputFormat, bufferSize*blockCount, channels*bufferSize);
            _audioBufferFactory.Clear();
            var waveFormat = new WaveFormat((int)sampleRate, 16, channels);
            _link = _bufferIn.LinkTo(_buffer);
            Output.SourceBlock = _buffer;
            try
            {
                _waveIn = ((IWaveInputFactory)device.Tag).Create(waveFormat, driverLatency);
                waveFormat = _waveIn.WaveFormat;
                Output.Format = outputFormat.Update(waveFormat);
                _buffer.Buffer.WaveFormat = Output.Format.WaveFormat; 
                _waveIn.DataAvailable += (s, a) =>
                {
                    var b = _audioBufferFactory.FromByteBuffer(waveFormat, a.Buffer, a.BytesRecorded);
                    _bufferIn.Post(b);
                };
                _waveIn.StartRecording();
                _logger.Info(
                    $"WaveInput: Started, input format: {waveFormat} ({_waveIn.WaveFormat.SampleRate}), output format: {Output.Format}");
            }
            catch (Exception e)
            {
                _logger.Error(e);
                Stop();
            }
        }

        public void Stop()
        {
            _logger.Info("Stopping WaveIn...");
            // _bufferIn.Complete();
            _waveIn?.StopRecording();
            _link?.Dispose();
            _buffer?.Dispose();
        }

        public override void Dispose()
        {
            Stop();
            AudioCore.Instance.RemoveInput(this);
            _audioBufferFactory.Dispose();
            base.Dispose();
        }
    }
}