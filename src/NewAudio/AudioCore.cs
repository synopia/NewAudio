using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using VL.Lib.Collections;
using VL.NewAudio;

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

    /*
    [Serializable]
    public class AudioSampleRate : DynamicEnumBase<AudioSampleRate, AudioSampleRateDefinition>
    {
        public AudioSampleRate(string value) : base(value)
        {
        }

        public static AudioSampleRate CreateDefault()
        {
            return new AudioSampleRate("44100");
        }
    }

    public class AudioSampleRateDefinition : DynamicEnumDefinitionBase<AudioSampleRateDefinition>
    {
        protected override IObservable<object> GetEntriesChangedObservable()
        {
            return AudioCore.Instance.DeviceSettingsChanged;
        }

        protected override IReadOnlyDictionary<string, object> GetEntries()
        {
            var sampleRates = new Dictionary<string, object>();
            foreach (var item in Enum.GetValues(typeof(AudioSampleRateEnum)))
            {
                sampleRates.Add(((int)item).ToString(), (int)item);
            }

            return sampleRates;
        }

        protected override bool AutoSortAlphabetically => false;
    }
    */

    /*
    [Serializable]
    public class AudioEngineSettings
    {
        private readonly BehaviorSubject<int> _bufferSize = new BehaviorSubject<int>(512);

        public int BufferSize
        {
            get => _bufferSize.Value;
            set => _bufferSize.OnNext(value);
        }

        public IObservable<int> ObsBufferSize => _bufferSize;

        private readonly BehaviorSubject<AudioSampleRate> _sampleRate =
            new BehaviorSubject<AudioSampleRate>(AudioSampleRate.Hz44100);

        public int SampleRate
        {
            get => (int)_sampleRate.Value;
            set => _sampleRate.OnNext((AudioSampleRate)Enum.Parse(typeof(AudioSampleRate), value.ToString()));
        }

        public IObservable<AudioSampleRate> ObsSampleRate => _sampleRate;
    }
    */

    public class AudioCore
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioEngine");
        // public readonly AudioEngineSettings Settings = new AudioEngineSettings();

        // private readonly BehaviorSubject<string> _settingsChanged =
            // new BehaviorSubject<string>("");

        // public IObservable<string> DeviceSettingsChanged => _settingsChanged;

        private static AudioCore _instance;

        public static AudioCore Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AudioCore();
                }
                return _instance;
            }
        }

        private readonly List<AudioLink> _links = new List<AudioLink>();
        private readonly List<AudioNodeSink> _sinks = new List<AudioNodeSink>();
        private readonly List<AudioNodeInput> _inputs = new List<AudioNodeInput>();
        public readonly BufferBlock<int> Requests = new BufferBlock<int>();
        
        public void Init()
        {
            _logger.Info($"AudioEngine started links: {_links.Count}, sinks: {_sinks.Count}, sources: {_inputs.Count}");
        }

        public void AddAudioLink(AudioLink audioLink)
        {
            if (!_links.Contains(audioLink))
            {
                _links.Add(audioLink);
            }
        }       
        public void RemoveAudioLink(AudioLink audioLink)
        {
            _links.Remove(audioLink);
        }
        public void AddSink(AudioNodeSink sink)
        {
            if (!_sinks.Contains(sink))
            {
                _sinks.Add(sink);
            }
        }       
        public void RemoveSink(AudioNodeSink sink)
        {
            _sinks.Remove(sink);
        }
        public void AddInput(AudioNodeInput source)
        {
            if (!_inputs.Contains(source))
            {
                _inputs.Add(source);
            }
        }       
        public void RemoveInput(AudioNodeInput source)
        {
            _inputs.Remove(source);
        }

        public AudioLink CreateStream(ISampleProvider provider)
        {
            return new AudioLink()
            {
                // Format = AudioFormat.Default().Update(provider.WaveFormat),
                // FillBuffer = provider.Read
            };
        }

        private Task _currentTask;
        private CancellationTokenSource _cancellationToken;

        public void Update(bool reset=false)
        {
            if (reset)
            {
                // _cancellationToken.Cancel();
                // _currentTask = null;
            }
            
            if (_currentTask == null )
            {
                // _cancellationToken = new CancellationTokenSource();
                // CancellationToken token = _cancellationToken.Token;

                // _currentTask = Task.Run((Action)(()=>
                // {
                    // _logger.Info($"Started Worker Thread {Settings.BufferSize} {Settings.SampleRate} buffers: {_audioSamples.Count} sinks: {_sinks.Count} outputs: {_sources.Count}");
                    
                    // while (!token.IsCancellationRequested)
                    // {
                        // Read(0, Settings.BufferSize);
                    // }
                // }), token);
            }
        }
        
        private int Read(int offset, int count)
        {
            foreach (var sample in _links)
            {
            }

            // foreach (var output in _sources)
            // {
                // output.Read(offset, count);
            // }

            float[] buffer = new float[count];
            var total = 0;
            foreach (var sink in _sinks)
            {
                // total = sink.Read(buffer, offset, count);
            }

            foreach (var sample in _links)
            {
            }

            return total;
        }
        
        /*
        public AudioBuffer CreateBuffer(AudioSamples input)
        {
            _logger.Info($"Creating audio buffer {input.WaveFormat}");
            var audioBuffer = new AudioBuffer(input, Settings.BufferSize);
            _audioBuffers.Add(audioBuffer.GetHashCode().ToString(), audioBuffer);
            return audioBuffer;
        }

        public void ReleaseBuffer(AudioBuffer buffer)
        {
            _audioBuffers.Remove(buffer.GetHashCode().ToString());
            buffer.Dispose();
        }
        */

        /*
        public void ChangeSettings(AudioSampleRate sampleRate = null, int bufferSize = 512)
        {
            if (sampleRate == null)
            {
                sampleRate = AudioSampleRate.CreateDefault();
            }
            Settings.SampleRate = (int)sampleRate.Tag;
            Settings.BufferSize = bufferSize;

            foreach (var audioBuffer in _links)
            {
                // audioBuffer.BufferSize = bufferSize;
            }
            
        }
    */
    }

    public static class AudioService
    {
        public static AudioCore AudioEngine => AudioCore.Instance;
    }
}