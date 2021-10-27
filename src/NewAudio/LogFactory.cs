using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;

namespace NewAudio
{
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warn,
        Error
    }
    public readonly struct LogEntry
    {
        public readonly DateTime Time;
        public readonly LogLevel Level; 
        public readonly string Category;
        public readonly string Message;
        public readonly int ThreadId;

        public override string ToString()
        {
            return $"[{Time}] [{ThreadId}] [{Level}] [{Category}]   {Message}";
        }

        public LogEntry(int threadId, LogLevel level, string category, string message)
        {
            ThreadId = threadId;
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

        public void Trace(string message)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Trace, Category, message));
        }
        public void Debug(string message)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Debug, Category, message));
        }
        public void Info(string message)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Info, Category, message));
        }
        public void Warn(string message)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Warn, Category, message));
        }
        public void Error(string message)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Error, Category, message));
        }
        public void Error(Exception exception)
        {
            _entries.OnNext(new LogEntry(Thread.CurrentThread.GetHashCode(), LogLevel.Error, Category, exception.Message+"\n"+exception.StackTrace));
        }
    }
    public class LogFactory
    {
        private readonly Subject<LogEntry> _entries = new Subject<LogEntry>();
        private static LogFactory _instance;
        private int _id = 0;

        public static LogFactory Instance => _instance ??= new LogFactory();
        readonly StreamWriter _writer = File.CreateText("VL.NewAudio.log");
        public LogLevel MinLevel = LogLevel.Debug;
        
        private LogFactory()
        {
            _writer.AutoFlush = true;
            GetLogEntries().Subscribe(e =>
            {
                if ((int)e.Level >= (int)MinLevel)
                {
                    Console.WriteLine(e);
                    _writer.WriteLine(e);
                }

            });
        }

        
        public Logger Create(string category)
        {
            return new Logger(_entries, category+$"{_id++}");
        }

        public IObservable<LogEntry> GetLogEntries()
        {
            return _entries.Where(e =>(int)e.Level>=(int)MinLevel);
        }
    }
}