using System;
using System.Linq;
using System.Threading;
using VL.NewAudio.Dispatcher;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Device
{
    public interface IAudioControl : IChangeBroadcaster, IDisposable
    {
        // void OpenAudioDeviceSetup(DeviceConfig[] setup);
        // void OpenAudioDeviceConfig(DeviceConfig config);

        void AddAudioCallback(IAudioDeviceCallback callback);
        void RemoveAudioCallback(IAudioDeviceCallback callback);

        double GetCpuUsage();

        object AudioProcessLock { get; }

        void PlayTestSound(int index);

        int TotalInputChannels { get; }
        int TotalOutputChannels { get; }
        bool IsRunning { get; }

        IAudioSession? Open(AudioStreamBuilder builder);

        void Close();
    }

    public class CallbackHandler : IAudioDeviceCallback
    {
        private IAudioDeviceCallback _owner;

        public CallbackHandler(IAudioDeviceCallback owner)
        {
            _owner = owner;
        }

        public void AudioDeviceCallback(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            _owner.AudioDeviceCallback(input, output, numFrames);
        }

        public void AudioDeviceAboutToStart(IAudioSession session)
        {
            _owner.AudioDeviceAboutToStart(session);
        }

        public void AudioDeviceStopped()
        {
            _owner.AudioDeviceStopped();
        }

        public void AudioDeviceError(string errorMessage)
        {
            _owner.AudioDeviceError(errorMessage);
        }
    }
}