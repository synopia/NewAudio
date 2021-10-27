using System;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave.SampleProviders;
using Serilog;

namespace NewAudio
{
    public class WdlResampling : AudioFlowBuffer
    {
        private WdlResamplingSampleProvider _wdl;
        private int _time = 0;
        private double _dTime = 0;

        public WdlResampling(AudioFormat format, int internalBufferSize) : base(format, internalBufferSize)
        {
            _wdl = new WdlResamplingSampleProvider(Buffer, format.SampleRate);
        }

        protected override void OnDataReceived(int time, int count)
        {
            _time = time;
            try
            {
                while (Buffer.BufferedSamples >= Format.BufferSize)
                {
                    var buf = AudioCore.Instance.BufferFactory.GetBuffer(Format.BufferSize);
                
                    _wdl.Read(buf.Data, 0, Format.BufferSize);
                    _time += Format.SampleCount;
                    _dTime += 1.0 / Format.SampleRate;
                    buf.Time = _time;
                    buf.DTime = _dTime;
                    Source.Post(buf);
                }
            }
            catch (Exception e)
            {
                Logger.Error("{e}",e);
            }
        }
    }
    public class AudioResampling: AudioNodeTransformer
    {
        private readonly ILogger _logger = Log.ForContext<AudioResampling>();
        public int InternalBufferSize { get; private set; }
        public int NewSampleRate { get; private set; }
        
        public WdlResampling Source { get; private set; }
        private IDisposable _link;
        
        public AudioResampling()
        {
            
        }

        public AudioLink Update(AudioLink input, int internalBufferSize, int newSampleRate)
        {
            if (InternalBufferSize != internalBufferSize || NewSampleRate != newSampleRate || Input != input)
            {
                InternalBufferSize = internalBufferSize;
                NewSampleRate = newSampleRate;
                Connect(input);
                _link?.Dispose();
                
                if (input != null)
                {
                    try
                    {
                        InternalBufferSize = internalBufferSize;
                        NewSampleRate = newSampleRate;
                        var format = input.Format.WithSampleRate(newSampleRate);
                        _logger.Information("Creating new wdl resampler {@InputFormat} => {@OutputFormat}, buffer: {size}", input.Format, format, input.Format.SampleCount*internalBufferSize);
                        Source = new WdlResampling(format, input.Format.SampleCount*internalBufferSize);

                        _link = input.SourceBlock.LinkTo(Source);
                        Output.SourceBlock = Source;
                        Output.Format = Source.Format;
                    }
                    catch (Exception e)
                    {
                        _logger.Error("{exception}", e);
                    }
                }
            }

            return Output;
        }
    }
}