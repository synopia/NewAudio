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
            CancellationTokenSource = new CancellationTokenSource();
            var configResponse = new DeviceConfigResponse()
            {
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };


            if (IsOutputDevice && config.IsPlaying)
            {
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer)
                {
                    CancellationToken = CancellationTokenSource.Token
                };
                _waveOut = new WaveOutEvent { DeviceNumber = _handle, DesiredLatency = config.Playing.Latency};
                _waveOut.Init(AudioDataProvider);
                configResponse.PlayingChannels = 2;
                configResponse.DriverPlayingChannels = 2;
                configResponse.PlayingWaveFormat = config.Playing.WaveFormat;
                _waveOut.Play();
            } else if (IsInputDevice && config.IsRecording)
            {
                _waveIn = new WaveInEvent
                {
                    WaveFormat = config.Recording.WaveFormat, DeviceNumber = _handle, BufferMilliseconds = config.Recording.Latency
                };
                _waveIn.DataAvailable += DataAvailable;
                configResponse.RecordingChannels = 2;
                configResponse.DriverRecordingChannels = 2;
                _waveIn.StartRecording();
            }
            return Task.FromResult(configResponse);

        }

        public override bool Free()
        {
            CancellationTokenSource?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
            _waveIn?.Dispose();
            _waveOut?.Dispose();

            return true;
        }

        public override bool Start()
        {
            GenerateSilence = false;
            return true;
        }

        public override bool Stop()
        {
            GenerateSilence = true;
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
                    _waveIn?.Dispose();
                    _waveOut?.Dispose();
                    _waveOut = null;
                    _waveIn = null;
                    
                }

                _disposedValue = disposing;
            }
            base.Dispose(disposing);
        }
    }
}