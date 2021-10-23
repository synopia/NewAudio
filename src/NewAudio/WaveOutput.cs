using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NewAudio.Internal;

namespace NewAudio
{
    public class WaveOutput : AudioNodeSink
    {
        private readonly Logger _logger = LogFactory.Instance.Create("WaveOutput");

        private AudioFlowBuffer _buffer;

        private IWavePlayer _waveOut;
        private IDisposable _link;
        private CancellationTokenSource _cancellation;

        public WaveOutput()
        {
            AudioCore.Instance.AddSink(this);
        }

        public void Update(out int overflows, out int underruns, out int bufferedSamples, out int audioBufferCache)
        {
            overflows = _buffer.Buffer.Overflows;
            underruns = _buffer.Buffer.UnderRuns;
            audioBufferCache = _buffer.CachedBuffers;
            bufferedSamples = _buffer.Buffer.BufferedSamples;
        }

        public void ChangeSettings(WaveOutputDevice device, AudioLink input, int driverLatency = 200)
        {
            _logger.Info(
                $"WaveOutput: Configuration changed, Device: {device?.Value}, Input connected: {input != null}, Driver latency: {driverLatency}");

            Stop();
            if (device == null || input == null)
            {
                return;
            }

            Connect(input);
            _buffer = new AudioFlowBuffer(input.Format.BufferSize, input.Format.BlockCount);
            _buffer.Buffer.WaveFormat = input.WaveFormat;
            _link = input.SourceBlock.LinkTo(_buffer);

            try
            {
                _waveOut = ((IWaveOutputFactory)device.Tag).Create(driverLatency);
                _cancellation = new CancellationTokenSource();
                var wave16 = new SampleToWaveProvider16(new BlockingSampleProvider(input.Format, _buffer));
                var asio = (AsioOut)_waveOut;
                asio.InitRecordAndPlayback(wave16, 0, 0);//new SampleToWaveProvider(_buffer.Buffer), 0, 0);
                asio.Play();
                _logger.Info($"{asio.FramesPerBuffer}");
                // _waveOut.Init(_buffer.Buffer);//wave16);
                // _waveOut.Play();
                // _logger.Info(
                    // $"WaveOutput: Started, input format {input.Format} output format: {wave16.WaveFormat}");
            }
            catch (Exception e)
            {
                _logger.Error(e);
                Stop();
            }
        }

        public void Stop()
        {
            _logger.Info("Stopping WaveOut...");
            _cancellation?.Cancel();
            _waveOut?.Stop();
            _link?.Dispose();
            _buffer?.Dispose();
        }

        public override void Dispose()
        {
            Stop();
            AudioCore.Instance.RemoveSink(this);
            _link?.Dispose();
            _waveOut?.Dispose();
            _buffer.Dispose();
            base.Dispose();
        }
    }
}