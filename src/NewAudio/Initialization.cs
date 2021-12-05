using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
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
        private static ILogger? _logger;

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
            return _logger ??= new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                .MinimumLevel.Debug()
                .Destructure.ByTransforming<XtDeviceStreamParams>(r => new { r.format, r.bufferSize, r.stream })
                .Destructure.ByTransforming<AudioStreamConfig>(r => new
                    { r.Interleaved, r.AudioDevice.Caps.Name, r.BufferSize, r.SampleRate, r.SampleType, r.IsValid })
                .CreateLogger();
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

        public static IResourceHandle<AudioGraph> GetAudioGraph()
        {
            var provider =
                NodeContext.Current.Factory.CreateService<IResourceProvider<AudioGraph>>(NodeContext.Current);
            return provider.GetHandle();
        }
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
                    f.NewNode(ctor: ctx =>new AudioDeviceNode(), category: category, name: "AudioDevice", hasStateOutput:false)
                        .AddInput(nameof(AudioDeviceNode.Device), x=>x.Device, (x,v)=>x.Device=v,defaultValue:default)

                        .AddOutput(nameof(AudioDeviceNode.Name), x=>x.Name)
                        .AddOutput(nameof(AudioDeviceNode.System), x=>x.System)
                        .AddOutput(nameof(AudioDeviceNode.AvailableInputChannels), x=>x.AvailableInputChannels)
                        .AddOutput(nameof(AudioDeviceNode.AvailableOutputChannels), x=>x.AvailableOutputChannels)
                        .AddOutput(nameof(AudioDeviceNode.AvailableSampleFrequencies), x=>x.AvailableSampleFrequencies)
                        .AddOutput(nameof(AudioDeviceNode.AvailableSampleTypes), x=>x.AvailableSampleTypes)
                        .AddOutput(nameof(AudioDeviceNode.MinBufferSizeMs), x=>x.MinBufferSizeMs)
                        .AddOutput(nameof(AudioDeviceNode.MaxBufferSizeMs), x=>x.MaxBufferSizeMs)
                        .AddOutput(nameof(AudioDeviceNode.InputChannelNames), x=>x.InputChannelNames)
                        .AddOutput(nameof(AudioDeviceNode.OutputChannelNames), x=>x.OutputChannelNames)
                        .AddOutput(nameof(AudioDeviceNode.AudioDevice), x=>x.AudioDevice)
                        .WithEnabledPins(),
                    f.NewNode(_=>new AudioStreamConfigNode(), category: category, name: "AudioStreamConfig", hasStateOutput:false)
                        .AddInput(nameof(AudioStreamConfigNode.AudioDevice), x=>x.AudioDevice, (x,v)=>x.AudioDevice=v)
                        .AddListInput(nameof(AudioStreamConfigNode.InputChannels), x=>x.InputChannels, (x,v)=>x.InputChannels=v)
                        .AddListInput(nameof(AudioStreamConfigNode.OutputChannels), x=>x.OutputChannels, (x,v)=>x.OutputChannels=v)
                        .AddInput(nameof(AudioStreamConfigNode.SamplingFrequency), x=>x.SamplingFrequency, (x,v)=>x.SamplingFrequency=v, defaultValue:SamplingFrequency.Hz44100)
                        .AddInput(nameof(AudioStreamConfigNode.BufferSize), x=>x.BufferSize, (x,v)=>x.BufferSize=v, defaultValue:0)
                        .AddOutput(nameof(AudioStreamConfigNode.Config), x=>x.Config)
                        .WithEnabledPins(),
                    f.NewNode(_=>new AudioStreamNode(), category:category, name: "AudioStream", hasStateOutput:false)
                        .AddInput(nameof(AudioStreamNode.Primary), x=>x.Primary, (x,v)=>x.Primary=v)
                        .AddListInput(nameof(AudioStreamNode.Secondary), x=>x.Secondary, (x,v)=>x.Secondary=v)
                        .AddOutput(nameof(AudioStreamNode.InputLatency), x=>x.InputLatency)
                        .AddOutput(nameof(AudioStreamNode.OutputLatency), x=>x.OutputLatency)
                        .AddOutput(nameof(AudioStreamNode.Type), x=>x.Type)
                        
                        .WithEnabledPins(),

                    f.NewProcessorNode(_=>new AudioGraphIOProcessor(false), category:category, name:"Input", hasAudioInput:false, hasAudioOutput:true, hasStateOutput:false)
                        .WithEnabledPins(),
                    f.NewProcessorNode(_=>new AudioGraphIOProcessor(true), category:category, name:"Output", hasAudioInput:true, hasAudioOutput:false, hasStateOutput:false)
                        .WithEnabledPins(),
                    f.NewProcessorNode(_=>new NoiseGenProcessor(), category:category, name: "Noise", hasAudioInput:false, hasAudioOutput:true, hasStateOutput:false)
                        .WithEnabledPins(),
                    f.NewProcessorNode(_=>new SineGenProcessor(), category:category, name: "Sine", hasAudioInput:false, hasAudioOutput:true, hasStateOutput:false)
                        .AddInput(nameof(SineGenProcessor.Freq), x=>x.Processor.Freq, (x,v)=>x.Processor.Freq=v)
                        .WithEnabledPins(),
                    f.NewProcessorNode(_=>new MultiplyProcessor(), category:category, name: "*", hasAudioInput:true, hasAudioOutput:true, hasStateOutput:false)
                        .AddInput(nameof(SineGenProcessor.Freq), x=>x.Processor.Value, (x,v)=>x.Processor.Value=v)
                        .WithEnabledPins(),
                    f.NewNode(_=>new MonitorNode(), category:category, update:x=>x.FillBuffer(),name: "Monitor", hasStateOutput:false)
                        .AddInput("Audio In", x=>x.Input, (x,v)=>x.Input=v)
                        .AddInput(nameof(MonitorNode.BufferSize), x=>x.BufferSize, (x,v)=>x.BufferSize=v)
                        .AddOutput(nameof(MonitorNode.Buffer), x=>x.Buffer)
                }.ToImmutableArray());
            }));

            // There can be only one XtAudio instance. Should be used by all opened VL documents/apps
            factory.RegisterService<string, IResourceProvider<XtPlatform>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("NewAudio.XtPlatform",
                    (_) => XtAudio.Init("NewAudio", IntPtr.Zero)).Finally(a =>
                {
                    a.Dispose();
                });
            });
            
            factory.RegisterService<NodeContext, IResourceProvider<IAudioService>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("IAudioService",
                    factory: (key) =>
                    {
                        var p = context.Factory.CreateService<IResourceProvider<XtPlatform>>("NewAudio.XtPlatform");
                        return new XtAudioService(p.GetHandle().Resource); // AudioServiceThread(logger, p);
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });

            factory.RegisterService<NodeContext, IResourceProvider<AudioGraph>>(context =>
            {
                return ResourceProvider.NewPooledPerApp(context, factory: () => new AudioGraph());
            });
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