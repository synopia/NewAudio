using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Internal;
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
        // public AudioOutputBlock AudioOutputBlock { get; protected set; }

        // public CircularBuffer RecordingBuffer { get; private set; }
        // public CircularBuffer PlayingBuffer { get; private set; }

        // protected DeviceConfigResponse _recordingConfig;
        // protected DeviceConfigResponse _playingConfig;
        // public DeviceConfigResponse RecordingConfig => _recordingConfig;
        // public DeviceConfigResponse PlayingConfig => _playingConfig;
        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }

        // private List<AudioInputBlockConfig> _inputBlockConfigs = new();
        // private List<AudioOutputBlockConfig> _outputBlockConfigs = new();

        public ActualDeviceParams RecordingParams { get; }
        public ActualDeviceParams PlayingParams { get; }
        
        private Dictionary<IVirtualDevice, ActualDeviceParams> _virtualDevices = new();
        private IVirtualDevice[] _playingDevices;
        private IVirtualDevice[] _recordingDevices;
        protected MixBuffers MixBuffers;
        protected SplitBuffers SplitBuffers;
        
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

        protected BaseDevice() : this(Factory.Instance)
        {
            RecordingParams = AudioParams.Create<ActualDeviceParams>();
            PlayingParams = AudioParams.Create<ActualDeviceParams>();
        }

        private BaseDevice(IFactory api)
        {
            _audioService = api.GetAudioService();
        }

        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }


        public ActualDeviceParams Add(VirtualInput input)
        {
            var deviceParams = AudioParams.Create<ActualDeviceParams>();
            _virtualDevices[input] = deviceParams;
            return deviceParams;
        }

        public void Remove(VirtualInput input)
        {
            _virtualDevices.Remove(input);
        }

        public ActualDeviceParams Add(VirtualOutput output)
        {
            var deviceParams = AudioParams.Create<ActualDeviceParams>();
            _virtualDevices[output] = deviceParams;
            return deviceParams;
        }

        public void Remove(VirtualOutput output)
        {
            _virtualDevices.Remove(output);
        }


        public IMixBuffer GetMixBuffer(IVirtualDevice device)
        {
            return MixBuffers.GetMixBuffer(Array.IndexOf(_playingDevices, device));
        }

        public void ReturnMixBuffer(IVirtualDevice device)
        {
            MixBuffers.ReturnMixBuffer(Array.IndexOf(_playingDevices, device));
        }

        public void Update()
        {
            UpdateParams();
            if ((IsInputDevice && RecordingParams.HasChanged) || (IsOutputDevice && PlayingParams.HasChanged))
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                SplitBuffers = new SplitBuffers(RecordingParams.AudioFormat);
                MixBuffers = new MixBuffers(_playingDevices.Length, 4, PlayingParams.AudioFormat);
                //todo stop
                AudioDataProvider = new AudioDataProvider(Logger, PlayingParams.AudioFormat.WaveFormat, MixBuffers);

                Init();
                RecordingParams.Commit();
                PlayingParams.Commit();

            }
        }

        public void UpdateParams()
        {
            if (IsInputDevice)
            {
                BuildParams(RecordingParams, false, true);
                UpdateActualParams(RecordingParams, false, true);
            }

            if (IsOutputDevice)
            {
                BuildParams(PlayingParams, true, false);
                UpdateActualParams(PlayingParams, true, false);
            }

            _playingDevices = _virtualDevices.Where(p => p.Value.IsPlayingDevice.Value).Select(p=>p.Key).ToArray();
            _recordingDevices = _virtualDevices.Where(p => p.Value.IsRecordingDevice.Value).Select(p=>p.Key).ToArray();
        }
        
        protected void BuildParams(ActualDeviceParams param, bool playing, bool recording)
        {
            var all = _virtualDevices.Where(p => p.Key.IsPlaying==playing && p.Key.IsRecording==recording).ToList();

            if (all.Count > 0)
            {
                var first = all.Min(p => p.Key.Params.FirstChannel);
                var last = all.Max(p => p.Key.Params.LastChannel);
                var sr = all.Max(p => p.Key.Params.SamplingFrequency.Value);
                // var latency = all.Max(p => p.Key.Params.DesiredLatency.Value);

                param.IsPlayingDevice.Value = playing;
                param.IsRecordingDevice.Value = recording;
                IsPlaying = playing;
                IsRecording = recording;
                param.ChannelOffset.Value = first;
                param.Channels.Value = last - first;
                param.SamplingFrequency.Value = sr;
                // param.Latency.Value = latency;
                // todo verify if possible
            }
            else
            {
                param.Active.Value = false;
                param.IsPlayingDevice.Value = false;
                param.IsRecordingDevice.Value = false;
            }
        }

        protected  void UpdateActualParams(ActualDeviceParams param,bool playing, bool recording)
        {
            var all = _virtualDevices.Where(p => p.Key.IsPlaying==playing && p.Key.IsRecording==recording).ToList();

            foreach (var pair in all)
            {
                var device = pair.Key;
                var actualParams = pair.Value;

                if (device.Params.FirstChannel >= param.FirstChannel && device.Params.LastChannel <= param.LastChannel)
                {
                    actualParams.ChannelOffset.Value = device.Params.FirstChannel;
                    actualParams.Channels.Value = device.Params.Channels.Value;
                    actualParams.SamplingFrequency.Value = param.SamplingFrequency.Value;
                    actualParams.Active.Value = true;
                    actualParams.IsPlayingDevice.Value = playing;
                    actualParams.IsRecordingDevice.Value = recording;
                }
                else
                {
                    actualParams.Active.Value = false;
                    actualParams.IsPlayingDevice.Value = false;
                    actualParams.IsRecordingDevice.Value = false;
                }
                device.Params.Commit();
            }
        }

        public void OnDataReceived(IntPtr[] channels)
        {
            
        }
        public void OnDataReceived(byte[] buffer)
        {
            SplitBuffers.SetBuffer(buffer);
            
            foreach (var device in _recordingDevices)
            {
                var p = _virtualDevices[device];
                var msg = SplitBuffers.CreateMessage(p.AudioFormat, p.FirstChannel);
                device.Post(msg);
            }
        }
