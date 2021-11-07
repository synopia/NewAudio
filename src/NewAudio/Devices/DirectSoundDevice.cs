using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Devices
{
    public class DirectSoundDevice : BaseDevice
    {
        private DirectSoundOut _directSoundOut;

        private readonly Guid _guid;
        private readonly ILogger _logger;

        public DirectSoundDevice(string name, Guid guid)
        {
            _logger = AudioService.Instance.Logger.ForContext<DirectSoundDevice>();
            Name = name;
            _guid = guid;
            IsOutputDevice = true;
            IsInputDevice = false;
        }

        public override Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            var configResponse = new DeviceConfigResponse()
            {
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };
            if (config.IsPlaying && config.IsPlaying)
            {
                
                AudioDataProvider = new AudioDataProvider(config.Playing.WaveFormat, config.Playing.Buffer);
                CancellationTokenSource = new CancellationTokenSource();
                AudioDataProvider.CancellationToken = CancellationTokenSource.Token;
                _directSoundOut = new DirectSoundOut(_guid, config.Playing.Latency);
                _directSoundOut?.Init(AudioDataProvider);
                configResponse.PlayingChannels = 2;
                configResponse.DriverPlayingChannels = 2;
                configResponse.PlayingWaveFormat = config.Playing.WaveFormat;
                _logger.Information("DirectSound Out initialized.. ");
            }
            _directSoundOut?.Play();
            return Task.FromResult(configResponse);
        }

        public override bool Free()
        {
            CancellationTokenSource?.Cancel();
            _directSoundOut?.Stop();
            _directSoundOut?.Dispose();
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

        private bool _disposedValue;
        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _directSoundOut?.Dispose();
                    _directSoundOut = null;
                }

                _disposedValue = disposing;
            }
            base.Dispose(disposing);
        }

        public override string ToString()
        {
            return Name;
        }
    }
}