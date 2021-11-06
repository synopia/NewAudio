using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;
using SharedMemory;

namespace NewAudio.Devices
{
    public class WaveDevice : BaseDevice
    {
        private readonly int _handle;
        private readonly ILogger _logger;
        private WaveInEvent _waveIn;
        private WaveOutEvent _waveOut;

        public WaveDevice(string name, bool isInputDevice, int handle)
        {
            _logger = AudioService.Instance.Logger.ForContext<WaveDevice>();
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = !isInputDevice;
            _handle = handle;
        }

        public override Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            var configResponse = new DeviceConfigResponse()
            {
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };


            if (IsOutputDevice && config.IsPlaying)
            {
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer);
                _waveOut = new WaveOutEvent { DeviceNumber = _handle, DesiredLatency = config.Playing.Latency};
                _waveOut?.Init(AudioDataProvider);
                configResponse.PlayingChannels = 2;
                configResponse.DriverPlayingChannels = 2;
                configResponse.PlayingWaveFormat = config.Playing.WaveFormat;
            } else if (IsInputDevice && config.IsRecording)
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = config.Recording.WaveFormat, DeviceNumber = _handle, BufferMilliseconds = config.Recording.Latency
                };
                _waveIn.DataAvailable += DataAvailable;
                configResponse.RecordingChannels = 2;
                configResponse.DriverRecordingChannels = 2;

            }
            return Task.FromResult(configResponse);

        }

        public override Task<bool> Free()
        {
            CancellationTokenSource?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
            _waveOut?.Dispose();

            return Task.FromResult(true);
        }

        public override bool Start()
        {
            CancellationTokenSource = new CancellationTokenSource();
            _waveIn?.StartRecording();
            _waveOut?.Play();
            return true;
        }

        public override bool Stop()
        {
            CancellationTokenSource?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
            return true;
        }

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
            throw new System.NotImplementedException();
        }

        public override string ToString()
        {
            return Name;
        }

        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _waveOut?.Dispose();
                }

                _disposedValue = disposing;
            }
            base.Dispose(disposing);
        }
    }
}