/*
        public async Task<DeviceConfigResponse> CreateInput(DeviceConfigRequest request,
            ITargetBlock<AudioDataMessage> targetBlock)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                var config = PrepareRecording(request);
                // todo: only channels will renew real device
                if (AudioInputBlock == null || config.ChannelOffset != _recordingConfig.ChannelOffset ||
                    config.Channels != _recordingConfig.Channels)
                {
                    _recordingConfig = config;
                    if (AudioInputBlock != null)
                    {
                        AudioInputBlock.Dispose();
                    }

                    _inputBlockConfigs.Add(new AudioInputBlockConfig(request.FirstChannel, request.LastChannel,
                        targetBlock));
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
            catch (Exception _)
            {
                // todo still needed?
                Dispose();
                throw;
            }
        }

        public async Task<DeviceConfigResponse> CreateOutput(DeviceConfigRequest request,
            ISourceBlock<AudioDataMessage> sourceBlock)
        {
            try
            {
                CancellationTokenSource ??= new CancellationTokenSource();
                var config = PreparePlaying(request);
                if (AudioOutputBlock == null || config.Channels != _playingConfig.Channels ||
                    config.ChannelOffset != _playingConfig.ChannelOffset)
                {
                    _playingConfig = config;
                    if (AudioOutputBlock != null)
                    {
                        AudioOutputBlock.Dispose();
                    }

                    _outputBlockConfigs.Add(new AudioOutputBlockConfig(request.FirstChannel, request.LastChannel,
                        sourceBlock));
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
/*
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

        protected virtual DeviceConfigResponse PreparePlaying(DeviceConfigRequest request)
        {
            var config = _playingConfig;
            if (_outputBlockConfigs.Count == 0)
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
*/
        public virtual string DebugInfo()
        {
            var dir = IsPlaying && IsRecording ? "FD" : IsPlaying ? "P" : IsRecording ? "R" : "-";
            // var input = IsRecording ? AudioInputBlock?.DebugInfo() : "";
            // var output = IsPlaying ? AudioOutputBlock?.DebugInfo() : "";
            return
                $"{dir}, cancelled={CancellationTokenSource?.Token.IsCancellationRequested}"; //, in={input}, out={output}";
        }

        public override string ToString()
        {
            return Name;
        }

        protected abstract bool Init();

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for Device {This} ({Disposing})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    CancellationTokenSource?.Cancel();
                    // AudioInputBlock?.Dispose();
                    // AudioOutputBlock?.Dispose();
                    _audioService?.Dispose();
                    CancellationTokenSource = null;
                    // AudioInputBlock = null;
                    // AudioOutputBlock = null;
                }

                _disposedValue = true;
            }
        }
    }
}