using System.Linq;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Backend
{
    public class AudioAggregateStream<TSampleType, TMemoryAccess> : AudioOutputStream<TSampleType, TMemoryAccess>,  IAudioInputOutputStream
        where TSampleType : struct, ISampleType
        where TMemoryAccess : struct, IMemoryAccess
    {
        public override int NumOutputChannels { get; }
        public override int NumInputChannels { get; }
        public override AudioStreamType Type => AudioStreamType.Aggregate;

        private readonly IAudioInputStream _inputStream;
        private readonly AudioStreamConfig[] _configs;
        private bool _disposed;
        
        public AudioAggregateStream(AudioStreamConfig primary, AudioStreamConfig[] secondary)
            : base(primary)
        {
            _configs = secondary.Where(i => i.ActiveInputChannels.Count > 0).ToArray();
            
            var numInputChannels = _configs.Sum(i => i.ActiveInputChannels.Count) + primary.ActiveInputChannels.Count;
            NumOutputChannels = primary.ActiveOutputChannels.Count;
            NumInputChannels = numInputChannels;

            _inputStream = new AudioInputStream<TSampleType, TMemoryAccess>(primary
            with {
                ActiveInputChannels = AudioChannels.Channels(numInputChannels),
                ActiveOutputChannels = AudioChannels.Disabled
            });

            Logger.Information("Audio aggregate stream created: {@This}", this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _disposed = true;
                    Stop();
                    _inputStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public double InputLatency => Latency.input;

        public override void Open(IAudioStreamCallback? callback)
        {
            Callback = callback;
            var mix = new XtMix(Config.SampleRate, SampleType);

            var outputChannels = CreateChannels();

            XtStreamParams streamParams;
            if (Callback != null)
            {
                streamParams = new XtStreamParams(Config.Interleaved, Callback.OnBuffer, Callback.OnXRun,
                    Callback.OnRunning);
            }
            else
            {
                streamParams = new XtStreamParams(Config.Interleaved, null, null, null);
            }

            double bufSize = Config.BufferSize;
            var aggregateDeviceParams =
                _configs.Select(i
                        => new XtAggregateDeviceParams(((XtAudioDevice)i.AudioDevice).XtDevice, new XtChannels(i.ActiveInputChannels.Count, i.ActiveInputChannels.Mask,0,0), bufSize))
                    .Concat(new[] { new XtAggregateDeviceParams(XtDevice, outputChannels, bufSize) }).ToArray();

            var deviceParams = new XtAggregateStreamParams(streamParams, aggregateDeviceParams,
                aggregateDeviceParams.Length, mix, XtDevice);

            Stream = XtService.AggregateStream(deviceParams, this);

            FramesPerBlock = Stream.GetFrames();

            _inputStream.CreateBuffers(NumInputChannels, FramesPerBlock);

            CreateBuffers(NumOutputChannels, FramesPerBlock);
        }

        public AudioBuffer? BindInput(XtBuffer buffer)
        {
            return _inputStream.BindInput(buffer);
        }
    }
}