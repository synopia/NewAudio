using System;
using Serilog;
using Serilog.Formatting.Display;
using VL.NewAudio.Core;

namespace NewAudio.Core
{
    public class AudioService : IDisposable
    {
        private static AudioService _service;
        public readonly Lifecycle Lifecycle = new Lifecycle();

        private AudioService()
        {
            Logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                // .WriteTo.File("VL.NewAudio.log",
                // outputTemplate:
                // "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}")
                .MinimumLevel.Verbose()
                .CreateLogger();
            Log.Logger = Logger;

            // Be careful, dont do anything, that needs the AudioService itself! 
            Flow = new AudioDataflow(Logger);
            Graph = new AudioGraph();
            Log.Logger.Information("Initializing Audio Service");
        }

        public static AudioService Instance => _service ??= new AudioService();

        public AudioDataflow Flow { get; private set; }

        public AudioGraph Graph { get; private set; }

        public ILogger Logger { get; }

        public void Dispose()
        {
            Lifecycle.Phase = LifecyclePhase.Shutdown;
            Flow.Dispose();
            Log.Logger.Information("AudioService Disposed");
        }

        public void Init()
        {
            Log.Logger.Information("Audio Service: Initialized, Flow={flow}", Flow.DebugInfo());
            Lifecycle.Phase = LifecyclePhase.Stopped;
        }

        public void Update()
        {
            Lifecycle.Update();
        }

        public void Stop()
        {
            Log.Logger.Information("Audio Service: Stop, current {current}", Lifecycle.Phase);
            Lifecycle.Stop();
        }

        public void Play()
        {
            Log.Logger.Information("Audio Service: Play current {current}", Lifecycle.Phase);
            Lifecycle.Start();
        }

        public string DebugInfo()
        {
            return $"{Flow.DebugInfo()}, {Graph.DebugInfo()}, Phase: {Lifecycle.Phase}";
        }

        public void Reset()
        {
            Stop();
            Flow = new AudioDataflow(Logger);
            Graph = new AudioGraph();
        }
    }
}