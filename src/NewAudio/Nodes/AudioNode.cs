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
        public AudioParam<DataflowBlockOptions> InputOptions;
        public AudioParam<DataflowBlockOptions> OutputOptions;
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

        public void UpdateOptions(AudioDataflowOptions inputOptions, AudioDataflowOptions outputOptions)
        {
            if (inputOptions is { HasChanged: true })
            {
                PlayParams.InputOptions.Value = inputOptions.GetAudioDataflowOptions();
                inputOptions.Commit();

            }

            if (outputOptions is { HasChanged: true })
            {
                PlayParams.OutputOptions.Value = outputOptions.GetAudioDataflowOptions();
                outputOptions.Commit();
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
            if (PlayParams.HasChanged)
            {
                if (PlayParams.InputOptions.HasChanged)
                {
                    PlayParams.InputOptions.Commit();
                    InputBufferBlock.SetBlockOptions(PlayParams.InputOptions.Value);
                }

                if (PlayParams.OutputOptions.HasChanged)
                {
                    PlayParams.OutputOptions.Commit();
                    Output.TargetBlock.SetBlockOptions(PlayParams.OutputOptions.Value);
                }
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
            }

            if (IsInitValid() && IsPlayValid() && PlayParams.Playing.Value && Phase != LifecyclePhase.Play)
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
                    AudioService.Instance.Graph.RemoveNode(this);
                    _inputLink?.Dispose();
                    Output.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}