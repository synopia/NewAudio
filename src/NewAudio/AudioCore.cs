using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;

namespace NewAudio
{
    public enum AudioSampleRate
    {
        Hz8000 = 8000,
        Hz11025 = 11025,
        Hz16000 = 16000,
        Hz22050 = 22050,
        Hz32000 = 32000,
        Hz44056 = 44056,
        Hz44100 = 44100,
        Hz48000 = 48000,
        Hz88200 = 88200,
        Hz96000 = 96000,
        Hz176400 = 176400,
        Hz192000 = 192000,
        Hz352800 = 352800
    }

    public class AudioCore
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioEngine");
        private static AudioCore _instance;

        public static AudioCore Instance => _instance ??= new AudioCore();

        public readonly AudioBufferFactory BufferFactory = new AudioBufferFactory();
        public readonly DeviceManager Devices = new DeviceManager();
        
        public readonly BroadcastBlock<int> Requests;
        public readonly AudioGraph AudioGraph = new AudioGraph();
        
        public AudioCore()
        {
            Requests = new BroadcastBlock<int>(i=>
            {
                _logger.Trace($"CC Received Request for {i} samples");
                return i;
            });
        }

        public void Restart()
        {
            Devices.Dispose();
            AudioGraph.Dispose();
            BufferFactory.Clear();
        }

        public void Init()
        {
            _logger.Info($"AudioEngine started links: {AudioGraph}");
        }

        public bool IsPlaying { get; private set; }
        public void ChangeSettings(bool playing = false)
        {
            if (IsPlaying == playing)
            {
                return;
            }

            IsPlaying = playing;
            if (IsPlaying)
            {
                Devices.Start();
            }
            else
            {
                Devices.Stop();
            }
        }
        
        public void Update(out int bufferCacheSize, bool reset = false)
        {
            if (reset)
            {
                BufferFactory.Clear();
            }

            bufferCacheSize = BufferFactory.Count;
        }

    }
}