using System;
using Serilog;
using Serilog.Formatting.Display;
using NewAudio.Core;

namespace NewAudio.Core
{
    public class AudioService : IDisposable
    {
        private static AudioService _service;
        // public readonly Lifecycle Lifecycle = new Lifecycle();

        private AudioService()
        {
            Logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                // .WriteTo.File("NewAudio.log",
                // outputTemplate:
                // "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
            Log.Logger = Logger;

            // Be careful, dont do anything, that needs the AudioService itself! 
            Graph = new AudioGraph(Logger);
            Log.Logger.Information("Initializing Audio Service");
        }

        public static AudioService Instance => _service ??= new AudioService();

        public AudioGraph Graph { get; private set; }

        public ILogger Logger { get; }
        public LifecyclePhase Phase { get; private set; }

        public void Dispose()
        {
            // Lifecycle.Phase = LifecyclePhase.Shutdown;
            Graph.Dispose();
            Log.Logger.Information("AudioService Disposed");
        }

        public void Init()
        {
            Phase = LifecyclePhase.Booting;
            Log.Logger.Information("Audio Service: Uninitialized => {Phase}", Phase);
            // Lifecycle.Phase = LifecyclePhase.Booting;
        }

        public void Update()
        {
            // Lifecycle.Update();
        }

        public void Stop()
        {
            Log.Logger.Information("Audio Service: {Phase} => Stop", Phase);
            Phase = LifecyclePhase.Stopped;
            Graph.StopAll();
            // Lifecycle.Stop();
        }

        public void Play()
        {
            Log.Logger.Information("Audio Service:{Phase} => Play", Phase);
            Phase = LifecyclePhase.Playing;
            Graph.PlayAll();
            // Lifecycle.Start();
        }

        public string DebugInfo()
        {
            return $"{Graph.DebugInfo()}, Phase: {Phase}";
        }

        public void Reset()
        {
            Stop();
            Graph.Dispose();
            Graph = new AudioGraph(Logger);
        }
    }
}