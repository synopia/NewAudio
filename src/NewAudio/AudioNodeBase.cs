using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using NAudio.Wave;
using NewAudio.Internal;
using VL.Core;

namespace NewAudio
{
    public abstract class AudioNodeProducer : IDisposable
    {
        public readonly AudioLink Output = new AudioLink();

        
        public virtual void Dispose()
        {
            Output.Dispose();
        }
    }

    public interface IAudioNodeConsumer : IDisposable
    {
        public AudioLink Input { get; }

        public void Connect(AudioLink input);
    }

    public abstract class AudioNodeConsumer : IAudioNodeConsumer
    {
        private AudioLink _input;
        public AudioLink Input => _input;
        public virtual void Connect(AudioLink input)
        {
            _input = input;
        }

        public virtual void Dispose()
        {
        }
    }
    public abstract class AudioNodeTransformer : AudioNodeProducer, IAudioNodeConsumer
    {
        private AudioLink _input;
        public AudioLink Input => _input;
        public void Connect(AudioLink input)
        {
            _input = input;
        }
    }

    public abstract class AudioNodeInput : AudioNodeProducer
    {
    }

    public abstract class AudioNodeSink : AudioNodeConsumer
    {
    }
}