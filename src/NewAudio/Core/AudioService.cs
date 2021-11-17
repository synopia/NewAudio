using System;
using NewAudio.Devices;
using Serilog;
using Serilog.Formatting.Display;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Core
{
    public enum LifecyclePhase
    {
        Uninitialized,
        Stop,
        Play,
        Invalid
    }
    
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

            Log.Logger.Information("============================================");
            Log.Logger.Information("Initializing Audio Service");
        }

        private IResourceHandle<DriverManager> _driverManager;

        private readonly ILogger _logger;
        private int _nextId;
        public ulong LastProcessedFrame => _lastFrame;
        private ulong _lastFrame;
        
        public ILogger GetLogger<T>()
        {
            // ReSharper disable once ContextualLoggerProblem
            return _logger.ForContext<T>();
        }

        public int GetNextId()
        {
            return _nextId++;
        }

        public void Update()
        {
            var currentFrame = VLSession.Instance.UserRuntime.Frame;

            if (currentFrame != _lastFrame)
            {
                try
                {
                    _driverManager ??= Factory.GetDriverManager();
                    _driverManager.Resource.UpdateAllDevices();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in DriverManager");
                }

                _lastFrame = currentFrame;
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
        }

        private bool _disposedValue;

        private void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }
        }
    }
}