using System;
using System.Linq;
using System.Threading;
using VL.NewAudio.Dispatcher;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using Xt;

namespace VL.NewAudio.Core
{
    public interface IAudioControl : IChangeBroadcaster, IDisposable
    {
        // void OpenAudioDeviceSetup(DeviceConfig[] setup);
        // void OpenAudioDeviceConfig(DeviceConfig config);

        void AddAudioCallback(IAudioCallback callback);
        void RemoveAudioCallback(IAudioCallback callback);

        double GetCpuUsage();

        object AudioProcessLock { get; }

        void PlayTestSound(int index);

        int TotalInputChannels { get; }
        int TotalOutputChannels { get; }
        bool IsRunning { get; }

        IAudioSession? Open(AudioStreamBuilder builder);

        void Close();
    }

    public class CallbackHandler : IAudioCallback
    {
        private IAudioCallback _owner;

        public CallbackHandler(IAudioCallback owner)
        {
            _owner = owner;
        }

        public void OnAudio(AudioBuffer? input, AudioBuffer output, int numFrames)
        {
            using var s = new ScopedMeasure("IAudioControl.OnAudio");

            _owner.OnAudio(input, output, numFrames);
        }

        public void OnAudioWillStart(IAudioSession session)
        {
            _owner.OnAudioWillStart(session);
        }

        public void OnAudioStopped()
        {
            _owner.OnAudioStopped();
        }

        public void OnAudioError(string errorMessage)
        {
            _owner.OnAudioError(errorMessage);
        }
    }
}