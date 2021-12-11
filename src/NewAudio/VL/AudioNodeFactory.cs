using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using VL.NewAudio.Nodes;
using VL.NewAudio.Processor;
using VL.Core;
using VL.Core.Diagnostics;

namespace VL.NewAudio.Core
{
    public static class AudioNodeFactory
    {
        public static AudioNodeDesc<T> NewNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor,
            Action<T>? update = null,
            string? name = null, string? category = null, bool hasStateOutput = false, bool copyOnWrite = false)
            where T : class
        {
            return new AudioNodeDesc<T>(factory, ctx => (ctor(ctx), () => { }), name: name, category: category,
                hasStateOutput: hasStateOutput, update: update, copyOnWrite: copyOnWrite);
        }

        public static AudioBaseNodeDesc<T> NewAudioNode<T>(this IVLNodeDescriptionFactory factory,
            Func<NodeContext, T> ctor, Action<T>? update = null,
            string? name = null, string? category = null, bool hasStateOutput = false) where T : AudioNode
        {
            return new AudioBaseNodeDesc<T>(factory, ctx => CreateInstanceWithErrorToggle(ctx, ctor), name: name,
                category: category, hasStateOutput: hasStateOutput, update: update);
        }

        public static AudioProcessorNodeDesc<T> NewProcessorNode<T>(this IVLNodeDescriptionFactory factory,
            Func<NodeContext, T> ctor, Action<AudioProcessorNode<T>>? update = null,
            string? name = null, string? category = null, bool hasInfoOutput = true, bool hasAudioInput = true,
            bool hasAudioOutput = true, bool hasStateOutput = false) where T : AudioProcessor
        {
            return new AudioProcessorNodeDesc<T>(factory, ctx => CreateInstanceWithErrorToggle(ctx, c =>
            {
                var processor = ctor(c);
                return new AudioProcessorNode<T>(processor);
            }), update, name, category, hasAudioInput, hasAudioOutput, hasStateOutput, hasInfoOutput);
        }

