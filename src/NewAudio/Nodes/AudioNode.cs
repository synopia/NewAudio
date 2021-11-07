using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using Serilog;
using VL.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Nodes
{
    public class AudioNodeCreateParams: AudioParams
    {
    }
    public class AudioNodeUpdateParams: AudioParams
    {
        public AudioParam<bool> Playing;
        public AudioParam<AudioLink> Input;
    }

    public interface IAudioNode : IDisposable, ILifecycleDevice
    {
        public AudioNodeCreateParams CreateParams { get; }
        public AudioNodeUpdateParams UpdateParams { get; }
        public string DebugInfo();
        public IEnumerable<string> ErrorMessages();

    }
    public abstract class AudioNode<TCreate, TUpdate>: IAudioNode where TCreate: AudioNodeCreateParams where TUpdate: AudioNodeUpdateParams
    {
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<AudioNode<TCreate, TUpdate>>();
        
        private List<Exception> _exceptions = new List<Exception>();
        
        public TCreate CreateParams { get; }
        public TUpdate UpdateParams { get; }
        AudioNodeCreateParams IAudioNode.CreateParams => CreateParams;
        AudioNodeUpdateParams IAudioNode.UpdateParams => UpdateParams;
        public AudioLink Output { get; } = new AudioLink();

        public BufferBlock<AudioDataMessage> InputBufferBlock { get; } = new BufferBlock<AudioDataMessage>(
            new DataflowBlockOptions
            {
                BoundedCapacity = 16
            });

        private IDisposable _inputLink;
        
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine Lifecycle;
        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Lifecycle = new LifecycleStateMachine(this);
            CreateParams = (TCreate)Activator.CreateInstance(typeof(TCreate));
            UpdateParams = (TUpdate)Activator.CreateInstance(typeof(TUpdate));
        }

        public void ExceptionHappened(Exception exception, string method)
        {
            if (!_exceptions.Exists(e => e.Message == exception.Message))
            {
                _logger.Error("{e}", exception);
                _exceptions.Add(exception);
                // throw exception;
            }
        }

        protected AudioLink Update()
        {
            // TODO
            // if (BaseConfig.Reset)
            // {
                // if (Config.Input!=null)
                // {
                    // OnDisconnect(Config.Input);
                    // Config.Input = null;
                // }
            // }
            if (CreateParams.HasChanged)
            {
                CreateParams.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eCreate);
            }

            if (UpdateParams.Input.HasChanged)
            {
                if (_inputLink != null && UpdateParams.Input.LastValue != null)
                {
                    _inputLink.Dispose();
                    _inputLink = null;
                } else if (_inputLink != null || UpdateParams.Input.LastValue != null)
                {
                    _logger.Warning("Illegal input link found!");
                    _inputLink?.Dispose();
                    _inputLink = null;
                }

                if (UpdateParams.Input.Value != null)
                {
                    _inputLink = UpdateParams.Input.Value.SourceBlock.LinkTo(InputBufferBlock);
                }
            }

            if (UpdateParams.HasChanged)
            {
                UpdateParams.Commit();
                Lifecycle.EventHappens(UpdateParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            if (IsCreateValid() && IsUpdateValid() && UpdateParams.Playing.Value && Phase != LifecyclePhase.Playing)
            {
                Lifecycle.EventHappens(UpdateParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            return Output;
        }

        public virtual string DebugInfo()
        {
            return null;
        }

        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i=>i.Message);
        }

        public abstract Task<bool> Create();
        public abstract Task<bool> Free();
        public abstract bool Play();
        public abstract bool Stop();

        public virtual bool IsCreateValid()
        {
            return true;
        }
        public virtual bool IsUpdateValid()
        {
            return true;
        }

        private bool _disposedValue;
        
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            AudioService.Instance.Logger.Information("Dispose called for AudioNode {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AudioService.Instance.Graph.RemoveNode(this);
                    _inputLink?.Dispose();
                    Output.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}