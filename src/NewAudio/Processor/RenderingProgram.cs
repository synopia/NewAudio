using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using NewAudio.Dsp;

namespace NewAudio.Processor
{
    public class RenderingProgram
    {
        public int NumBuffersNeeded;
        public AudioBuffer RenderingBuffer = new ();
        public AudioBuffer CurrentOutputBuffer= new ();
        public AudioBuffer? CurrentInputBuffer;
        private List<IRenderingOperation> _renderingOperations = new ();
        public string ToCode=>string.Join("\n", _renderingOperations.Select(i=>i.ToCode()));
        
        struct Context
        {
            public AudioPlayHead PlayHead;
            public int NumFrames;
            public Memory<float>[] AudioBuffers;
        }

        interface IRenderingOperation
        {
            void Perform(Context context);
            string ToCode();
        }

        public void Perform(AudioBuffer buffer, AudioPlayHead playHead)
        {
            var numFrames = buffer.NumberOfFrames;
            var maxFrames = RenderingBuffer.NumberOfFrames;

            if (numFrames > maxFrames)
            {
                int chunkStart = 0;
                while (chunkStart < numFrames)
                {
                    var chunkSize = Math.Min(maxFrames, numFrames - chunkStart);
                    AudioBuffer audioChunk = new AudioBuffer(buffer.GetWriteChannels(), buffer.NumberOfChannels,
                        chunkStart, chunkSize);
                    Perform(audioChunk, playHead);
                    chunkStart += maxFrames;
                }
                return;
            }

            CurrentInputBuffer = buffer;
            CurrentOutputBuffer.SetSize(Math.Max(1, buffer.NumberOfChannels), numFrames);
            CurrentOutputBuffer.Zero();

            var ctx = new Context()
            {
                    NumFrames                = numFrames,
                    AudioBuffers = RenderingBuffer.GetWriteChannels()
            };
            foreach (var operation in _renderingOperations)
            {
                operation.Perform(ctx);
            }

            for (int i = 0; i < buffer.NumberOfChannels; i++)
            {
                buffer.CopyFrom(i, 0, CurrentOutputBuffer, i, 0, numFrames);
            }

            CurrentInputBuffer = null;
        }

        public void Reset()
        {
            
        }
        public  void AddClearChannelOp(int index)
        {
            CreateOp($"clear({index})", (ctx)=>ctx.AudioBuffers[index].Span.Fill(0, ctx.NumFrames));
        }
        public  void AddCopyChannelOp(int source, int target)
        {
            CreateOp($"copy({source}, {target})", (ctx)=>ctx.AudioBuffers[target].Span.CopyFrom(ctx.AudioBuffers[source].Span,ctx.NumFrames));
        }
        public  void AddAddChannelOp(int source, int target)
        {
            CreateOp($"add({source}, {target})", (ctx)=>ctx.AudioBuffers[target].Span.Add(ctx.AudioBuffers[source].Span,ctx.NumFrames));
        }
        public  void AddDelayChannelOp(int channel, int delaySize)
        {
            _renderingOperations.Add(new DelayChannelOp(channel, delaySize));
        }
        public void AddProcessOp(AudioGraph.Node node, List<int> channelsUsed, int totalChannels)
        {
            _renderingOperations.Add(new ProcessOp(node, channelsUsed, totalChannels));
        }

        public void PrepareBuffers(int frames)
        {
            RenderingBuffer.SetSize(NumBuffersNeeded+1, frames);
            RenderingBuffer.Zero();
            CurrentOutputBuffer.SetSize(NumBuffersNeeded+1, frames);
            CurrentOutputBuffer.Zero();

            CurrentInputBuffer = null;
        }

        public void ReleaseBuffers()
        {
            RenderingBuffer.SetSize(1,1);
            CurrentOutputBuffer.SetSize(1,1);
            CurrentInputBuffer = null;
            
        }

        delegate void PerformOpDelegate(Context ctx);

        private void CreateOp(string code,PerformOpDelegate op)
        {
            _renderingOperations.Add(new PerformOp(code, op));
        }
        
        private struct PerformOp: IRenderingOperation
        {
            private PerformOpDelegate _delegate;
            private string _code;
            public string ToCode()
            {
                return _code;
            }

            public PerformOp(string code, PerformOpDelegate @delegate)
            {
                _code = code;
                _delegate = @delegate;
            }

            public void Perform(Context context)
            {
                _delegate.Invoke(context);
            }
        }
        
        private struct DelayChannelOp: IRenderingOperation
        {
            private int _channel;
            private int _frames;
            private int _bufferSize;
            private int _writeIndex;
            private int _readIndex;
            private float[] _buffer;

            public string ToCode()
            {
                return $"delay({_channel}, {_bufferSize})";
            }

            public DelayChannelOp(int channel, int delaySize) : this()
            {
                _channel = channel;
                _bufferSize = delaySize + 1;
                _writeIndex = delaySize;

                _buffer = ArrayPool<float>.Shared.Rent(_bufferSize);
            }

            public void Perform(Context context)
            {
                var data = context.AudioBuffers[_channel].Span;
                int p = 0;
                for (int i = context.NumFrames; --i >= 0; )
                {
                    _buffer[_writeIndex] = data[p];
                    data[p++] = _buffer[_readIndex];

                    if (++_readIndex >= _bufferSize)
                    {
                        _readIndex = 0;
                    }

                    if (++_writeIndex >= _bufferSize)
                    {
                        _writeIndex = 0;
                    }
                }
            }
        }
        
        private struct ProcessOp: IRenderingOperation
        {
            private AudioGraph.Node _node;
            private AudioProcessor _processor;
            private List<int> _channelsToUse;
            private Memory<float>[] _channels;
            private int _totalChannels;

            public string ToCode()
            {
                return $"{_node.Processor.Name}.process({_node.NodeId}, [{string.Join(",", _channelsToUse)}])";
            }

            public ProcessOp(AudioGraph.Node node, List<int> channelsToUse, int totalChannels) : this()
            {
                _node = node;
                _channelsToUse = channelsToUse;
                _totalChannels = totalChannels;
                _processor = node.Processor;

                _channels = new Memory<float>[totalChannels];
                while (channelsToUse.Count<totalChannels)
                {
                    channelsToUse.Add(0);
                }
            }

            public void Perform(Context context)
            {
                _processor.PlayHead = context.PlayHead;
                for (int i = 0; i < _totalChannels; i++)
                {
                    _channels[i] = context.AudioBuffers[_channelsToUse[i]];
                }

                AudioBuffer buffer = new(_channels, _totalChannels, context.NumFrames);
                if (_processor.SuspendProcessing)
                {
                    buffer.Zero();
                }
                else
                {
                    CallProcess(buffer);
                }
            }

            private void CallProcess(AudioBuffer buffer)
            {
                if (_node.IsBypassed)
                {
                    _node.ProcessBypassed(buffer);
                }
                else
                {
                    _node.Process(buffer);
                }
            }
        }
    }
}