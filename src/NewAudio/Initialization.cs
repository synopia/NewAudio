using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Nodes;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lang.Platforms;
using VL.Lang.Symbols;
using VL.Lib.Basics.Resources;
using VL.Model;
using VL.NewAudio.Nodes;
using Xt;
using SamplingFrequency = NewAudio.Device.SamplingFrequency;

[assembly: AssemblyInitializer(typeof(NewAudio.Initialization))]

namespace NewAudio
{
    public static class Resources
    {
        private static Func<IXtPlatform> _platform;
        // private static IAudioService _audioService;
        // private static AudioGraph _audioGraph;
        private static ILogger _logger;

        /// <summary>
        /// Only used for testing purpose
        /// </summary>
        /*public static void SetResources(Func<IXtPlatform> platform, AudioGraph graph = null)
        {
            _logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .MinimumLevel.Debug()
                .CreateLogger();
            _platform = platform;
            _audioService = new AudioServiceThread(_logger, ResourceProvider.New(()=>platform.Invoke()));
            _audioGraph = graph ?? new AudioGraph();
        }*/

        public static ILogger GetLogger<T>()
        {
            if (_logger != null)
            {
                // ReSharper disable once ContextualLoggerProblem
                return _logger.ForContext<T>();
            }

            var provider = NodeContext.Current.Factory.CreateService<IResourceProvider<ILogger>>(NodeContext.Current);
            var logger = provider.GetHandle().Resource;
            // ReSharper disable once ContextualLoggerProblem
            return logger.ForContext<T>();
        }

        public static IAudioService GetAudioService()
        {
            /*
            if (_audioService != null)
            {
                return _audioService;
            }
            */

            var provider =
                NodeContext.Current.Factory.CreateService<IResourceProvider<IAudioService>>(NodeContext.Current);
            return provider.GetHandle().Resource;
        }
        /*

        public static IResourceHandle<AudioGraph> GetAudioGraph()
        {
            if (_audioGraph != null)
            {
                return new TestResourceHandle<AudioGraph>(_audioGraph);
            }

            var provider =
                NodeContext.Current.Factory.CreateService<IResourceProvider<AudioGraph>>(NodeContext.Current);
            return provider.GetHandle();
        }
    */
    }

    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            var category = "NewAudio.Nodes";
            factory.RegisterNodeFactory(NodeBuilding.NewNodeFactory(factory, "NewAudio.Factory", f =>
            {
                return NodeBuilding.NewFactoryImpl(new IVLNodeDescription[]
                {
                    f.NewNode(ctor: ctx =>new AudioDeviceNode(), category: category, name: "AudioDevice", hasStateOutput:true)
                        .AddCachedInput(nameof(AudioDeviceNode.Device), x=>x.Device, (x,v)=>x.Device=v,defaultValue:default)

                        .AddOutput(nameof(AudioDeviceNode.Name), x=>x.Name)
                        .AddOutput(nameof(AudioDeviceNode.System), x=>x.System)
                        .AddOutput(nameof(AudioDeviceNode.AvailableInputChannels), x=>x.AvailableInputChannels)
                        .AddOutput(nameof(AudioDeviceNode.AvailableOutputChannels), x=>x.AvailableOutputChannels)
                        .AddOutput(nameof(AudioDeviceNode.AvailableSampleFrequencies), x=>x.AvailableSampleFrequencies)
                        .AddOutput(nameof(AudioDeviceNode.AvailableSampleTypes), x=>x.AvailableSampleTypes)
                        .AddOutput(nameof(AudioDeviceNode.MinBufferSizeMs), x=>x.MinBufferSizeMs)
                        .AddOutput(nameof(AudioDeviceNode.MaxBufferSizeMs), x=>x.MaxBufferSizeMs)
                        .AddOutput(nameof(AudioDeviceNode.InputChannelNames), x=>x.InputChannelNames)
                        .AddOutput(nameof(AudioDeviceNode.OutputChannelNames), x=>x.OutputChannelNames),
                    f.NewNode(_=>new AudioSessionNode(), category:category, name:"AudioSession", hasStateOutput:false)
                        .AddCachedInput(nameof(AudioSessionNode.Input), x=>x.Input, (x,v)=>x.Input=v, defaultValue:null)
                        .AddCachedInput(nameof(AudioSessionNode.InputDevice), x=>x.InputDevice, (x,v)=>x.InputDevice=v, defaultValue:null)
                        .AddCachedInput(nameof(AudioSessionNode.OutputDevice), x=>x.OutputDevice, (x,v)=>x.OutputDevice=v, defaultValue:null)
                        .AddCachedListInput(nameof(AudioSessionNode.InputChannels), x=>x.InputChannels, (x,v)=>x.InputChannels=v)
                        .AddCachedListInput(nameof(AudioSessionNode.OutputChannels), x=>x.OutputChannels, (x,v)=>x.OutputChannels=v)
                        .WithEnabledPin(),
                    f.NewProcessorNode(_=>new NoiseGenProcessor(), category:category, name: "Noise", hasAudioInput:false, hasAudioOutput:true, hasStateOutput:false)
                        .WithEnabledPin(),
                    f.NewProcessorNode(_=>new SineGenProcessor(), category:category, name: "Sine", hasAudioInput:false, hasAudioOutput:true, hasStateOutput:false)
                        .AddInput(nameof(SineGenProcessor.Freq), x=>x.Processor.Freq, (x,v)=>x.Processor.Freq=v)
                        .WithEnabledPin(),
                    f.NewProcessorNode(_=>new MultiplyProcessor(), category:category, name: "*", hasAudioInput:true, hasAudioOutput:true, hasStateOutput:false)
                        .AddInput(nameof(SineGenProcessor.Freq), x=>x.Processor.Value, (x,v)=>x.Processor.Value=v)
                        .WithEnabledPin(),
                }.ToImmutableArray());
            }));
            

