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
        public OutputDevice Device => _device;
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

        public WaveOutput()
        {
            AudioCore.Instance.AudioGraph.AddSink(this);
        }

        public void Update(out AudioFormat format, out int overflows, out int underruns, out float latency)
        {
            overflows = _device?.Overflows ?? 0;
            underruns = _device?.UnderRuns ?? 0;
            latency = _device?.Latency ?? 0;
            format = Input?.Format ?? default;
        }

        public void ChangeDevice(WaveOutputDevice device)
        {
            var wasPlaying = IsPlaying;
            if (_device != null)
            {
                if (Input != null)
                {
                    _device.RemoveAudioLink(Input);
                }
                AudioCore.Instance.Devices.ReleaseOutputDevice(_device);
            }

            if (device != null)
            {
                _device = AudioCore.Instance.Devices.GetOutputDevice(device);
                if (Input != null)
                {
                    _device.AddAudioLink(Input);
                }
                _logger.Info($"Changed device to {device.Value}");
                IsPlaying = true;

            }
            else
            {
                _device = null;
            }
        }

        public override void Connect(AudioLink input)
        {
            if (Input == input)
            {
                return;
            }

            if (_device != null)
            {
                if (Input != null)
                {
                    _device.RemoveAudioLink(Input);
                }

                if (input != null)
                {
                    _device.AddAudioLink(input);
                }
            }
            base.Connect(input);
        }

        public void ChangeSettings(AudioLink input, int latency = 150)
        {
            Connect(input);
            _logger.Info($"Changed settings");
        }

        public override void Dispose()
        {
            IsPlaying = false;
            AudioCore.Instance.AudioGraph.RemoveSink(this);
            base.Dispose();
        }
    }
}