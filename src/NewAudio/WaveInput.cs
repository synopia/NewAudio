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
        public InputDevice Device => _device;
        public int Channels { get; private set; }
        public int ChannelOffset { get; private set; }

        public bool IsPlaying
        {
            get => _device?.IsPlaying ?? false;
            set
            {
                if (value)
                {
                    _device?.Start();
                }
                else
                {
                    _device?.Stop();
                }
            }
        }

        public WaveInput()
        {
            AudioCore.Instance.AudioGraph.AddInput(this);
        }

        public void Update(out AudioFormat format, out int overflows, out int underruns, out int bufferedSamples)
        {
            overflows = _device?.Overflows ?? 0;
            underruns = _device?.UnderRuns ?? 0;
            bufferedSamples = _device?.BufferedSamples ?? 0;
            format = Output?.Format ?? default;
        }

        public void ChangeDevice(WaveInputDevice device)
        {
            var wasPlaying = IsPlaying;
            if (_device != null)
            {
                AudioCore.Instance.Devices.ReleaseInputDevice(_device);
            }
            if (device != null)
            {
                _device = AudioCore.Instance.Devices.GetInputDevice(device);
                Output.Format = _device.Format;
                Output.SourceBlock = _device.OutputBuffer;
                _logger.Info($"Changed device to {device.Value}");
                IsPlaying = wasPlaying;
            }
            else
            {
                _device = null;
                Output.Format = default;
                Output.SourceBlock = null;
            }
        }
        public void ChangeSettings(int channelOffset = 0, int channels = 2)
        {
        }

        public override void Dispose()
        {
            IsPlaying = false;
            AudioCore.Instance.AudioGraph.RemoveInput(this);
            base.Dispose();
        }
    }
}