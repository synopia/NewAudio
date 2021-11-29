using System;
using System.Threading;

namespace NewAudio.Dispatcher
{
    public interface IAsyncUpdater
    {
        void TriggerAsyncUpdate();
        void CancelPendingUpdate();
        void HandleUpdateNow();
        bool IsUpdatePending();
        void HandleAsyncUpdate();

    }
    public class AsyncUpdaterMessage : CallbackMessage
    {
        private readonly AsyncUpdater _owner;
        internal int ShouldDeliver;
        public AsyncUpdaterMessage(AsyncUpdater owner)
        {
            _owner = owner;
        }

        public override void MessageCallback()
        {
            var v = Interlocked.CompareExchange(ref ShouldDeliver, 0, 1);
            if (v == 1)
            {
                _owner.HandleAsyncUpdate();
            }
        }
    }
    public abstract class AsyncUpdater: IAsyncUpdater
    {
        private AsyncUpdaterMessage _message;
        protected AsyncUpdater()
        {
            _message = new AsyncUpdaterMessage(this);
        }

        public void TriggerAsyncUpdate()
        {
            var v = Interlocked.CompareExchange(ref _message.ShouldDeliver, 1, 0);
            if (v == 0)
            {
                if (!_message.Post())
                {
                    CancelPendingUpdate();
                }
            }
            
        }

        public void CancelPendingUpdate()
        {
            _message.ShouldDeliver = 0;
        }

        public void HandleUpdateNow()
        {
            if (Interlocked.Exchange(ref _message.ShouldDeliver, 0) != 0)
            {
                HandleAsyncUpdate();
            }
        }

        public bool IsUpdatePending()
        {
            return _message.ShouldDeliver != 0;
        }

        public abstract void HandleAsyncUpdate();

    }

    public class AsyncUpdateSupport : AsyncUpdater
    {
        private Action _onHandlyAsyncUpdate;

        public AsyncUpdateSupport(Action onHandlyAsyncUpdate)
        {
            _onHandlyAsyncUpdate = onHandlyAsyncUpdate;
        }

        public override void HandleAsyncUpdate()
        {
            _onHandlyAsyncUpdate();
        }
    }
}