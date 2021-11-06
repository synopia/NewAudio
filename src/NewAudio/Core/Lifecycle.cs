using System;
using System.Threading.Tasks;
using Abodit.StateMachine;
using Serilog;

namespace NewAudio.Core
{
    public enum LifecyclePhase
    {
        Uninitialized,
        Ready,
        Playing
    }

    public interface ILifecycleDevice
    {
        LifecyclePhase Phase { get; set; }
        Task<bool> FreeResources();
        Task<bool> StartProcessing();
        Task<bool> StopProcessing();

        void ExceptionHappened(Exception e, string method);        
    }
    public interface ILifecycleDevice<in TConfigIn, TConfigOut> : ILifecycleDevice
    {

        Task<TConfigOut> CreateResources(TConfigIn config);
    }

    internal class CreateEvent<TConfigIn> : Event
    {
        public TConfigIn Config { get; }

        public CreateEvent(string name, TConfigIn config=default) : base(name)
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
    public class LifecycleStateMachine<TConfig> : StateMachineAsync<LifecycleStateMachine<TConfig>, Event, ILifecycleDevice<TConfig, bool>>
    {
        protected override void OnStateChanging(ILifecycleDevice<TConfig, bool> device, State oldState, State newState)
        {
        }

        public static readonly State Erroneous = AddState("Erroneous");
        public static readonly State Uninitialized = AddState("Uninitialized");

        public static readonly State Ready = AddState("Ready");

        public static readonly State Playing = AddState("Playing");

        
        static LifecycleStateMachine()
        {
            Uninitialized
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Uninitialized;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    return await c.CreateResources(config) ? Ready : Uninitialized;
                });
            Ready
                .OnEnter((m, s, e, c) =>
                {
                    c.Phase = LifecyclePhase.Ready;
                    return Task.CompletedTask;
                })
                .When(LifecycleEvents.eStart, async (m, s, e, c) =>
                {
                    return await c.StartProcessing() ? Playing : Erroneous;
                })
                .When(LifecycleEvents.eFree, async (m, s, e, c) =>
                {
                    return await c.FreeResources() ? Uninitialized : Erroneous;
                })
                .When(LifecycleEvents.eCreateIntern, async (m, s, e, c) =>
                {
                    var config = ((CreateEvent<TConfig>)e).Config;
                    var res = await c.FreeResources();
                    res = res && await c.CreateResources(config);
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
                    var res = await c.StopProcessing();
                    res = res && await c.FreeResources();
                    res = res && await c.CreateResources(config);
                    res = res && await c.StartProcessing();
                    return res ? Playing : Erroneous;
                })
                .When(LifecycleEvents.eFree,  async (m, s, e, c) =>
                {
                    var res = await c.StopProcessing();
                    res = res && await c.FreeResources();
                    return res ? Uninitialized : Erroneous;
                })
                .When(LifecycleEvents.eStop,async (m, s, e, c) =>
                {
                    return await c.StopProcessing() ? Ready : Erroneous;
                });
            
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

            public async Task<bool> CreateResources(TConfig config)
            {
                try
                {
                    return await Device.CreateResources(config);
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "CreateResources");
                    return false;
                }
            }

            public async Task<bool> FreeResources()
            {
                try
                {
                    return await Device.FreeResources();
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "FreeResources");
                    return false;
                }
            }

            public async Task<bool> StartProcessing()
            {
                try
                {
                    return await Device.StartProcessing();
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "StartProcessing");
                    return false;
                }
            }

            public async Task<bool> StopProcessing()
            {
                try
                {
                    return await Device.StopProcessing();
                }
                catch (Exception e)
                {
                    ExceptionHappened(e, "StopProcessing");
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