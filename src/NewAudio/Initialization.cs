using System;
using System.Collections.Immutable;
using System.Linq;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lib.Basics.Resources;
using VL.NewAudio;
using VL.NewAudio.Backend;
using VL.NewAudio.Core;
using VL.NewAudio.Processor;
using VL.NewAudio.Sources;
using Xt;

[assembly: AssemblyInitializer(typeof(Initialization))]

namespace VL.NewAudio
{
    public static class Resources
    {
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
    }

    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            factory.RegisterNodeFactory(
                NodeBuilding.NewNodeFactory(factory, "NewAudio.Factory", f => NodeBuilding.NewFactoryImpl(
                    CoreNodes.GetNodeDescriptions(f)
                        .Concat(ProcessorNodes.GetNodeDescriptions(f))
                        .Concat(SourceNodes.GetNodeDescriptions(f)).ToImmutableArray())
                ));


            factory.RegisterService<NodeContext, IResourceProvider<IAudioService>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("IAudioService",
                    _ =>
                    {
                        var service = new XtAudioService();
                        service.ScanForDevices();
                        return service;
                    }, 0).Finally(a => { a.Dispose(); });
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