using System.Collections.Generic;
using System.Threading;

namespace VL.NewAudio.Internal
{
    public class EventManager
    {
        private static object _lockEvents = new();
        private static Dictionary<AutoResetEvent, List<AutoResetEvent>> _events = new();

        public static AutoResetEvent GenerateAutoResetEvent()
        {
            var autoResetEvent = new AutoResetEvent(false);
            lock (_lockEvents)
            {
                _events.Add(autoResetEvent, new List<AutoResetEvent>());
            }

            return autoResetEvent;
        }

        public static void ReleaseAutoResetEvent(AutoResetEvent autoResetEvent)
        {
            lock (_lockEvents)
            {
                _events.Remove(autoResetEvent);
            }
        }

        public static AutoResetEvent GenerateChildEvent(AutoResetEvent autoResetEvent)
        {
            var autoResetEventChild = new AutoResetEvent(false);
            lock (_lockEvents)
            {
                _events[autoResetEvent].Add(autoResetEventChild);
            }

            return autoResetEventChild;
        }

        public static void ReleaseChildEvent(AutoResetEvent autoResetEventChild)
        {
            lock (_lockEvents)
            {
                foreach (var autoResetEvents in _events.Values)
                {
                    if (autoResetEvents.Contains(autoResetEventChild))
                    {
                        autoResetEvents.Remove(autoResetEventChild);
                        break;
                    }
                }
            }
        }

        public static void SetAll(AutoResetEvent autoResetEvent)
        {
            lock (_lockEvents)
            {
                foreach (var autoResetEventChild in _events[autoResetEvent])
                {
                    autoResetEventChild.Set();
                }
            }
        }
    }
}