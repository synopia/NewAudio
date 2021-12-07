using System;
using System.Threading;

namespace VL.NewAudio.Dispatcher
{
    public abstract class MessageListener
    {
        public abstract void HandleMessage(Message message);

        public void PostMessage(Message message)
        {
            message.Recipient = this;
            message.Post();
        }
    }
    public class Message : Dispatcher.BaseMessage
    {
        internal MessageListener Recipient;
        
        public override void MessageCallback()
        {
            Recipient?.HandleMessage(this);
        }
    }

    public abstract class CallbackMessage : Dispatcher.BaseMessage
    {
        public abstract override void MessageCallback();
    }
    public delegate object MessageCallbackFunc(object userData);
    
    public class AsyncMessage : Dispatcher.BaseMessage
    {
        private MessageCallbackFunc _callbackFunc;
        private object _userData;
        public object Result { get; private set; }
        public ManualResetEvent Finished { get; } = new(false);

        public AsyncMessage(MessageCallbackFunc callbackFunc, object userData=null)
        {
            _callbackFunc = callbackFunc;
            _userData = userData;
        }

        public override void MessageCallback()
        {
            Result = _callbackFunc.Invoke(_userData);
            Finished.Set();
        }
        
        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Finished.Dispose();
                }

                _disposedValue = true;
            }
        }
    }
}