using System;
using System.Buffers;
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

        public AudioDataProvider AudioDataProvider { get; protected set; }
        public bool IsPlaying { get; private set; }
        public bool IsRecording { get; private set; }

        public ActualDeviceParams RecordingParams { get; }
        public ActualDeviceParams PlayingParams { get; }
        
        private Dictionary<IVirtualDevice, ActualDeviceParams> _virtualDevices = new();
        private IVirtualDevice[] _playingDevices;
        private IVirtualDevice[] _recordingDevices;
        private bool[] _pausedDevices; 
        protected MixBuffers MixBuffers;
        protected SplitBuffers SplitBuffers;
        
        protected CancellationTokenSource CancellationTokenSource;

        public string Name { get; protected set; }

        public bool IsInputDevice { get; protected set; }

        public bool IsOutputDevice { get; protected set; }

        protected BaseDevice()
        {
            _audioService = Factory.GetAudioService();
            RecordingParams = AudioParams.Create<ActualDeviceParams>();
            PlayingParams = AudioParams.Create<ActualDeviceParams>();
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

        public void Update()
        {
            //todo exception handling

            UpdateParams();
            var recording = IsInputDevice && RecordingParams.HasChanged;
            var playing = IsOutputDevice && PlayingParams.HasChanged;
            if (recording || playing)
            {
                Logger.Information("Stopping {Device}", Name);
                Stop();
                CancellationTokenSource = new CancellationTokenSource();
                if (recording)
                {
                    SplitBuffers = new SplitBuffers(RecordingParams.AudioFormat);
                }

                if (playing)
                {

                    DataSlot[] slots = _playingDevices.Select(d => new DataSlot()
                    {
                        Offset = d.ActualParams.FirstChannel,
                        Channels = d.ActualParams.Channels.Value,
                        Q = new MSQueue<float[]>()
                    }).ToArray();
                    MixBuffers = new MixBuffers(slots, PlayingParams.AudioFormat);
                    AudioDataProvider = new AudioDataProvider(Logger, PlayingParams.AudioFormat.WaveFormat, MixBuffers)
                    {
                        CancellationToken = CancellationTokenSource.Token
                    };
                }

                Init();
                RecordingParams.Commit();
                PlayingParams.Commit();
                
                Logger.Information("Started {Device} using {Playing} playing and {Recording} recording device nodes", Name, _playingDevices.Length, _recordingDevices.Length);
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
            _pausedDevices = new bool[_playingDevices.Length];
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

                param.ConnectedDevices.Value = all.Count;
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
                param.ConnectedDevices.Value = 0;
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
                actualParams.Update();
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
            var index = Array.IndexOf(_playingDevices, device);
            MixBuffers.SetData(index, msg.Data);
            // ArrayPool<float>.Shared.Return(msg.Data);
        }
        public void FillBuffer(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            MixBuffers.FillBuffer(buffer, offset, count, cancellationToken);
        }
        
        public void OnDataReceived(IntPtr[] channels)
        {
            
        }
        
        public void OnDataReceived(byte[] buffer)
        {
            SplitBuffers.SetBuffer(buffer);
            
            foreach (var device in _recordingDevices)
            {
                if (_virtualDevices.ContainsKey(device))
                {
                    var p = _virtualDevices[device];
                    var msg = SplitBuffers.CreateMessage(p.AudioFormat, p.FirstChannel);
                    device.Post(msg);
                }
            }
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