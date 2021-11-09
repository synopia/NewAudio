using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using NewAudio.Core;
using Serilog;

namespace NewAudio.Nodes
{
    public class AudioNodeInitParams: AudioParams
    {

    }
    public class AudioNodePlayParams: AudioParams
    {
        public AudioParam<bool> Playing;
        public AudioParam<AudioLink> Input;
    }

    public interface IAudioNode : IDisposable, ILifecycleDevice
    {
        public AudioNodeInitParams InitParams { get; }
        public AudioNodePlayParams PlayParams { get; }
        public string DebugInfo();
        public IEnumerable<string> ErrorMessages();

    }
    public abstract class AudioNode<TInit, TPlay>: IAudioNode where TInit: AudioNodeInitParams where TPlay: AudioNodePlayParams
    {
        private readonly ILogger _logger = AudioService.Instance.Logger.ForContext<AudioNode<TInit, TPlay>>();
        
        private List<Exception> _exceptions = new();
        private AudioDataflowOptions InputOptions;
        private AudioDataflowOptions OutputOptions;
        public TInit InitParams { get; }
        public TPlay PlayParams { get; }
        AudioNodeInitParams IAudioNode.InitParams => InitParams;
        AudioNodePlayParams IAudioNode.PlayParams => PlayParams;
        public AudioLink Output { get; } = new();

        public DynamicBufferBlock InputBufferBlock { get; } = new();

        private IDisposable _inputLink;
        
        public LifecyclePhase Phase { get; set; }
        public readonly LifecycleStateMachine Lifecycle;

        protected AudioNode()
        {
            AudioService.Instance.Graph.AddNode(this);
            Lifecycle = new LifecycleStateMachine(this);
            InitParams = (TInit)Activator.CreateInstance(typeof(TInit));
            PlayParams = (TPlay)Activator.CreateInstance(typeof(TPlay));
            InputOptions = (AudioDataflowOptions)Activator.CreateInstance(typeof(AudioDataflowOptions));
            OutputOptions = (AudioDataflowOptions)Activator.CreateInstance(typeof(AudioDataflowOptions));
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

        public void UpdateInputOptions(int bufferCount, int maxBuffersPerTask, bool ensureOrdered)
        {
            InputOptions.UpdateAudioDataflowOptions(bufferCount, maxBuffersPerTask, ensureOrdered);
        }
        public void UpdateOutputOptions(int bufferCount, int maxBuffersPerTask, bool ensureOrdered)
        {
            OutputOptions.UpdateAudioDataflowOptions(bufferCount, maxBuffersPerTask, ensureOrdered);
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
            if (InputOptions.HasChanged)
            {
                InputOptions.Commit();
                InputBufferBlock.SetBlockOptions(InputOptions.DataflowBlockOptions);
            }

            if (OutputOptions.HasChanged)
            {
                OutputOptions.Commit();
                Output.TargetBlock.SetBlockOptions(OutputOptions.DataflowBlockOptions);
            }    
            if (PlayParams.HasChanged)
            {
               
            }
            if( InitParams.HasChanged){
                InitParams.Commit();
                Lifecycle.EventHappens(LifecycleEvents.eInit);
            }

            if (PlayParams.Input.HasChanged)
            {
                if (_inputLink != null && PlayParams.Input.LastValue != null)
                {
                    _inputLink.Dispose();
                    _inputLink = null;
                } else if (_inputLink != null || PlayParams.Input.LastValue != null)
                {
                    _logger.Warning("Illegal input link found!");
                    _inputLink?.Dispose();
                    _inputLink = null;
                }

                if (PlayParams.Input.Value != null)
                {
                    _inputLink = PlayParams.Input.Value.SourceBlock.LinkTo(InputBufferBlock);
                }
            }

            if (PlayParams.HasChanged)
            {
                PlayParams.Commit();
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            } else if (IsInitValid() && IsPlayValid() && PlayParams.Playing.Value && Phase != LifecyclePhase.Play)
            {
                Lifecycle.EventHappens(PlayParams.Playing.Value ? LifecycleEvents.ePlay : LifecycleEvents.eStop);
            }

            return Output;
        }

        public virtual string DebugInfo()
        {
            return  $"node:[ Input count={InputBufferBlock?.BufferUsage}, Output count={Output?.TargetBlock?.BufferUsage}]";
        }

        public IEnumerable<string> ErrorMessages()
        {
            return _exceptions.Select(i=>i.Message);
        }

        public abstract Task<bool> Init();
        public abstract Task<bool> Free();
        public abstract bool Play();
        public abstract bool Stop();

        public virtual bool IsInitValid()
        {
            return true;
        }
        public virtual bool IsPlayValid()
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
                    _inputLink?.Dispose();
                    Output.Dispose();
                    AudioService.Instance.Graph.RemoveNode(this);
                }

                _disposedValue = true;
            }
        }
    }
}