            factory.RegisterService<NodeContext, IResourceProvider<ILogger>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("Logger",
                    factory: (key) =>
                    {
                        var logger = new LoggerConfiguration()
                            .Enrich.WithThreadId()
                            .WriteTo.Console(new MessageTemplateTextFormatter(
                                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                            .WriteTo.Seq("http://localhost:5341")
                            .MinimumLevel.Debug()
                            .CreateLogger();
                        return logger;
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });

            // There can be only one XtAudio instance. Should be used by all opened VL documents/apps
            factory.RegisterService<string, IResourceProvider<XtPlatform>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("NewAudio.XtPlatform",
                    factory: (key) =>
                    {
                        return XtAudio.Init("NewAudio", IntPtr.Zero);
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });
            
            factory.RegisterService<NodeContext, IResourceProvider<IAudioService>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("IAudioService",
                    factory: (key) =>
                    {
                        // var logger = context.Factory.CreateService<IResourceProvider<ILogger>>(context).GetHandle()
                            // .Resource;
                        var p = context.Factory.CreateService<IResourceProvider<XtPlatform>>("NewAudio.XtPlatform");
                        return new XtAudioService(p.GetHandle().Resource); // AudioServiceThread(logger, p);
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });
            

            // One AudioGraph per app. TODO maybe make AudioGraph a VL node, that needs a connected output node  
            /*
            factory.RegisterService<NodeContext, IResourceProvider<AudioGraph>>(context =>
            {
                return ResourceProvider.NewPooledPerApp(context,
                        factory: () => { return new AudioGraph(); }, delayDisposalInMilliseconds: 0)
                    .Finally(ag => { ag.Dispose(); });
            });
            */

        }
    }

    public class TestResourceHandle<T> : IResourceHandle<T>
    {
        public TestResourceHandle(T resource)
        {
            Resource = resource;
        }

        public void Dispose()
        {
            if (Resource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        public T Resource { get; }
    }
}