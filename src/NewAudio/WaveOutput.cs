using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace NewAudio
{
    public class WaveOutput : AudioNodeSink
    {
        private readonly Logger _logger = LogFactory.Instance.Create("WaveOutput");

        private OutputDevice _device;

        public WaveOutput()
        {
            AudioCore.Instance.AddSink(this);
        }

        public void Update(out int overflows, out int underruns, out int bufferedSamples)
        {
            overflows = _device.Buffer.Overflows;
            underruns = _device.Buffer.UnderRuns;
            bufferedSamples = _device.Buffer.BufferedSamples;
        }

        public void ChangeSettings(WaveOutputDevice device, AudioLink input, int blockCount = 64)
        {

            Stop();
            if (device == null || input == null || blockCount==0)
            {
                return;
            }

            Connect(input);

            _device = AudioCore.Instance.Devices.GetOutputDevice(device);
            _device.AddAudioLink(input);
            _logger.Info($"Ready");
        }

        public void Stop()
        {
            _logger.Info("Stopping WaveOut...");
            _device?.Stop();
        }

        public override void Dispose()
        {
            Stop();
            AudioCore.Instance.RemoveSink(this);
            base.Dispose();
        }
    }
}