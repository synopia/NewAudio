using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VL.NewAudio.Dispatcher
{
    public interface IChangeListener
    {
        void ChangeListenerCallback(ChangeBroadcaster source);
    }

    public interface IChangeBroadcaster
    {
        void SendChangeMessage();
    }
    public class ChangeBroadcaster: IChangeBroadcaster
    {
        private class ChangeBroadcasterCallback : AsyncUpdater
        {
            internal ChangeBroadcaster Owner;
            
            public override void HandleAsyncUpdate()
            {
                Trace.Assert(Owner!=null);
                Owner.CallListeners();
            }
        }

        private ChangeBroadcasterCallback _broadcastCallback;
        private List<IChangeListener> _listener = new List<IChangeListener>();
        private bool _anyListener;
        public ChangeBroadcaster()
        {
            _broadcastCallback = new ChangeBroadcasterCallback
            {
                Owner = this
            };
        }

        public void AddChangeListener(IChangeListener listener)
        {
            Dispatcher.NeedToBeLocked();
            _listener.Add(listener);
            _anyListener = true;
        }

        public void RemoveChangeListener(IChangeListener listener)
        {
            Dispatcher.NeedToBeLocked();
            _listener.Remove(listener);
            _anyListener = _listener.Count>0;
        }

        public void RemoveAllChangeListener()
        {
            Dispatcher.NeedToBeLocked();
            _listener.Clear();
            _anyListener = false;
        }

        public void SendChangeMessage()
        {
            if (_anyListener)
            {
                _broadcastCallback.TriggerAsyncUpdate();
            }
        }

        public void SendSynchronousChangeMessage()
        {
            Dispatcher.NeedToBeLocked();
            _broadcastCallback.CancelPendingUpdate();
            CallListeners();
        }

        public void DispatchPendingMessages()
        {
            _broadcastCallback.HandleUpdateNow();
        }

        public void CallListeners()
        {
            foreach (var changeListener in _listener)
            {
                changeListener.ChangeListenerCallback(this);
            }
        }
    }
}