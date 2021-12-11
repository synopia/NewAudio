using System;
using System.Diagnostics;
using VL.NewAudio.Dsp;
using Serilog;

namespace VL.NewAudio.Processor
{
    public class AudioGraphIOProcessor : AudioProcessor
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
                    SetPlayConfig(outChannels, inChannels, SampleRate, FramesPerBlock);
                }
            }
        }

        public override string Name => _isOutput ? "Audio Output" : "Audio Input";
        private readonly bool _isOutput;

        public bool IsInput => !_isOutput;
        public bool IsOutput => _isOutput;

        public override bool IsBusStateSupported(AudioBusState layout)
        {
            return IsInput ? layout.MainInput.Count == 0 : layout.MainOutput.Count == 0;
        }

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
                for (var i = Math.Min(currentOutput.NumberOfChannels, buffer.NumberOfChannels); --i >= 0;)
                {
                    currentOutput.CopyFrom(i, 0, buffer, i, 0, buffer.NumberOfFrames);
                }
            }
            else
            {
                var currentInput = program.CurrentInputBuffer!;
                for (var i = Math.Min(currentInput.NumberOfChannels, buffer.NumberOfChannels); --i >= 0;)
                {
                    buffer.CopyFrom(i, 0, currentInput, i, 0, buffer.NumberOfFrames);
                }
            }
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlock)
        {
            Trace.Assert(_graph != null);
        }


        public override void ReleaseResources()
        {
        }
    }
}