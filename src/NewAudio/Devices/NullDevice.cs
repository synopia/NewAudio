using NAudio.Wave;
using SharedMemory;

namespace NewAudio.Devices
{
    public class NullDevice : BaseDevice
    {
        public NullDevice(string name, bool isInputDevice, bool isOutputDevice)
        {
            Name = name;
            IsInputDevice = isInputDevice;
            IsOutputDevice = isOutputDevice;
        }

        public override void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
        }

        public override void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
        {
        }

        public override void Record()
        {
        }

        public override void Play()
        {
        }

        public override void Stop()
        {
        }

        public override void Dispose()
        {
            base.Dispose();
        }

        public override string ToString()
        {
            return Name;
        }
    }
}