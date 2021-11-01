using System;
using Serilog;
using Serilog.Formatting.Display;
using Stride.Core;

namespace NewAudio.Core
{
    public class AudioService : IDisposable
    {
        private static AudioService _service;

        public static AudioService Instance => _service ??= new AudioService();

        public AudioDataflow Flow { get; private set; }

        public AudioGraph Graph { get; private set; }

        public ILogger Logger { get; }

        private LifecyclePhase _phase;
        public LifecyclePhase Phase
        {
            get => _phase;
            set
            {
                Flow.PostLifecycleMessage(_phase, value);
                _phase = value;
            }
        }

        private AudioService()
        {
            Logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter("{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                // .WriteTo.File("VL.NewAudio.log",
                    // outputTemplate:
                    // "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
            Log.Logger = Logger;
            
            // Be careful, dont do anything, that needs the AudioService itself! 
            Flow = new AudioDataflow(Logger);
            Graph = new AudioGraph();
            Log.Logger.Information("Initializing Audio Service");
        }

        public void Init()
        {
            Log.Logger.Information("Audio Service: Initialized, Flow={flow}", Flow.DebugInfo());
            Phase = LifecyclePhase.Stopped;
        }

        public void Stop()
        {
            Log.Logger.Information("Audio Service: Stop, current {current}", _phase);
            if (Phase == LifecyclePhase.Playing)
            {
                Phase = LifecyclePhase.Stopped;
            }
        }

        public void Play()
        {
            Log.Logger.Information("Audio Service: Play current {current}", _phase);
            if (Phase == LifecyclePhase.Stopped)
            {
                Phase = LifecyclePhase.Playing;
            }
        }

        public string DebugInfo()
        {
            return $"{Flow.DebugInfo()}, {Graph.DebugInfo()}, Phase: {Phase}";
        }
        
        public void Reset()
        {
            Flow = new AudioDataflow(Logger);
            Graph = new AudioGraph();
        }

        public void Dispose()
        {
            Phase = LifecyclePhase.Shutdown;
            Flow.Dispose();
            Log.Logger.Information("AudioService Disposed");
        }
    }
}