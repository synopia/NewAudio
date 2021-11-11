using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public abstract class BaseDevice : IDevice
    {
        protected ILogger Logger;
        private readonly IResourceHandle<AudioService> _audioService;

        public AudioDataProvider AudioDataProvider { get; protected set; }
        public AudioInputBlock AudioInputBlock  { get; protected set; }
        public  AudioOutputBlock AudioOutputBlock { get; protected set; }
        
        public CircularBuffer RecordingBuffer { get; private set; }
        public CircularBuffer PlayingBuffer { get; private set; }

        protected DeviceConfigResponse RecordingConfig;
        protected DeviceConfigResponse PlayingConfig;
        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }

        public BroadcastBlock<AudioDataMessage> InputBufferBlock = new BroadcastBlock<AudioDataMessage>(i => i);
            // new ExecutionDataflowBlockOptions()
            // {
                // BoundedCapacity = 2,
                // SingleProducerConstrained = true,
                // MaxDegreeOfParallelism = 1
            // });
        protected CancellationTokenSource CancellationTokenSource;

        private bool _generateSilence;
        public bool GenerateSilence
        {
            get => _generateSilence;
            set
            {
                _generateSilence = value;
                if (AudioDataProvider != null)
                {
                    AudioDataProvider.GenerateSilence = value;
                }
            }
        }

        public string Name { get; protected set; }

        public bool IsInputDevice { get; protected set; }

        public bool IsOutputDevice { get; protected set; }

        protected BaseDevice():this(VLApi.Instance){}

        private BaseDevice(IVLApi api)
        {
            _audioService = api.GetAudioService();

        }
        
        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public async Task<Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>> CreateInput(DeviceConfigRequest request)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                RecordingConfig = PrepareRecording(request);
                if (AudioInputBlock == null)
                {
                    AudioInputBlock = new AudioInputBlock();
                    AudioInputBlock.Create(InputBufferBlock, RecordingConfig .AudioFormat, 2);
                    RecordingBuffer = AudioInputBlock.Buffer;
                    IsRecording = true;
                    await Init();
                }
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }
            return new Tuple<DeviceConfigResponse, ISourceBlock<AudioDataMessage>>(RecordingConfig , InputBufferBlock);
        }

        public async Task<Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>> CreateOutput(DeviceConfigRequest request)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                PlayingConfig = PreparePlaying(request);
                if (AudioOutputBlock == null)
                {
                    AudioOutputBlock = new AudioOutputBlock();
                    AudioOutputBlock.Create(PlayingConfig.AudioFormat, 2);
                    PlayingBuffer = AudioOutputBlock.Buffer;
                    AudioDataProvider =
                        new AudioDataProvider(Logger, request.AudioFormat.WaveFormat, PlayingBuffer)
                        {
                            CancellationToken = CancellationTokenSource.Token,
                            GenerateSilence = GenerateSilence
                        };
                    IsPlaying = true;
                    await Init();
                }
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }
            return new Tuple<DeviceConfigResponse, ITargetBlock<AudioDataMessage>>(PlayingConfig, AudioOutputBlock);
        }

        protected virtual DeviceConfigResponse PrepareRecording(DeviceConfigRequest request)
        {
            return new DeviceConfigResponse()
            {
                Channels = 2,
                AudioFormat = request.AudioFormat,
                ChannelOffset = 0,
                Latency = 0,
                DriverChannels = 2,
                FrameSize = request.AudioFormat.BufferSize,
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };
        }
        protected virtual DeviceConfigResponse PreparePlaying(DeviceConfigRequest request)
        {
            return new DeviceConfigResponse()
            {
                Channels = 2,
                AudioFormat = request.AudioFormat,
                ChannelOffset = 0,
                Latency = 0,
                DriverChannels = 2,
                FrameSize = request.AudioFormat.BufferSize,
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
            };
        }

        public virtual string DebugInfo()
        {
            var dir = IsPlaying && IsRecording ? "FD" : IsPlaying ? "P" : IsRecording ? "R" : "-";
            var input = IsRecording ? AudioInputBlock?.DebugInfo() : "";
            var output = IsPlaying ? AudioOutputBlock?.DebugInfo() : "";
            return $"{dir}, cancelled={CancellationTokenSource?.Token.IsCancellationRequested}, in={input}, out={output}";
        }

        public override string ToString()
        {
            return Name;
        }

        protected abstract Task<bool> Init();
        public abstract bool Start();
        public abstract bool Stop();
        
        private bool _disposedValue;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for Device {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioInputBlock?.Dispose();
                    AudioOutputBlock?.Dispose();
                    _audioService?.Dispose();     
                    CancellationTokenSource = null;
                    AudioInputBlock = null;
                    AudioOutputBlock = null;
                }

                _disposedValue = true;
            }
        }
    }
}