using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using NewAudio.Nodes;

namespace NewAudio.Devices
{
    public class AsioDevice : BaseDevice, IWaveProvider
    {
        private AsioOut _asioOut;
        private readonly string _driverName;
        public override string Name { get; }
        private int _numberOfInputChannels;
        private int _numberOfOutputChannels;
        public override int NumberOfInputChannels => _numberOfInputChannels;
        public override int NumberOfOutputChannels => _numberOfOutputChannels;
        public WaveFormat WaveFormat { get; set; }

        
        public AsioDevice(string name, string driverName)
        {
            Name = name;
            InitLogger<AsioDevice>();
            Logger.Information("CREATE: AsioDevice ({DriverName})", driverName);
            _driverName = driverName;
            _sampleRate = 48000;
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        void  OnAsioOutAudioAvailable(object sender, AsioAudioAvailableEventArgs e)
        {
            _framesPerBlock = e.SamplesPerBuffer;

            try
            {
                Output.FillBuffer(e.OutputBuffers, e.SamplesPerBuffer);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                Logger.Error(exception, "Error in ASIO Thread");
            }

            e.WrittenToOutputBuffers = true;
        }
        public override void Initialize()
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_sampleRate, 2);
            _asioOut = new AsioOut(_driverName);
            _asioOut.InitRecordAndPlayback(this, 2, _sampleRate);
            _asioOut.AudioAvailable += OnAsioOutAudioAvailable;
            _numberOfInputChannels = _asioOut.DriverInputChannelCount;
            _numberOfOutputChannels = _asioOut.DriverOutputChannelCount;
            _framesPerBlock = _asioOut.FramesPerBuffer;
        }

        public override void Uninitialize()
        {
            _asioOut.AudioAvailable -= OnAsioOutAudioAvailable;
            _asioOut.Stop();
            _asioOut.Dispose();
            _asioOut = null;
        }

        public override void EnableProcessing()
        {
            _asioOut.Play();
        }

        public override void DisableProcessing()
        {
            _asioOut.Stop();
            
        }

        
        /*
        protected override DeviceConfigResponse PrepareRecording(DeviceConfigRequest request)
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut(_driverName);
            }

            return base.PrepareRecording(request);
        }

        protected override DeviceConfigResponse PreparePlaying(DeviceConfigRequest request)
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut(_driverName);
            }

            return new DeviceConfigResponse()
            {
                Channels = 2,
                AudioFormat = request.AudioFormat,
                ChannelOffset = 0,
                Latency = 0,
                DriverChannels = _asioOut.DriverOutputChannelCount,
                FrameSize = request.AudioFormat.BufferSize,
                SupportedSamplingFrequencies = Enum.GetValues(typeof(SamplingFrequency)).Cast<SamplingFrequency>()
                    .Where(sr => _asioOut.IsSampleRateSupported((int)sr)).ToList()
            };
        }
*/
        /*
        protected override bool Init()
        {
            if (_asioOut == null)
            {
                _asioOut = new AsioOut();
            }
            if (IsPlaying && IsRecording)
            {
                _asioOut.InitRecordAndPlayback(AudioDataProvider, RecordingParams.Channels.Value,
                    (int)RecordingParams.SamplingFrequency.Value);
                _asioOut.InputChannelOffset = RecordingParams.ChannelOffset.Value;
                _asioOut.ChannelOffset = PlayingParams.ChannelOffset.Value;
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                // PlayingParams.Channels = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency.Value = _asioOut.PlaybackLatency;
                RecordingParams.Latency.Value = _asioOut.PlaybackLatency;
                // todo
                // PlayingParams.FrameSize = _asioOut.FramesPerBuffer;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = true;
                RecordingParams.Active.Value = true;
            }
            else if (IsRecording)
            {
                _asioOut.InitRecordAndPlayback(null, RecordingParams.Channels.Value,
                    (int)RecordingParams.SamplingFrequency.Value);
                _asioOut.AudioAvailable += OnAsioData;
                // RecordingParams.Channels = _asioOut.NumberOfInputChannels;
                RecordingParams.Latency.Value = _asioOut.PlaybackLatency;
                // RecordingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = false;
                RecordingParams.Active.Value = true;
            }
            else if (IsPlaying)
            {
                _asioOut.Init(AudioDataProvider);
                // PlayingParams.Channels.Value = _asioOut.NumberOfOutputChannels;
                PlayingParams.Latency.Value = _asioOut.PlaybackLatency;
                // PlayingParams.FrameSize = _asioOut.FramesPerBuffer;
                PlayingParams.Active.Value = true;
                RecordingParams.Active.Value = false;
            }

            _asioOut.Play();
            return _asioOut.PlaybackState == PlaybackState.Playing;
        }

        protected override bool Stop()
        {
            if (_asioOut != null )
            {
                if (_asioOut.PlaybackState == PlaybackState.Playing)
                {
                    CancellationTokenSource?.Cancel();
                    _asioOut.Stop();
                }

                _asioOut.AudioAvailable -= OnAsioData;
                _asioOut.Dispose();
                _asioOut = null;
            }

            return true;
        }
        */

        private void OnAsioData(object sender, AsioAudioAvailableEventArgs evt)
        {
            // AudioService.Instance.Flow.PostRequest(new AudioDataRequestMessage(evt.BytesRecorded/4));
            // if (RecordingBuffer != null)
            // {
            // Buffers.WriteAll(RecordingBuffer, evt.Buffer, evt.BytesRecorded, Token);
            // }
        }

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (_asioOut != null)
                    {
                        if (_asioOut.PlaybackState == PlaybackState.Playing)
                        {
                            // CancellationTokenSource.Cancel();
                            _asioOut.Stop();
                        }

                        _asioOut.AudioAvailable -= OnAsioOutAudioAvailable;
                        _asioOut.Dispose();
                        _asioOut = null;
                    }
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

        // public string DebugInfo()
        // {
            // return $"[{this}, {_asioOut?.PlaybackState}, {base.DebugInfo()}]";
        // }
    }
}