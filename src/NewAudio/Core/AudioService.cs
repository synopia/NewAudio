using System;
using Serilog;
using Serilog.Formatting.Display;
using NewAudio.Core;
using VL.Core;
using VL.Model;

namespace NewAudio.Core
{
    public class AudioService
    {
        private static AudioService _service;

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


            Log.Logger.Information($"Initializing Audio Service");
        }

        public static AudioService Instance => _service ??= new AudioService();

        public AudioGraph Graph { get; private set; }

        public ILogger Logger { get; }

        private bool _playing;
        private ulong _lastFrame;

        public void Init()
        {
        }

        public void Update(bool playing, int buffersCount=6)
        {
            var currentFrame = VLSession.Instance.UserRuntime.Frame;

            if (currentFrame != _lastFrame)
            {
                if (playing != _playing)
                {
                    Log.Logger.Information("Audio Service: {old} => {new}", _playing, playing);
                    _playing = playing;
                    if (playing)
                    {
                        Graph.PlayAll();
                    }
                    else
                    {
                        Graph.StopAll();
                    }
                }
            }

            _lastFrame = currentFrame;
        }

        public string DebugInfo()
        {
            return Graph.DebugInfo();
        }

        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioService {t} ({d})", this, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                    // Graph.Dispose();
                }

                _disposedValue = disposing;
            }
        }
    }
}