        private static (T, Action) CreateInstanceWithErrorToggle<T>(NodeContext ctx, Func<NodeContext, T> ctor)
            where T : AudioNode
        {
            var instance = ctor(ctx);
            /*
            Message[] visibleErrors = {};
            var unsubscribe = instance.Messages.Subscribe((x)=>
            {
                ToggleMessage(x);
            });
            */
            return (instance, () =>
            {
                /*
                foreach (var error in visibleErrors)
                {
                    Session.ToggleMessage(error, false);
                }

                unsubscribe.Dispose();
                */
                instance.Dispose();
            });
            /*
            void ToggleMessage(Message[] errors)
            {
                Trace.WriteLine(uniqueId);
                Trace.WriteLine(ctx.Path.Stack.Peek());
                foreach (var error in visibleErrors)
                {
                    if (Array.IndexOf(errors, error) == -1)
                    {
                        Session.ToggleMessage(error, false);                        
                    }
                }

                var newErrors = new List<Message>();
                foreach (var error in errors)
                {
                    if (Array.IndexOf(visibleErrors, error) == -1)
                    {
                        var message = error.WithElementId(ctx.Path.Stack.Peek());
                        newErrors.Add(message);
                        Session.ToggleMessage(message, true);
                    }
                }

                visibleErrors = newErrors.ToArray();
            }   
        */
        }
    }

    public class AudioProcessorNodeDesc<TProcessor> : AudioBaseNodeDesc<AudioProcessorNode<TProcessor>>
        where TProcessor : AudioProcessor
    {
        public AudioProcessorNodeDesc(IVLNodeDescriptionFactory factory,
            Func<NodeContext, (AudioProcessorNode<TProcessor>, Action)> ctor,
            Action<AudioProcessorNode<TProcessor>>? update, string? name, string? category, bool hasAudioInput,
            bool hasAudioOutput, bool hasStateOutput, bool hasInfoOutput) : base(factory, ctor, update, name, category,
            hasStateOutput)
        {
            if (hasAudioInput)
            {
                AddInput("Audio In", x => x.Input, (x, v) => x.Input = v);
            }

            if (hasAudioOutput)
            {
                AddOutput("Audio Out", x => x.Output);
            }

            if (hasInfoOutput)
            {
                AddOutput("Output Channels", x => x.Processor.TotalNumberOfOutputChannels);
            }
        }
    }

    public class AudioBaseNodeDesc<TAudioNode> : AudioNodeDesc<TAudioNode>
        where TAudioNode : AudioNode
    {
        public AudioBaseNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (TAudioNode, Action)> ctor,
            Action<TAudioNode>? update, string? name = null, string? category = null, bool hasStateOutput = false,
            bool copyOnWrite = false) : base(factory, ctor, update, name, category, hasStateOutput, copyOnWrite)
        {
        }

        public AudioBaseNodeDesc<TAudioNode> WithEnabledPins(bool output = true)
        {
            AddInput(nameof(AudioNode.IsEnable), x => x.IsEnable, (x, v) => x.IsEnable = v, false,
                displayName: "Enable");
            if (output)
            {
                AddOutput(nameof(AudioNode.IsEnabled), x => x.IsEnabled);
            }

            return this;
        }
    }

    public class AudioNodeDesc<TInstance> : IVLNodeDescription, IInfo where TInstance : class
    {
        private readonly List<AudioPinDesc> _inputs = new();
        private readonly List<AudioPinDesc> _outputs = new();
        private readonly Func<NodeContext, (TInstance, Action)> _ctor;
        private readonly Action<TInstance>? Update;

        public AudioNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (TInstance, Action)> ctor,
            Action<TInstance>? update,
            string? name = null, string? category = null, bool hasStateOutput = false, bool copyOnWrite = false)
        {
            Factory = factory;
            _ctor = ctor;
            Name = name ?? typeof(TInstance).Name;
            Category = category ?? string.Empty;
            Update = update;
            CopyOnWrite = copyOnWrite;

            if (hasStateOutput)
            {
                AddOutput("Output", x => x);
            }
        }

        public bool CopyOnWrite { get; }
        public IVLNodeDescriptionFactory Factory { get; }
        public string Name { get; }
        public string Category { get; }
        public bool Fragmented => false;
        public IReadOnlyList<IVLPinDescription> Inputs => _inputs;

        public IReadOnlyList<IVLPinDescription> Outputs => _outputs;

        public IEnumerable<Message> Messages => Enumerable.Empty<Message>();

        public string Summary => typeof(TInstance).GetSummary();

        public string Remarks => typeof(TInstance).GetRemarks();
        public IObservable<object> Invalidated => Observable.Empty<object>();

        public IVLNode CreateInstance(NodeContext context)
        {
            var (instance, onDispose) = _ctor(context);
            var node = new Node(context)
            {
                NodeDescription = this
            };
            var inputs = _inputs.Select(p => p.CreatePin(node, instance)).ToArray();
            var outputs = _outputs.Select(p => p.CreatePin(node, instance)).ToArray();

            node.Inputs = inputs;
            node.Outputs = outputs;

            if (CopyOnWrite)
            {
                node.UpdateAction = () =>
                {
                    if (node.NeedsUpdate)
                    {
                        node.NeedsUpdate = false;
                        if (instance is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }

                        instance = _ctor(context).Item1;
                        foreach (var input in inputs)
                        {
                            input.Update(instance);
                        }

                        foreach (var output in outputs)
                        {
                            output.Instance = instance;
                        }
                    }
                };
                node.DisposeAction = () =>
                {
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                };
            }
            else
            {
                node.UpdateAction = () =>
                {
                    if (node.NeedsUpdate)
                    {
                        node.NeedsUpdate = false;
                        Update?.Invoke(instance);
                        /*
                                     try
                                     {
                                         Message? message = null;
                                     
                                         message = instance.Update(node.UpdateMask);
                                         instance.AddMessage(message);
                                     }
                                     catch (Exception exception)
                                     {
                                         instance.AddMessage(new Message(MessageSeverity.Error, exception.Message));
                                     }
                                     finally
                                     {
                                         node.UpdateMask = 0;
                                     }
                                 */
                    }
                };
                node.DisposeAction = () =>
                {
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }

                    onDispose.Invoke();
                };
            }

            return node;
        }

        public bool OpenEditor()
        {
            return false;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new CachedInputPin<T>(node, instance, getter, setter, getter(instance)),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter,
            Func<T, T, bool> equals,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) =>
                    new CachedInputPin<T>(node, instance, getter, setter, getter(instance), equals),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter,
            T defaultValue,
            string? summary = null, string? remarks = null, bool isVisible = true, string? displayName = null)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = displayName ?? name.InsertSpaces(),
                CreatePin = (node, instance) => new CachedInputPin<T>(node, instance, getter, setter, defaultValue),
                IsVisible = isVisible
            });
            return this;
        }

        private static bool SequenceEqual<T>(IEnumerable<T>? one, IEnumerable<T>? two)
        {
            if (one == null)
            {
                return two == null;
            }

            if (two == null)
            {
                return false;
            }

            return one.SequenceEqual(two);
        }

        public AudioNodeDesc<TInstance> AddListInput<T>(string name, Func<TInstance, IEnumerable<T>> getter,
            Action<TInstance, IEnumerable<T>> setter)
        {
            return AddInput(name, getter,
                equals: SequenceEqual, setter:
                (x, v) =>
                {
                    var items = v?.Where(i => i != null);
                    setter(x, items ?? Array.Empty<T>());
                });
        }


        public AudioNodeDesc<TInstance> AddOutput<T>(string name, Func<TInstance, T> getter,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new OutputPin<T>(node, instance, getter),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddCachedOutput<T>(string name, Func<TInstance, T> getter,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new CachedOutputPin<T>(node, instance, getter),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddOutput<T>(string name, Func<(Func<TInstance, T>, IDisposable)> ctor,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) =>
                {
                    var (getter, disposable) = ctor();
                    return new CachedOutputPin<T>(node, instance, getter, disposable);
                },
                IsVisible = isVisible
            });
            return this;
        }

        private class AudioPinDesc : IVLPinDescription, IInfo, IVLPinDescriptionWithVisibility
        {
            private readonly string _memberName;
            private string? _summary;
            private string? _remarks;

            public AudioPinDesc(string memberName, Type type, string? summary = null, string? remarks = null)
            {
                _memberName = memberName;
                Type = type;
                _summary = summary;
                _remarks = remarks;
                DefaultValue = null;
                Name = memberName;
            }

            public string MemberName => _memberName;
            public string Name { get; init; }
            public Type Type { get; init; }
            public object? DefaultValue { get; init; }
            public Func<Node, TInstance, Pin> CreatePin { get; init; }
            public string Summary => _summary ??= typeof(TInstance).GetSummary(_memberName);
            public string Remarks => _remarks ??= typeof(TInstance).GetRemarks(_memberName);
            public bool IsVisible { get; init; } = true;
        }

        private abstract class Pin : IVLPin
        {
            protected readonly Node Node;
            public TInstance Instance;

            protected Pin(Node node, TInstance instance)
            {
                Node = node;
                Instance = instance;
            }

            protected abstract object BoxedValue { get; set; }

            public virtual void Update(TInstance instance)
            {
                Instance = instance;
            }

            object IVLPin.Value
            {
                get => BoxedValue;
                set => BoxedValue = value;
            }
        }

        private abstract class Pin<T> : Pin, IVLPin<T>
        {
            protected readonly Func<TInstance, T> Getter;
            protected readonly Action<TInstance, T>? Setter;

            protected Pin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T>? setter) :
                base(node, instance)
            {
                Getter = getter;
                Setter = setter;
            }

            protected sealed override object BoxedValue
            {
                get => Value!;
                set => Value = (T)value;
            }

            public abstract T Value { get; set; }
        }

        private class InputPin<T> : Pin<T>
        {
            protected InputPin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter,
                T initialValue)
                : base(node, instance, getter, setter)
            {
                InitialValue = initialValue;
                setter(instance, initialValue);
            }

            private T InitialValue { get; }

            public override T Value
            {
                get => Getter(Instance);
                set
                {
                    Setter!(Instance, value ?? InitialValue);
                    Node.NeedsUpdate = true;
                }
            }

            public override void Update(TInstance instance)
            {
                var currentValue = Getter(Instance);
                base.Update(instance);
                Setter!(instance, currentValue);
            }
        }

        private class CachedInputPin<T> : InputPin<T>
        {
            private readonly Func<T, T, bool> _equals;
            private T _lastValue;

            public CachedInputPin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter,
                T initialValue, Func<T, T, bool>? @equals = null) : base(node, instance, getter, setter, initialValue)
            {
                _equals = @equals ?? EqualityComparer<T>.Default.Equals;
                _lastValue = initialValue;
            }

            public override T Value
            {
                get => Getter(Instance);
                set
                {
                    if (!_equals(value, _lastValue))
                    {
                        _lastValue = value;
                        base.Value = value;
                    }
                }
            }
        }


        private class OutputPin<T> : Pin<T>
        {
            public OutputPin(Node node, TInstance instance, Func<TInstance, T> getter)
                : base(node, instance, getter, null)
            {
            }

            public override T Value
            {
                get
                {
                    if (Node.NeedsUpdate)
                    {
                        Node.Update();
                    }

                    return Getter(Instance);
                }
                set => throw new InvalidOperationException();
            }
        }

        private class CachedOutputPin<T> : OutputPin<T>, IDisposable
        {
            private readonly IDisposable? _disposable;
            private T _cached;

            public CachedOutputPin(Node node, TInstance instance, Func<TInstance, T> getter,
                IDisposable? disposable = null) : base(node, instance, getter)
            {
                _disposable = disposable;
            }

            public override T Value
            {
                get
                {
                    if (Node.NeedsUpdate)
                    {
                        Node.Update();
                        _cached = Getter(Instance);
                    }

                    return _cached;
                }
                set => throw new InvalidOperationException();
            }

            public void Dispose()
            {
                _disposable?.Dispose();
            }
        }


        private class Node : VLObject, IVLNode
        {
            public Action? UpdateAction;
            public Action? DisposeAction;
            public bool NeedsUpdate;

            public Node(NodeContext nodeContext) : base(nodeContext)
            {
            }

            public IVLNodeDescription NodeDescription { get; init; }

            public IVLPin[] Inputs { get; set; }

            public IVLPin[] Outputs { get; set; }

            public void Dispose()
            {
                foreach (var p in Outputs)
                {
                    if (p is IDisposable d)
                    {
                        d.Dispose();
                    }
                }

                DisposeAction?.Invoke();
            }

            public void Update()
            {
                UpdateAction?.Invoke();
            }
        }
    }
}