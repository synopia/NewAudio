using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Processor;
using NewAudio.Core;
using Serilog;
using VL.Core;
using VL.Lang.Helper;
using VL.Lib.Basics.Resources;
using VL.Lib.Collections;
using VL.Model;
using NewAudio;

namespace NewAudio.Nodes
{
    public abstract class AudioNode: IDisposable
    {
        public IAudioService AudioService { get; } 
        
        private readonly IResourceHandle<AudioGraph> _graph;
        public AudioGraph Graph => _graph.Resource;
        protected ILogger Logger { get; private set; }
        public abstract string NodeName { get; }

        public readonly AudioLink Output = new();
        private ulong _lastException;

        private AudioProcessor _audioProcessor;
        protected AudioProcessor AudioProcessor
        {
            get => _audioProcessor;
            set
            {
                if (value == _audioProcessor)
                {
                    return;
                }

                IList<AudioProcessor> inputs = null;
                if (_audioProcessor != null)
                {
                    inputs = _audioProcessor.Inputs;
                    _audioProcessor.Dispose();
                    _audioProcessor = null;
                }
                if (value != null)
                {
                    _audioProcessor = value;
                    if (_audioProcessor is not OutputProcessor)
                    {
                        _audioProcessor.Connect(Output.Pin);
                    }

                    if (inputs != null)
                    {
                        foreach (var input in inputs)
                        {
                            input.Connect(_audioProcessor);
                        }
                    }
                }
            }
        }
        public int Id { get; }

        private List<Exception> _exceptions = new();

        protected AudioNode()
        {
            AudioService = Resources.GetAudioService();
            _graph = Resources.GetAudioGraph();
        }

        protected void InitLogger<T>()
        {
            Logger = Resources.GetLogger<T>();
            Logger.Information("Started AudioNode {Name}", NodeName);

        }

        public bool InExceptionTimeOut()
        {
            return VLSession.Instance.UserRuntime.Frame - _lastException < 30;
        }
        
        public void ExceptionHappened(Exception exception, string method)
        {
            _lastException = VLSession.Instance.UserRuntime.Frame;
            
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                Logger.Error(exception, "Exceptions happened in {This}.{Method}", this, method);
                _exceptions.Add(exception);
                throw exception;
            }
        }

        public virtual string DebugInfo()
        {
            return null;
        }
        
        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i => i.Message);
        }

        
        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        public override string ToString()
        {
            return NodeName;
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioNode {This} ({Disposing})", NodeName, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _graph.Dispose();
                }

                _disposedValue = true;
            }
        }

    }
}