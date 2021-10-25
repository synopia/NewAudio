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
        private InputDevice _device;

        public WaveInput()
        {
            AudioCore.Instance.AddInput(this);
        }

        public void Update(out AudioFormat format, out int overflows, out int underruns, out int bufferedSamples)
        {
            overflows = _device.Buffer.Overflows;
            underruns = _device.Buffer.UnderRuns;
            bufferedSamples = _device.Buffer.BufferedSamples;
            format = Output.Format;
        }

        public void ChangeSettings(WaveInputDevice device, AudioSampleRate sampleRate = AudioSampleRate.Hz44100,
            int channelOffset = 0, int channels = 2, int bufferSize = 256, int blockCount = 64)
        {
            Stop();
            if (device == null)
            {
                return;
            }

            _device = AudioCore.Instance.Devices.GetInputDevice(device);
            Output.Format = _device.OutputBuffer.Format;
            Output.SourceBlock = _device.OutputBuffer;

            _logger.Info(
                $"Ready");

        }

        public void Stop()
        {
            _logger.Info("Stopping WaveIn...");
            _device?.Stop();
        }

        public override void Dispose()
        {
            Stop();
            AudioCore.Instance.RemoveInput(this);
            base.Dispose();
        }
    }
}