using System.Threading;
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

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            _logger.Information("Init: Format={format}", waveFormat);
            AudioDataProvider = new AudioDataProvider(waveFormat, buffer);

            if (IsOutputDevice)
            {
                _waveOut = new WaveOutEvent { DeviceNumber = _handle, DesiredLatency = desiredLatency };
                _waveOut?.Init(AudioDataProvider);
            }
        }

        private void DataAvailable(object sender, WaveInEventArgs evt)
        {
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
            if (IsInputDevice)
            {
                _waveIn = new WaveInEvent
                {
                    /*WaveFormat = format,*/ DeviceNumber = _handle, BufferMilliseconds = desiredLatency
                };
                _waveIn.DataAvailable += DataAvailable;
            }
        }

        public override void Record()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _waveIn?.StartRecording();
        }

        public override void Play()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            AudioDataProvider.CancellationToken = _cancellationTokenSource.Token;
            _waveOut?.Play();
        }

        public override void Stop()
        {
            _cancellationTokenSource?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
        }

        public override string ToString()
        {
            return Name;
        }

        public override void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _waveIn?.StopRecording();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            base.Dispose();
        }
    }
}