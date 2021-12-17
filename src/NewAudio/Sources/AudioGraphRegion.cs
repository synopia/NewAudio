using System;
using VL.NewAudio.Core;
using VL.NewAudio.Internal;
using VL.NewAudio.Nodes;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Sources
{
    public struct SampleIterator
    {
        private int _framePos;

        public SampleIterator(int framePos)
        {
            _framePos = framePos;
        }
    }

    public class AudioGraphRegion<TState> : AudioSourceNode where TState : class
    {
        private object _lock = new();
        private readonly AudioGraph _graph = new();
        private readonly AudioGraphIOProcessor _graphIn = new(false);
        private readonly AudioGraphIOProcessor _graphOut = new(true);
        private readonly AudioSampleFrameClock _clock = new();

        private int _sampleRate;
        private int _framesExpected;
        private AudioGraph.Node? _outputNode;
        private AudioGraph.Node? _inputNode;

        private TState? _state;
        private AudioProcessorNode<AudioGraphIOProcessor>? _input1;
        private AudioProcessorNode<AudioGraphIOProcessor>? _output;


        public AudioGraphRegion()
        {
            _graph.SetChannels(2, 2);
        }

        public override void PrepareToPlay(int sampleRate, int framesPerBlockExpected)
        {
            _sampleRate = sampleRate;
            _framesExpected = framesPerBlockExpected;
        }

        public override void ReleaseResources()
        {
        }

        public override void GetNextAudioBlock(AudioSourceChannelInfo bufferToFill)
        {
            using var s = new ScopedMeasure("AudioGraphRegion.GetNextAudioBlock");
            _graph.Process(bufferToFill.Buffer);
        }

        public AudioProcessor Update(AudioLink? input, Func<TState> create,
            Func<TState, AudioLink, Tuple<TState, AudioLink>> update)
        {
            lock (_lock)
            {
                var lastGraph = AudioGraph.CurrentGraph;
                AudioGraph.CurrentGraph = _graph;

                if (_state == null)
                {
                    _state = create();
                    _input1 ??= new AudioProcessorNode<AudioGraphIOProcessor>(_graphIn);
                    _output ??= new AudioProcessorNode<AudioGraphIOProcessor>(_graphOut);
                }

                // _inputNode ??= _graph.AddNode(_graphIn);
                // _outputNode ??= _graph.AddNode(_graphOut)!;

                var res = update(_state!, _input1!.Output);
                _state = res.Item1;
                // res.Item2;

                _output!.Input = res.Item2;

                AudioGraph.CurrentGraph = lastGraph;
                return _graph;
            }
        }
    }
}