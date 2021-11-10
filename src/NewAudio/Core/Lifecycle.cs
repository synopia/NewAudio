using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
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
    public class LifecycleStateMachine : StateMachineAsync<LifecycleStateMachine, Event, ILifecycleDevice>, IDisposable
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

        private readonly ExceptionHandler _handler = new();
        private ActionBlock<Event> _actionBlock;
        public ManualResetEvent WaitForEvents = new(false);
        private int _eventsInProcess = 0;
        private IDisposable _link;
        private BufferBlock<Event> _inputQueue = new(new DataflowBlockOptions()
        {
            MaxMessagesPerTask = 1
        });

        public LifecycleStateMachine(ILifecycleDevice context) : base(Uninitialized)
        {
            _handler.Device = context;
            _actionBlock = new ActionBlock<Event>(async evt =>
            {
                await base.EventHappens(evt, _handler);
                var r = Interlocked.Decrement(ref _eventsInProcess);
                if (r == 0)
                {
                    WaitForEvents.Set();
                }
            }, new ExecutionDataflowBlockOptions()
            {
                BoundedCapacity = 1,
                SingleProducerConstrained = true,
                MaxDegreeOfParallelism = 1,
                MaxMessagesPerTask = 1,
                TaskScheduler = TaskScheduler.FromCurrentSynchronizationContext()
            });
            _link = _inputQueue.LinkTo(_actionBlock);
        }

        public void EventHappens(Event @event)
        {
            
            var r= Interlocked.Increment(ref _eventsInProcess);
            if (r == 1)
            {
                WaitForEvents.Reset();
            }
            _inputQueue.Post(@event);
        }

        private bool _disposedValue;

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _eventsInProcess = 0;
                WaitForEvents.Dispose();
                _link.Dispose();
                _inputQueue.Complete();
                _actionBlock.Complete();
                Task.WaitAll(new[] { _actionBlock.Completion, _inputQueue.Completion });

                _link = null;
                WaitForEvents = null;
                _inputQueue = null;
                _actionBlock = null;
                _disposedValue = true;
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

            async Task<bool> ILifecycleDevice.Init()
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

            bool ILifecycleDevice.Play()
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