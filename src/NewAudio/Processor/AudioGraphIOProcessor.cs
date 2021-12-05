using System;
using System.Diagnostics;
using NewAudio.Dsp;
using Serilog;

namespace NewAudio.Processor
{
    public class AudioGraphIOProcessor: AudioProcessor
    {
        private AudioGraph? _graph;

        public AudioGraph? ParentGraph
        {
            get => _graph;
            set
            {
                _graph = value;
                if (_graph != null)
                {
                    var outChannels = IsOutput ? _graph.TotalNumberOfOutputChannels : 0;
                    var inChannels = IsInput ? _graph.TotalNumberOfInputChannels : 0;
                    SetPlayConfig(inChannels, outChannels, SampleRate, FramesPerBlock );
                }
            }
        }

        public override string Name => _isOutput ? "Audio Output" : "Audio Input";
        private readonly bool _isOutput;

        public bool IsInput => !_isOutput;
        public bool IsOutput => _isOutput;


        public AudioGraphIOProcessor(bool isOutput)
        {
            _isOutput = isOutput;
        }

        public override void Process(AudioBuffer buffer)
        {
            var program = _graph!.Program;
            if (IsOutput)
            {
                var currentOutput = program.CurrentOutputBuffer;
                for (int i = Math.Min(currentOutput.NumberOfChannels, buffer.NumberOfChannels); --i >= 0;)
                {
                    currentOutput.CopyFrom(i,0,buffer, i,0,buffer.NumberOfFrames);
                }
            }
            else
            {
                var currentInput = program.CurrentInputBuffer!;
                for (int i = Math.Min(currentInput.NumberOfChannels, buffer.NumberOfChannels); --i >= 0;)
                {
                    buffer.CopyFrom(i, 0, currentInput, i, 0, buffer.NumberOfFrames);
                }
            }
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            Trace.Assert(_graph!=null);
        }

        
        public override void ReleaseResources()
        {
            
        }

    }
}