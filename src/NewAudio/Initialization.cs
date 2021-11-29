using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lang.Platforms;
using VL.Lang.Symbols;
using VL.Lib.Basics.Resources;
using VL.Model;
using Xt;

[assembly: AssemblyInitializer(typeof(NewAudio.Initialization))]

namespace NewAudio
{
    public static class Resources
    {
        private static Func<IXtPlatform> _platform;
        private static IAudioService _audioService;
        private static AudioGraph _audioGraph;
        private static ILogger _logger;

        /// <summary>
        /// Only used for testing purpose
        /// </summary>
        public static void SetResources(Func<IXtPlatform> platform, AudioGraph graph = null)
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
        }

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
            if (_audioService != null)
            {
                return _audioService;
            }

            var provider =
                NodeContext.Current.Factory.CreateService<IResourceProvider<IAudioService>>(NodeContext.Current);
            return provider.GetHandle().Resource;
        }

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
    }

    public sealed class Initialization : AssemblyInitializer<Initialization>
    {
        protected override void RegisterServices(IVLFactory factory)
        {
            factory.RegisterNodeFactory(NodeBuilding.NewNodeFactory(factory, "NewAudio.Factory", f =>
            {
                return NodeBuilding.NewFactoryImpl(new IVLNodeDescription[]
                {
                    new AudioNodeDesc<AudioGenerator>(f, ctor: ctx =>
                    {
                        var i = new AudioGenerator();
                        return (i, () => i.Dispose());
                    }, category: "NewAudio.Nodes", name: "AService", hasStateOutput: true),
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
            factory.RegisterService<string, IResourceProvider<IXtPlatform>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("NewAudio.XtPlatform",
                    factory: (key) =>
                    {
                        return new RPlatform(XtAudio.Init("NewAudio", IntPtr.Zero));
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });
            factory.RegisterService<NodeContext, IResourceProvider<IAudioService>>(context =>
            {
                return ResourceProvider.NewPooledSystemWide("NewAudioThread",
                    factory: (key) =>
                    {
                        var logger = context.Factory.CreateService<IResourceProvider<ILogger>>(context).GetHandle()
                            .Resource;
                        var p = context.Factory.CreateService<IResourceProvider<IXtPlatform>>("NewAudio.XtPlatform");
                        return new AudioServiceThread(logger, p);
                    }, delayDisposalInMilliseconds: 0).Finally(a => { a.Dispose(); });
            });

            // One AudioGraph per app. TODO maybe make AudioGraph a VL node, that needs a connected output node  
            factory.RegisterService<NodeContext, IResourceProvider<AudioGraph>>(context =>
            {
                return ResourceProvider.NewPooledPerApp(context,
                        factory: () => { return new AudioGraph(); }, delayDisposalInMilliseconds: 0)
                    .Finally(ag => { ag.Dispose(); });
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