using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NAudio.Wave;
using NewAudio.Blocks;
using NewAudio.Core;
using NewAudio.Internal;
using NewAudio.Nodes;
using Serilog;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudio.Devices
{
    public abstract class BaseAudioClient : IAudioClient
    {
        protected ILogger Logger;
        private readonly IResourceHandle<AudioService> _audioService;

        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }

        public DeviceConfig RecordingParams { get; private set; }
        public DeviceConfig PlayingParams { get; private set; }
        
        private List<VirtualOutput> _playingDevices = new ();
        private List<VirtualInput> _recordingDevices = new ();

        protected OutputNode OutputNode { get; set; }
        protected InputNode InputNode { get; set; }
        protected CancellationTokenSource CancellationTokenSource;

        public string Name { get; protected set; }

        public bool IsInputDevice { get; protected set; }

        public bool IsOutputDevice { get; protected set; }

        protected BaseAudioClient()
        {
            _audioService = Factory.GetAudioService();
        }

        protected void InitLogger<T>()
        {
            Logger = _audioService.Resource.GetLogger<T>();
        }

        public void Add(VirtualInput input)
        {
            InputNode.Connect(input);            
            _recordingDevices.Add(input);
            RecordingParams = null;
        }

        public void Remove(VirtualInput input)
        {
            InputNode.Disconnect(input);
            _recordingDevices.Remove(input);
            RecordingParams = null;
        }

        public void Add(VirtualOutput output)
        {
            output.Connect(OutputNode);
            _playingDevices.Add(output);
            PlayingParams = null;
        }

        public void Remove(VirtualOutput output)
        {
            output.Disconnect(OutputNode);
            _playingDevices.Remove(output);
            PlayingParams = null;
        }

        public void Update()
        {
            //todo exception handling

            if ( IsOutputDevice && PlayingParams==null )
            {

                PlayingParams = BuildConfig(_playingDevices.Select(p=>p.ConfigRequest).ToList());
                if (PlayingParams != null)
                {
                    Stop();
                    CancellationTokenSource = new CancellationTokenSource();
                    AnswerPlayRequests(PlayingParams);

                    Logger.Information("{Device}: Updated {Count} virtual outputs", Name, _playingDevices.Count);
                    OutputNode.UpdateConfig(PlayingParams);
                    foreach (var device in _playingDevices)
                    {
                        device.Graph.OutputNode = OutputNode;
                    }

                    Init();
                }

            }
            if( IsInputDevice && RecordingParams==null){
                RecordingParams = BuildConfig(_recordingDevices.Select(r => r.ConfigRequest).ToList());
                if (RecordingParams != null)
                {
                    Stop();
                    CancellationTokenSource = new CancellationTokenSource();
                    AnswerRecordRequests(RecordingParams);
                    Logger.Information("{Device}: Updated {Count} virtual inputs", Name, _recordingDevices.Count);

                    InputNode.UpdateConfig(RecordingParams);
                    Init();
                }

                
            }
        }
        
        private DeviceConfig BuildConfig(List<DeviceConfigRequest> requests)
        {
            if (requests.Count > 0)
            {
                var first = requests.Min(p => p.FirstChannel);
                var last = requests.Max(p => p.LastChannel);
                var sr = requests.Max(p => p.SamplingFrequency);
                // var latency = all.Max(p => p.Key.Params.DesiredLatency.Value);
                IsPlaying = true;

                return new DeviceConfig()
                {
                    Active = true,
                    ChannelOffset = first,
                    Channels = last - first,
                    SamplingFrequency = sr,
                    FramesPerBlock = 512,
                    WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)sr, last-first)
                };
                
            }
            return null;
        }

        protected  void AnswerPlayRequests(DeviceConfig param)
        {
            foreach (var device in _playingDevices)
            {
                if (device.ConfigRequest.FirstChannel >= param.FirstChannel && device.ConfigRequest.LastChannel <= param.LastChannel)
                {
                    device.Config = new DeviceConfig()
                    {
                        Active = true,
                        ChannelOffset = device.ConfigRequest.ChannelOffset,
                        Channels = device.ConfigRequest.Channels,
                        SamplingFrequency = param.SamplingFrequency,
                        FramesPerBlock = param.FramesPerBlock,
                        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)param.SamplingFrequency, device.ConfigRequest.Channels)

                    };
                }
                else
                {
                    device.Config = null;
                }
            }
        }
        protected  void AnswerRecordRequests(DeviceConfig param)
        {
            foreach (var device in _recordingDevices)
            {
                if (device.ConfigRequest.FirstChannel >= param.FirstChannel && device.ConfigRequest.LastChannel <= param.LastChannel)
                {
                    device.Config = new DeviceConfig()
                    {
                        Active = true,
                        ChannelOffset = device.ConfigRequest.ChannelOffset,
                        Channels = device.ConfigRequest.Channels,
                        // SamplingFrequency = PlayingParams.SamplingFrequency,
                        FramesPerBlock = 512,//PlayingParams.FramesPerBlock,
                        // WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat((int)PlayingParams.SamplingFrequency, device.ConfigRequest.Channels)

                    };
                }
                else
                {
                    device.Config = null;
                }
            }
        }

        public void Pause(IVirtualDevice device)
        {
            // var index = Array.IndexOf(_playingDevices, device);
            // if (!_pausedDevices[index])
            // {
                // _pausedDevices[index] = true;
                // MixBuffers.DecreaseDevices();
            // }
        }
        public void UnPause(IVirtualDevice device)
        {
            // var index = Array.IndexOf(_playingDevices, device);
            // if (_pausedDevices[index])
            // {
                // _pausedDevices[index] = false;
                // MixBuffers.IncreaseDevices();
            // }
        }
        public void AddAudioMessage(IVirtualDevice device, AudioDataMessage msg)
        {
        }
        public void FillBuffer(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
        }
        
        public void OnDataReceived(IntPtr[] channels)
        {
            
        }
        
        public void OnDataReceived(byte[] buffer)
        {
            /*SplitBuffers.SetBuffer(buffer);
            
            foreach (var device in _recordingDevices)
            {
                if (_virtualDevices.ContainsKey(device))
                {
                    var p = _virtualDevices[device];
                    var msg = SplitBuffers.CreateMessage(p.AudioFormat, p.FirstChannel);
                    device.Post(msg);
                }
            }*/
        }
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
        protected abstract bool Stop();

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