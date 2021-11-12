using System;
using System.Collections.Generic;
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

        protected DeviceConfigResponse _recordingConfig;
        protected DeviceConfigResponse _playingConfig; 
        public  DeviceConfigResponse RecordingConfig=>_recordingConfig;
        public  DeviceConfigResponse PlayingConfig=>_playingConfig; 
        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }

        private List<AudioInputBlockConfig> _inputBlockConfigs = new();
        private List<AudioOutputBlockConfig> _outputBlockConfigs = new();
        
        // public BroadcastBlock<AudioDataMessage> InputBufferBlock = new BroadcastBlock<AudioDataMessage>(i => i);
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

        protected BaseDevice():this(Factory.Instance){}

        private BaseDevice(IFactory api)
        {
            _audioService = api.GetAudioService();

        }
        
        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public async Task<DeviceConfigResponse> CreateInput(DeviceConfigRequest request, ITargetBlock<AudioDataMessage> targetBlock)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                var config = PrepareRecording(request);
                // todo: only channels will renew real device
                if (AudioInputBlock == null || config.ChannelOffset!=_recordingConfig.ChannelOffset || config.Channels!=_recordingConfig.Channels )
                {
                    _recordingConfig = config;
                    if (AudioInputBlock != null)
                    {
                        AudioInputBlock.Dispose();
                    }
                    _inputBlockConfigs.Add(new AudioInputBlockConfig(request.FirstChannel, request.LastChannel, targetBlock));
                    AudioInputBlock = new AudioInputBlock();
                    AudioInputBlock.Create(_inputBlockConfigs.ToArray(), config.AudioFormat, 2);
                    RecordingBuffer = AudioInputBlock.Buffer;
                    IsRecording = true;
                    await Init();
                }

                var resp = new DeviceConfigResponse()
                {
                    Channels = request.Channels,
                    ChannelOffset = request.ChannelOffset,
                    AudioFormat = request.AudioFormat,
                    Latency = config.Latency,
                    DriverChannels = config.DriverChannels,
                    FrameSize = config.FrameSize // todo?
                };
                return resp;
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }
        }

        public async Task<DeviceConfigResponse> CreateOutput(DeviceConfigRequest request, ISourceBlock<AudioDataMessage> sourceBlock)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                var config = PreparePlaying(request);
                if (AudioOutputBlock == null || config.Channels!=_playingConfig.Channels || config.ChannelOffset!=_playingConfig.ChannelOffset)
                {
                    _playingConfig = config;
                    if (AudioOutputBlock != null)
                    {
                        AudioOutputBlock.Dispose();
                    }
                    _outputBlockConfigs.Add(new AudioOutputBlockConfig(request.FirstChannel, request.LastChannel ,sourceBlock));
                    AudioOutputBlock = new AudioOutputBlock();
                    AudioOutputBlock.Create(_outputBlockConfigs.ToArray(), config.AudioFormat, 2);
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
                var resp = new DeviceConfigResponse()
                {
                    Channels = request.Channels,
                    ChannelOffset = request.ChannelOffset,
                    AudioFormat = request.AudioFormat,
                    Latency = config.Latency,
                    DriverChannels = config.DriverChannels,
                    FrameSize = config.FrameSize // todo?
                };
                return resp;
            }
            catch (Exception e)
            {
                Dispose();
                throw;
            }


        }

        protected virtual DeviceConfigResponse PrepareRecording(DeviceConfigRequest request)
        {
            var config = _recordingConfig;
            if (_inputBlockConfigs.Count == 0)
            {
                config = new DeviceConfigResponse()
                {
                    ChannelOffset = request.ChannelOffset,
                    Channels = request.Channels,
                    AudioFormat = request.AudioFormat,
                    Latency = 0,
                    DriverChannels = 2,
                    FrameSize = request.AudioFormat.BufferSize,
                    SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
                };
            } else 
            {
                var first = Math.Min(config.FirstChannel, request.FirstChannel);
                var last = Math.Max(config.LastChannel, request.LastChannel);
                config.ChannelOffset = first;
                config.Channels = last - first;
                config.AudioFormat = config.AudioFormat.WithChannels(last - first);
            }

            return config;
        }
        protected virtual DeviceConfigResponse PreparePlaying(DeviceConfigRequest request)
        {
            var config = _playingConfig;
            if (_outputBlockConfigs.Count==0)
            {
                config = new DeviceConfigResponse()
                {
                    ChannelOffset = request.ChannelOffset,
                    Channels = request.Channels,
                    AudioFormat = request.AudioFormat,
                    Latency = 0,
                    DriverChannels = 2,
                    FrameSize = request.AudioFormat.BufferSize,
                    SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
                };
            }
            else
            {
                var first = Math.Min(config.FirstChannel, request.FirstChannel);
                var last = Math.Max(config.LastChannel, request.LastChannel);
                config.ChannelOffset = first;
                config.Channels = last - first;
                config.AudioFormat = config.AudioFormat.WithChannels(last - first);
            }

            return config;
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
                    CancellationTokenSource?.Cancel();
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