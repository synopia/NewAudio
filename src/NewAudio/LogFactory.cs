using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Subjects;

namespace NewAudio
{
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARN,
        ERROR
    }
    public readonly struct LogEntry
    {
        public readonly DateTime Time;
        public readonly LogLevel Level; 
        public readonly string Category;
        public readonly string Message;

        public override string ToString()
        {
            return $"{Time} {Level} {Category} {Message}";
        }

        public LogEntry(LogLevel level, string category, string message)
        {
            Time = DateTime.Now;
            Level = level;
            Category = category;
            Message = message;
        }
    }

    public class Logger
    {
        private readonly Subject<LogEntry> _entries;
        public string Category;

        public Logger(Subject<LogEntry> entries, string category)
        {
            _entries = entries;
            Category = category;
        }

        public void Debug(string message)
        {
            _entries.OnNext(new LogEntry(LogLevel.DEBUG, Category, message));
        }
        public void Info(string message)
        {
            _entries.OnNext(new LogEntry(LogLevel.INFO, Category, message));
        }
        public void Warn(string message)
        {
            _entries.OnNext(new LogEntry(LogLevel.WARN, Category, message));
        }
        public void Error(string message)
        {
            _entries.OnNext(new LogEntry(LogLevel.ERROR, Category, message));
        }
        public void Error(Exception exception)
        {
            _entries.OnNext(new LogEntry(LogLevel.ERROR, Category, exception.Message+"\n"+exception.StackTrace));
        }
    }
    public class LogFactory
    {
        private readonly Subject<LogEntry> _entries = new Subject<LogEntry>();
        private static LogFactory _instance;
        private static int id = 0;

        public static LogFactory Instance => _instance ??= new LogFactory();
        readonly StreamWriter _writer = File.CreateText("c:\\Users\\paulf\\RiderProjects\\NewAudio\\text.log");
        public LogFactory()
        {
            _writer.AutoFlush = true;
            _entries.Subscribe(e =>
            {
                _writer.WriteLine(e);
                
            });
        }

        public Logger Create(string category)
        {
            return new Logger(_entries, category+$"{id++}");
        }

        public static IObservable<LogEntry> GetLogEntries()
        {
            return _instance._entries;
        }
    }
}