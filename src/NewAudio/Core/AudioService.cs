using System;
using Serilog;
using Serilog.Formatting.Display;
using NewAudio.Core;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Core
{
    public class AudioService
    {
        public AudioService()
        {
            _logger = new LoggerConfiguration()
                .Enrich.WithThreadId()
                .WriteTo.Console(new MessageTemplateTextFormatter(
                    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}"))
                .WriteTo.Seq("http://localhost:5341")
                // .WriteTo.File("NewAudio.log",
                // outputTemplate:
                // "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj} {Properties}{NewLine}{Exception}")
                .MinimumLevel.Debug()
                .CreateLogger();
            Log.Logger = _logger;

            Log.Logger.Information($"Initializing Audio Service");
        }

        private readonly ILogger _logger;
        private int _nextId;

        public ILogger GetLogger<T>()
        {
            return _logger.ForContext<T>();
        }
        public int GetNextId()
        {
            return _nextId++;
        }

        public void Dispose() => Dispose(true);

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = disposing;
            }
        }
    }
}