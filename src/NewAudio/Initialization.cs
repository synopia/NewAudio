using System;
using System.Diagnostics;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Core.CompilerServices;
using VL.Lang.Platforms;
using VL.Lang.Symbols;
using VL.Lib.Basics.Resources;
using VL.Model;
using Xt;

[assembly: AssemblyInitializer(typeof(VL.NewAudio.Initialization))]
namespace VL.NewAudio
{
  
    public static class Resources
    {
        private static IAudioService _audioService;
        private static AudioGraph _audioGraph;
        private static DeviceManager _deviceManager;
        private static ILogger _logger;

        /// <summary>
        /// Only used for testing purpose
        /// </summary>
        /// <param name="platform">The Xt implementation to use. Use TestPlatform to mock the whole audio system</param>
        public static void SetResources(IXtPlatform platform)
        {
            _logger =  new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .MinimumLevel.Debug()
                .CreateLogger();
            SetResources(new AudioService(platform));
        }
        public static void SetResources(IAudioService service, AudioGraph graph=null, DeviceManager deviceManager=null)
        {
            _logger =  new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .MinimumLevel.Debug()
                .CreateLogger();
            _audioService = service;
            _audioGraph = graph ?? new AudioGraph();
            _deviceManager = deviceManager ?? new DeviceManager();
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

            var provider = NodeContext.Current.Factory.CreateService<IResourceProvider<IAudioService>>(NodeContext.Current);
            return provider.GetHandle().Resource;
        }
        public static IResourceHandle<AudioGraph> GetAudioGraph()
        {
            if (_audioGraph != null)
            {
                return new TestResourceHandle<AudioGraph>(_audioGraph);
            }
            var provider = NodeContext.Current.Factory.CreateService<IResourceProvider<AudioGraph>>(NodeContext.Current);
            return provider.GetHandle();
        }
        public static IResourceHandle<DeviceManager> GetDeviceManager()
        {
            if (_deviceManager != null)
            {
                return new TestResourceHandle<DeviceManager>(_deviceManager);
            }
            var provider = NodeContext.Current.Factory.CreateService<IResourceProvider<DeviceManager>>(NodeContext.Current);
            return provider.GetHandle();
        }
        
    }
    public sealed class Initialization: AssemblyInitializer<Initialization>
    {
        
        protected override void RegisterServices(IVLFactory factory)
        {
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
                    },delayDisposalInMilliseconds:0).Finally(a =>
                {
                    a.Dispose();
                });
            });
            
            // There can be only one XtAudio instance. Should be used by all opened VL documents/apps
            factory.RegisterService<NodeContext, IResourceProvider<IAudioService>>(context =>
            {
                // return ResourceProvider.NewPooledPerApp(context,
                    // factory: () =>
                    // {
                        // var platform = new RPlatform(XtAudio.Init("NewAudio", IntPtr.Zero));
                        // var audioService = new AudioService(platform);
                        
                        // return audioService;
                    // },delayDisposalInMilliseconds:0).Finally(a =>
                // {
                    // a.Dispose();
                // });
                return ResourceProvider.NewPooledSystemWide("NewAudio",
                factory: (key) =>
                {
                var platform = new RPlatform(XtAudio.Init("NewAudio", IntPtr.Zero));
                XtAudio.SetOnError(platform.DoOnError);
                var audioService = new AudioService(platform);
                         
                return audioService;
                },delayDisposalInMilliseconds:0).Finally(a =>
                {
                a.Dispose();
                });
            });
            
            // One AudioGraph per app. TODO maybe make AudioGraph a VL node, that needs a connected output node  
            factory.RegisterService<NodeContext, IResourceProvider<AudioGraph>>(context =>
            {
                return ResourceProvider.NewPooledPerApp(context,
                    factory: () =>
                    {
                        return new AudioGraph();
                    },delayDisposalInMilliseconds:0).Finally(ag =>
                {
                    ag.Dispose();
                });
            });
            
            // One DeviceManager per AudioGraph/app. Keeps track of devices and their configuration used by the AudioGraph
            factory.RegisterService<NodeContext, IResourceProvider<DeviceManager>>(context =>
            {
                return ResourceProvider.NewPooledPerApp(context,
                    factory: () =>
                    {
                        return new DeviceManager();
                    },delayDisposalInMilliseconds:0).Finally(dm =>
                {
                    dm.Dispose();
                });
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