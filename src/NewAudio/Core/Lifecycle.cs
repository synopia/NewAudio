using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abodit.StateMachine;
using Serilog;

namespace NewAudio.Core
{
    public enum LifecyclePhase
    {
        Uninitialized,
        Init,
        Play,
        Invalid,
    }

    public interface ILifecycleDevice
    {
        Task<bool> Init();
        Task<bool> Free();
        bool Play();
        bool Stop();
        bool IsInitValid();
        bool IsPlayValid();
        LifecyclePhase Phase { get; set; }

        void ExceptionHappened(Exception e, string method);
    }

    public static class LifecycleEvents
    {
        public static readonly Event ePlay = new Event("Play");
        public static readonly Event eStop = new Event("Stop");

        public static readonly Event eInit = new Event("Init");
        public static readonly Event eFree = new Event("Free");
    }

    [Serializable]
    public class LifecycleStateMachine : StateMachineAsync<LifecycleStateMachine, Event, ILifecycleDevice>
    {
        protected override void OnStateChanging(ILifecycleDevice device, State oldState, State newState)
        {
        }

        protected override void OnStateChanged(ILifecycleDevice context, State oldState, State newState)
        {
            base.OnStateChanged(context, oldState, newState);
            var device = (ExceptionHandler)context;
            AudioService.Instance.Logger.Information("LIFECYLE: {device} STATE CHANGE {old}=>{new}, P={phase}",
                device.Device, oldState.Name, newState.Name, device.Device.Phase);
        }

        public static readonly State Uninitialized = AddState("Uninitialized");
        public static readonly State Init = AddState("Init");
        public static readonly State Play = AddState("Play");
        public static readonly State Invalid = AddState("Invalid");

        static LifecycleStateMachine()
        {
            Invalid
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Invalid;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eInit, async (m, s, e, c) =>
                {
                    var res = c.IsInitValid();
                    if (res)
                    {
                        return await c.Init() ? Init : s;
                    }

                    return s;
                });
            Uninitialized
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Uninitialized;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eInit, async (m, s, e, c) =>
                {
                    var res = c.IsInitValid();
                    if (res)
                    {
                        return await c.Init() ? Init : Invalid;
                    }

                    return s;
                })
                .When(LifecycleEvents.ePlay,
                    async (m, s, e, c) =>
                    {
                        var res = c.IsInitValid();
                        if (!res)
                        {
                            return Uninitialized;
                        }
                        res = await c.Init();
                        if (!res)
                        {
                            return Invalid;
                        }
                        var inputValid = c.IsPlayValid();
                        if (!inputValid)
                        {
                            return Init;
                        }

                        return c.Play() ? Play : Init;
                    });
            Init
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Init;
                    return Task.CompletedTask;
                })

                .When(LifecycleEvents.eFree,
                    async (m, s, e, c) => { return await c.Free() ? Uninitialized : Invalid; })

                .When(LifecycleEvents.ePlay,
                    (m, s, e, c) =>
                    {
                        var inputValid = c.IsPlayValid();
                        if (!inputValid)
                        {
                            return Task.FromResult(Init);
                        }

                        return c.Play() ? Task.FromResult(Play) : Task.FromResult(Init);
                    })
                .When(LifecycleEvents.eInit, async (m, s, e, c) =>
                {
                    var res = await c.Free();
                    var inputValid = c.IsInitValid();
                    if (!inputValid)
                    {
                        return Invalid;
                    }

                    res = res && await c.Init();
                    return res ? s : Invalid;
                });
            Play
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Play;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eInit, async (m, s, e, c) =>
                {
                    var res = c.Stop();
                    res = res && await c.Free();
                    var inputValid = c.IsInitValid();
                    if (!inputValid)
                    {
                        return Invalid;
                    }
                    res = res && await c.Init();
                    inputValid = c.IsPlayValid();
                    if (!inputValid)
                    {
                        return Init;
                    }
                    res = res && c.Play();
                    return res ? Play : Invalid;
                })
                .When(LifecycleEvents.ePlay, (m, s, e, c) =>
                {
                    var inputValid = c.IsPlayValid();
                    if (!inputValid)
                    {
                        return Task.FromResult(Init);
                    }
                    return c.Play() ? Task.FromResult(Play) : Task.FromResult(Invalid);
                })
                .When(LifecycleEvents.eFree, async (m, s, e, c) =>
                {
                    var res = c.Stop();
                    res = res && await c.Free();
                    return res ? Uninitialized : Invalid;
                })
                .When(LifecycleEvents.eStop,
                    (m, s, e, c) =>
                    {
                        return c.Stop() ? Task.FromResult(Init) : Task.FromResult(Invalid);
                    });
        }

        private readonly ExceptionHandler _handler = new ExceptionHandler();
        private Queue<Event> _events = new Queue<Event>();
        public ManualResetEvent  WaitForEvents = new ManualResetEvent(false); 

        public LifecycleStateMachine(ILifecycleDevice context) : base(Uninitialized)
        {
            _handler.Device = context;
            EventHappened += (arg) =>
            {
                Event next=null;
                lock (_events)
                {
                    _events.Dequeue();
                    if (_events.Count>0)
                    {
                        next = _events.Peek();
                    }
                }

                if (next != null)
                {
                    base.EventHappens(next, _handler);
                }
                else
                {
                    WaitForEvents.Set();
                }
            };
        }

        public void EventHappens(Event @event)
        {
            var empty = false;
            WaitForEvents.Reset();
            lock (_events)
            {
                empty = _events.Count==0;
                _events.Enqueue(@event);
            }

            if (empty)
            {
                base.EventHappens(@event, _handler);
            }
        }

        private class ExceptionHandler : ILifecycleDevice
        {
            public ILifecycleDevice Device;

            public LifecyclePhase Phase
            {
                get => Device.Phase;
                set => Device.Phase = value;
            }

            public async Task<bool> Init()
            {
                try
                {
                    var result = await Device.Init();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Create, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "Create");
                    return false;
                }
            }
            public async Task<bool> Free()
            {
                try
                {
                    var result = await Device.Free();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Free, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "Free");
                    return false;
                }
            }
            public bool Play()
            {
                try
                {
                    var result = Device.Play();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Play, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "Play");
                    return false;
                }
            }

            public bool Stop()
            {
                try
                {
                    var result = Device.Stop();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Stop, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "Stop");
                    return false;
                }
            }

            public bool IsInitValid()
            {
                try
                {
                    var result = Device.IsInitValid();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} IsInputValid, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "IsInputValid");
                    return false;
                }
            }
            public bool IsPlayValid()
            {
                try
                {
                    var result = Device.IsPlayValid();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} IsUpdateValid, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "IsUpdateValid");
                    return false;
                }
            }

            public void ExceptionHappened(Exception e, string method)
            {
                AudioService.Instance.Logger.Error("ERROR {device}.{method} {e}", Device, method, e);
                Device.ExceptionHappened(e, method);
            }
        }
    }
}