using System;
using NewAudio.Block;
using NewAudio.Core;
using Serilog;
using VL.Lang.Helper;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public abstract class AudioNode: IDisposable
    {
        private readonly IResourceHandle<AudioGraph> _graph;
        public AudioGraph Graph => _graph.Resource;
        protected ILogger Logger { get; private set; }
        public abstract string NodeName { get; }

        public readonly AudioLink Output = new();

        protected void InitLogger<T>()
        {
            Logger = Graph.GetLogger<T>();
        }


        public virtual string DebugInfo()
        {
            return null;
        }
        
        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioNode {This} ({Disposing})", this, disposing);
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