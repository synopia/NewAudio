using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using NewAudio.Internal;
using VL.Lib.Collections;

namespace NewAudio
{
    public class AudioBufferOut: AudioNodeConsumer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("AudioBufferOut");
        
        private IDisposable _link;
        private float[] _outBuffer;
        private ActionBlock<AudioBuffer> _action;
        private CircularSampleBuffer _sampleBuffer;

        public IEnumerable<float> Update()
        {
            if (_outBuffer!=null)
            {
                return _outBuffer;
            }            
            return Enumerable.Empty<float>();
        }

        public void SettingsChanged(AudioLink input, int count=512)
        {
            Stop();
            if (count == 0 || input == null)
            {
                return;
            }
            Connect(input);

            if (_outBuffer==null || _sampleBuffer==null || _sampleBuffer.MaxLength != count)
            {
                _sampleBuffer = new CircularSampleBuffer(_logger.Category, count)
                {
                    BufferFilled = data =>
                    {
                        Array.Copy(data, _outBuffer, count);
                    }
                };
                _outBuffer = new float[count];
            }
            
            if (_action == null)
            {
                _logger.Info($"Getting samples {count}");
                _action = new ActionBlock<AudioBuffer>(inp =>
                {
                    var written = _sampleBuffer.Write(inp.Data, 0, Math.Min(inp.Count, count));
                    _sampleBuffer.Advance(written);
                });
            }

            if (_link == null )
            {
                _logger.Info($"Setting link {input}");
                _link = input.SourceBlock.LinkTo(_action);
            }
        }

        public void Stop()
        {
            _link?.Dispose();
            Connect(null);
            _action = null;
            _outBuffer = null;
            _link = null;
            _sampleBuffer = null;
        }
        
        public override void Dispose()
        {
            Stop();
            base.Dispose();
        }
    }
}