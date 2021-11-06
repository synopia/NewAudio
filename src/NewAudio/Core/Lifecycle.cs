using System;
using System.Threading.Tasks;
using Abodit.StateMachine;
using Serilog;

namespace NewAudio.Core
{
    public enum LifecyclePhase
    {
        Uninitialized,
        Invalid,
        Created,
        Playing,
        Erroneous,
    }

    public interface ILifecycleDevice
    {
        LifecyclePhase Phase { get; set; }
        Task<bool> Free();
        bool Start();
        bool Stop();

        void ExceptionHappened(Exception e, string method);
    }

    public interface ILifecycleDevice<in TConfigIn, TConfigOut> : ILifecycleDevice
    {
        Task<TConfigOut> Create(TConfigIn config);
        bool IsInputValid(TConfigIn config);
    }

    internal class CreateEvent<TConfigIn> : Event
    {
        public TConfigIn Config { get; }

        public CreateEvent(string name, TConfigIn config = default) : base(name)
        {
            Config = config;
        }
    }

    public static class LifecycleEvents
    {
        public static readonly Event eStart = new Event("Start");
        public static readonly Event eStop = new Event("Stop");

        internal static readonly Event eCreateIntern = new Event("Create");
        public static readonly Event eFree = new Event("Free");

        public static Event eCreate<T>(T config)
        {
            return new CreateEvent<T>("Create", config);
        }
    }

    [Serializable]
    public class LifecycleStateMachine<TConfig> : StateMachineAsync<LifecycleStateMachine<TConfig>, Event,
        ILifecycleDevice<TConfig, bool>>
    {
        protected override void OnStateChanging(ILifecycleDevice<TConfig, bool> device, State oldState, State newState)
        {
        }

        protected override void OnStateChanged(ILifecycleDevice<TConfig, bool> context, State oldState, State newState)
        {
            base.OnStateChanged(context, oldState, newState);
            var device = (ExceptionHandler)context;
            AudioService.Instance.Logger.Information("LIFECYLE: {device} STATE CHANGE {old}=>{new}, P={phase}",
                device.Device, oldState.Name, newState.Name, device.Device.Phase);
        }

        public static readonly State Erroneous = AddState("Erroneous");
        public static readonly State Uninitialized = AddState("Uninitialized");
        public static readonly State Invalid = AddState("Invalid");
        public static readonly State Created = AddState("Created");
        public static readonly State Playing = AddState("Playing");


        static LifecycleStateMachine()
        {
            Erroneous
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Erroneous;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = c.IsInputValid(config);
                    if (res)
                    {
                        return await c.Create(config) ? Created : Uninitialized;
                    }

                    return Invalid;
                });
            Uninitialized
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Uninitialized;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = c.IsInputValid(config);
                    if (res)
                    {
                        return await c.Create(config) ? Created : s;
                    }

                    return Invalid;
                });
            Invalid
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Invalid;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = c.IsInputValid(config);
                    if (res)
                    {
                        return await c.Create(config) ? Created : Uninitialized;
                    }

                    return Invalid;
                });
            Created
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Created;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eStart,
                    (m, s, e, c) =>
                    {
                        return c.Start() ? Task.FromResult(Playing) : Task.FromResult<State>(Erroneous);
                    })
                .When(LifecycleEvents.eFree,
                    async (m, s, e, c) => { return await c.Free() ? Uninitialized : Erroneous; })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = await c.Free();
                    var inputValid = c.IsInputValid(config);
                    if (!inputValid)
                    {
                        return Invalid;
                    }

                    res = res && await c.Create(config);
                    return res ? s : Erroneous;
                });
            Playing
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Playing;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = c.Stop();
                    res = res && await c.Free();
                    var inputValid = c.IsInputValid(config);
                    if (!inputValid)
                    {
                        return Invalid;
                    }

                    res = res && await c.Create(config);
                    res = res && c.Start();
                    return res ? Playing : Erroneous;
                })
                .When(LifecycleEvents.eFree, async (m, s, e, c) =>
                {
                    var res = c.Stop();
                    res = res && await c.Free();
                    return res ? Uninitialized : Erroneous;
                })
                .When(LifecycleEvents.eStop,
                    (m, s, e, c) => { return c.Stop() ? Task.FromResult(Created) : Task.FromResult(Erroneous); });
        }

        private readonly ExceptionHandler _handler = new ExceptionHandler();

        public LifecycleStateMachine() : base(Uninitialized)
        {
        }

        public override Task EventHappens(Event @event, ILifecycleDevice<TConfig, bool> context)
        {
            _handler.Device = context;
            
            return base.EventHappens(@event, _handler);
        }

        private class ExceptionHandler : ILifecycleDevice<TConfig, bool>
        {
            public ILifecycleDevice<TConfig, bool> Device;

            public LifecyclePhase Phase
            {
                get => Device.Phase;
                set => Device.Phase = value;
            }

            public async Task<bool> Create(TConfig config)
            {
                try
                {
                    var result = await Device.Create(config);
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Create, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "CreateResources");
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
                    ExceptionHappened(e, "FreeResources");
                    return false;
                }
            }

            public bool Start()
            {
                try
                {
                    var result = Device.Start();
                    AudioService.Instance.Logger.Information(
                        "LIFECYLE: {device} Start, result={result}", Device, result);
                    return result;
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "StartProcessing");
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
                    ExceptionHappened(e, "StopProcessing");
                    return false;
                }
            }

            public bool IsInputValid(TConfig config)
            {
                try
                {
                    var result = Device.IsInputValid(config);
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

            public void ExceptionHappened(Exception e, string method)
            {
                AudioService.Instance.Logger.Error("ERROR {device}.{method} {e}", Device, method, e);
                Device.ExceptionHappened(e, method);
            }
        }
    }
}