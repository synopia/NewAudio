using System;
using System.Collections.Generic;
using NewAudio.Core;
using Serilog;
using Serilog.Formatting.Display;
using VL.Core;
using VL.Lang.Symbols;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Nodes
{
    public class TestAudioNode: IDisposable
    {
        private IResourceHandle<AudioDrivers> _drivers;
        private IResourceHandle<AudioGraph2> _graph;
        private IResourceHandle<AudioDevice2> _device2;
        public TestAudioNode()
        {
            _drivers = AudioDrivers.GetGlobal();
            _device2  =_drivers.Resource.GetDevice("Test");
            _graph = AudioDrivers.GetGraph();
            _graph.Resource.Add(this);
            
        }

        public string Update()
        {
            return $"{_drivers.Resource.Id}, {_device2.Resource.Name}, graph={_graph.GetHashCode()}, {_graph.Resource}";
        }

        public void Dispose()
        {
            _graph.Resource.Remove(this);
            _graph.Dispose();
            _drivers.Resource.Logger.Information("TestAudioNode disposed");
            _device2.Dispose();
            _drivers.Dispose();
        }
    }

    public class AudioGraph2 : IDisposable
    {
        private ILogger _logger;
        private List<TestAudioNode> _nodes = new();

        public AudioGraph2(ILogger logger)
        {
            _logger = logger;
            _logger.Information("AudioGraph created");
        }

        public override string ToString()
        {
            return $"count={_nodes.Count}";
        }

        public void Add(TestAudioNode node)
        {
            _nodes.Add(node);
        }
        public void Remove(TestAudioNode node)
        {
            _nodes.Remove(node);
        }
        
        public void Dispose()
        {
            _logger.Information("AudioGraph disposed");
        }
    }
    
    public class AudioDevice2 : IDisposable
    {
        public ILogger Logger;
        public string Name;

        public AudioDevice2(ILogger logger)
        {
            Logger = logger;
            Logger.Information("AudioDevice created!");
        }

        public void Dispose()
        {
            Logger.Information("AudioDevice disposed!");
        }
    }
    public class AudioDrivers : IDisposable
    {
        public static ILogger SLogger = new LoggerConfiguration()
            .Enrich.WithThreadId()
            .WriteTo.Console(new MessageTemplateTextFormatter(
                "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
            .WriteTo.Seq("http://localhost:5341")
            // .WriteTo.File("NewAudio.log",
            // outputTemplate:
            // "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}")
            .MinimumLevel.Debug()
            .CreateLogger();
        
        public ILogger Logger;
        public readonly int Id = 0;
        public AudioDrivers(ILogger logger)
        {
            Logger = logger;
            Id = new Random().Next();
            VLSession.Instance.EditorRuntime.ModeChanged += ModeChanged;
            logger.Information("Hello {id}", Id);
        }


        public void ModeChanged(object obj, ModeChangedEventArgs arg)
        {
            Logger.Information("New mode: {mode}", arg.New);
        }
        public IResourceHandle<AudioDevice2> GetDevice(string name)
        {
            var pool = ResourceProvider.NewPooledSystemWide($"AudioDevice.{name}", s =>
            {
                var audioDevice2 = new AudioDevice2(Logger.ForContext<AudioDevice2>());
                audioDevice2.Name = $"{name} created by {Id}";
                return audioDevice2;
            });

            return pool.GetHandle();
        }
        
        public static IResourceHandle<AudioGraph2> GetGraph()
        {
            var pool = ResourceProvider.NewPooledPerApp(NodeContext.Current, "AudioGraph", s =>
            {
                return new AudioGraph2(SLogger);
            });
            return pool.GetHandle();
        }

        public static IResourceHandle<AudioDrivers> GetGlobal()
        {
            var pool = ResourceProvider.NewPooledSystemWide("AudioDrivers", s =>
            {
                return new AudioDrivers(SLogger);
            });
            return pool.GetHandle();
        }

        public void Dispose()
        {
            
            Logger.Information("Dispose");
        }
    }
}