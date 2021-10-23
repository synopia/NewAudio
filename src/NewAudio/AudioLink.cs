using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using VL.Lib.Collections;

namespace NewAudio
{
    public class AudioLink : IDisposable, ISampleProvider
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioLink");
        public WaveFormat WaveFormat => Format.WaveFormat;
        public BroadcastBlock<AudioBuffer> BroadcastBlock = new BroadcastBlock<AudioBuffer>(i=>i, new GroupingDataflowBlockOptions()
        {
        });

        private ISourceBlock<AudioBuffer> _sourceBlock; 
        public ISourceBlock<AudioBuffer> SourceBlock
        {
            get=>BroadcastBlock;
            set
            {
                _sourceBlock = value;
                _sourceBlock.LinkTo(BroadcastBlock);
            }
        }
        public AudioFormat Format;
        
        public AudioLink()
        {
            AudioCore.Instance.AddAudioLink(this);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        private IDisposable _link;
        public float[] OutBuffer = new float[1];
        private ActionBlock<AudioBuffer> _action;
        private int _pos = 0;
        public float[] GetSamples(int count=0)
        {
            /*
            _pos = 0;
            if (_action == null)
            {
                _logger.Info($"Getting samples {count}");
                if (OutBuffer==null || count != OutBuffer.Length)
                {
                    OutBuffer = new float[count];
                }
                _action = new ActionBlock<AudioBuffer>(input =>
                {
                    if (_pos < count)
                    {
                        Array.Copy(input.Data, 0, OutBuffer, _pos, Math.Min(input.Data.Length, OutBuffer.Length));
                        _pos += input.Data.Length;
                    }
                }, new ExecutionDataflowBlockOptions()
                {
                });
            }

            if (_link == null && SourceBlock!=null )
            {
                _logger.Info($"Setting link {SourceBlock}");
                // _link = BroadcastBlock.LinkTo(_action);
                _logger.Info($"Link: {_link}");
            }

            */
            return OutBuffer;
        }

        public void Dispose()
        {
            AudioCore.Instance.RemoveAudioLink(this);
        }
    }
}