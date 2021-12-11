using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace VL.NewAudio.Dispatcher
{
    public class Dispatcher: IDisposable
    {
        public static Dispatcher Instance { get; private set; }

        public static void NeedToBeLocked()
        {
            Trace.Assert(Instance!=null && Instance.CurrentThreadHasLockedMessageManager());
        }
        public abstract class BaseMessage : IDisposable
        {
            public abstract void MessageCallback();

            public bool Post()
            {
                return Instance?.Post(this) ?? false;
            }

            private bool _disposedValue;

            public void Dispose()
            {
                Dispose(true);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!_disposedValue)
                {
                    if (disposing)
                    {
                    }

                    _disposedValue = true;
                }
            }
        }

        private CancellationTokenSource _cts = new ();
        private ConcurrentQueue<BaseMessage> _messageQueue = new ();
        public CancellationToken ActiveToken;
        private Thread _thread;
        private AutoResetEvent _readyEvent = new(false);
        public bool IsRunning { get; private set; }
        private int _dispatcherThreadId;
        private Exception _lastException;

        public Dispatcher()
        {
            StartThread();
        }

        public bool CurrentThreadHasLockedMessageManager()
        {
            var id = Thread.CurrentThread.ManagedThreadId;
            return id == _dispatcherThreadId; /* || id */
        }
        
        private void StartThread()
        {
            _thread = new Thread(DispatcherLoop);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Priority = ThreadPriority.Highest;
            _thread.IsBackground = true;
            _thread.Start();

            _readyEvent.WaitOne(2000);
        }
        
        
        public bool IsThisDispatcherThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _dispatcherThreadId;
        }

        public object CallFunctionInDispatcher(MessageCallbackFunc func, object userData)
        {
            if (IsThisDispatcherThread())
            {
                return func(userData);
            }

            var message = new AsyncMessage(func, userData);

            if (message.Post())
            {
                message.Finished.WaitOne();
                return message.Result;
            }
            
            Trace.Assert(false);
            return null;
        }

        public bool Post(BaseMessage message)
        {
            _messageQueue.Enqueue(message);
            return true;
        }
        private void DispatcherLoop()
        {
            _dispatcherThreadId = Thread.CurrentThread.ManagedThreadId;
            Trace.Assert(Instance==null);
            Instance = this;
            Trace.Assert(IsThisDispatcherThread());

            IsRunning = true;
            _readyEvent.Set();
            
            while (!ActiveToken.IsCancellationRequested)
            {
                BaseMessage message = null;
                try
                {
                    while (_messageQueue.TryDequeue(out message))
                    {
                        Trace.WriteLine("Message received");
                        message.MessageCallback();
                    }

                    Thread.Sleep(1);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e.Message);
                    Trace.WriteLine(e.StackTrace);
                    if (message != null)
                    {
                        if (message is AsyncMessage am)
                        {
                            try
                            {
                                am.Finished.Set();
                            }
                            catch (Exception exception)
                            {
                                Trace.WriteLine(exception.Message);
                                Trace.WriteLine(exception.StackTrace);
                            }
                        }
                    }

                    _lastException = e;
                }
            }

            IsRunning = false;
        }

        public void StopDispatchLoop()
        {
            _cts.Cancel();
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                IsRunning = false;
                _cts.Cancel();
                bool threadDone = _thread.Join(500);
                if (!threadDone)
                {
                    _thread.Abort();
                }
                threadDone = _thread.Join(500);
                _readyEvent.Dispose();
                Instance = null;
            }
        }
    }
}