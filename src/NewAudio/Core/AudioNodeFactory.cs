using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using NewAudio.Nodes;
using NewAudio.Processor;
using VL.Core;
using VL.Core.Diagnostics;
using VL.Lang;
using VL.Lang.PublicAPI;

namespace NewAudio.Core
{
    public static class AudioNodeFactory
    {
        public static AudioNodeDesc<T> NewNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor,
            string? name = null, string? category = null, bool hasStateOutput=false) where T : AudioNode
        {
            return new AudioNodeDesc<T>(factory, ctor: ctx =>
            {
                var instance = ctor(ctx);
                return (instance, instance.Dispose);
            }, name: name, category: category, hasStateOutput: hasStateOutput);
        }
        public static AudioNodeDesc<AudioProcessorNode<T>> NewProcessorNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor,
            string? name = null, string? category = null, bool hasAudioInput = true, bool hasAudioOutput = true, bool hasStateOutput=false) where T : AudioProcessor
        {
            return new AudioProcessorNodeDesc<T>(factory, ctor: ctx =>
            {
                var processor = ctor(ctx);
                var node = new AudioProcessorNode<T>(processor);

                return (node, node.Dispose);
            }, name: name, category: category, hasAudioInput:hasAudioInput, hasAudioOutput: hasAudioOutput, hasStateOutput: hasStateOutput);
        }
    }

    public class AudioProcessorNodeDesc<TProcessor> : AudioNodeDesc<AudioProcessorNode<TProcessor>>
        where TProcessor : AudioProcessor
    {
        public AudioProcessorNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (AudioProcessorNode<TProcessor>, Action)> ctor, string? name, string? category, bool hasAudioInput, bool hasAudioOutput, bool hasStateOutput) : base(factory, ctor, name, category, hasStateOutput)
        {
            if (hasAudioInput)
            {
                AddInput("Audio In", x => x.Input, (x,v)=>x.Input = v);
            }
            if (hasAudioOutput)
            {
                AddOutput("Audio Out", x => x.Output);
            }
        }
    }
    public class AudioNodeDesc<TInstance> : IVLNodeDescription, IInfo where TInstance : AudioNode
    {
        private readonly List<AudioPinDesc> _inputs = new();
        private readonly List<AudioPinDesc> _outputs = new();
        private readonly Func<NodeContext, (TInstance, Action)> _ctor;

        public AudioNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (TInstance, Action)> ctor,
            string? name, string? category, bool hasStateOutput)
        {
            Factory = factory;
            _ctor = ctor;
            Name = name ?? typeof(TInstance).Name;
            Category = category ?? string.Empty;

            if (hasStateOutput)
            {
                AddOutput("Output", x => x);
            }
        }

        public IVLNodeDescriptionFactory Factory { get; }
        public string Name { get; }
        public string Category { get; }
        public bool Fragmented => false;
        public IReadOnlyList<IVLPinDescription> Inputs => _inputs;

        public IReadOnlyList<IVLPinDescription> Outputs => _outputs;
        public IEnumerable<VL.Core.Diagnostics.Message> Messages =>  Enumerable.Empty<VL.Core.Diagnostics.Message>();

        // {
            // get { yield return new Message(MessageType.Error, "Hallo");  }
        // }

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
            IVLPin[] inputs = _inputs.Select(p => (IVLPin)p.CreatePin(node, instance)).ToArray();
            IVLPin[] outputs = _outputs.Select(p => (IVLPin)p.CreatePin(node, instance)).ToArray();
            node.Inputs = inputs;
            node.Outputs = outputs;
            var cachedMessages = new List<VL.Lang.Message>();
            node.UpdateAction = () =>
            {
                if (cachedMessages.Count != instance.Messages.Count)
                {
                    if (instance.Messages.Count == 0)
                    {
                        foreach (var message in cachedMessages)
                        {
                            Session.ToggleMessage(message, false);
                        }
                        cachedMessages.Clear();
                    }
                    else
                    {
                        foreach (var message in instance.Messages)
                        {
                            var m = new VL.Lang.Message(context.Path.Stack.Peek(), MessageSeverity.Error, message);
                            cachedMessages.Add(m);
                            Session.ToggleMessage(m, true);
                        }
                    }

                }

                if (node.NeedsUpdate)
                {
                    /*foreach (var input in inputs)
                    {
                        input.Update(instance);
                    }

                    foreach (var output in outputs)
                    {
                        output.Instance = instance;
                    }*/
                    node.NeedsUpdate = false;
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
                CreatePin = (node, instance) => new InputPin<T>(node, instance, getter, setter, getter(instance)),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter, T defaultValue,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new InputPin<T>(node, instance, getter, setter, defaultValue),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddCachedInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter, Func<T,T,bool> equals,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new CachedInputPin<T>(node, instance, getter, setter, getter(instance), equals),
                IsVisible = isVisible
            });
            return this;
        }
        public AudioNodeDesc<TInstance> AddCachedInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter, T defaultValue,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, instance) => new CachedInputPin<T>(node, instance, getter, setter, defaultValue),
                IsVisible = isVisible
            });
            return this;
        }

        static bool SequenceEqual<T>(IEnumerable<T>? one, IEnumerable<T>? two)
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
        
        public AudioNodeDesc<TInstance> AddCachedListInput<T>(string name, Func<TInstance, IList<T>> getter, Action<TInstance>? updateAfterSet=null)
        {
            return AddCachedInput(name, getter: i => (IReadOnlyList<T>)getter(i),
                equals: SequenceEqual, setter:
                (x, v) =>
                {
                    var items = getter(x);
                    items.Clear();
                    if (v != null)
                    {
                        foreach (var item in v)
                        {
                            if (item != null)
                            {
                                items.Add(item);
                            }
                        }
                    }

                    updateAfterSet?.Invoke(x);
                });
        }
        
        public AudioNodeDesc<TInstance> AddCachedListInput<T>(string name, Func<TInstance, T[]> getter, Action<TInstance, T[]> setter)
        {
            return AddCachedInput(name, getter: i => (IReadOnlyList<T>)getter(i),
                equals: SequenceEqual, setter:
                (x, v) =>
                {
                    var items = v?.Where(i => i != null);
                    setter(x, items?.ToArray() ?? Array.Empty<T>());
                });
        }

        public AudioNodeDesc<TInstance> AddCachedListInput<T>(string name, Func<TInstance, IEnumerable<T>> getter, Action<TInstance, IEnumerable<T>> setter)
        {
            return AddCachedInput(name, getter: getter,
                equals: SequenceEqual, setter:
                (x, v) =>
                {
                    var items = v?.Where(i => i != null);
                    setter(x, items ?? Array.Empty<T>());
                });
        }

        
        public AudioNodeDesc<TInstance> AddOutput<T>(string name, Func<TInstance, T> getter, string? summary = null,
            string? remarks = null, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, typeof(T), remarks)
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
        public AudioNodeDesc<TInstance> AddCachedOutput<T>(string name, Func<(Func<TInstance, T>,IDisposable)> ctor, string? summary = null, string? remarks = null, bool isVisible = true)
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
        public AudioNodeDesc<TInstance> WithEnabledPin(bool output=true)
        {
            AddInput("Enable", x => x.Enable, (x, v) => x.Enable = v);
            if (output)
            {
                AddOutput("Enabled", x => x.Enabled);
            }
            return this;
        }

        class AudioPinDesc : IVLPinDescription, IInfo, IVLPinDescriptionWithVisibility
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

            public string Name { get; init; }
            public Type Type { get; init; }
            public object? DefaultValue { get; init; }
            public Func<Node, TInstance, Pin> CreatePin { get; init; }
            public string Summary => _summary ??= typeof(TInstance).GetSummary(_memberName);
            public string Remarks => _remarks ??= typeof(TInstance).GetRemarks(_memberName);
            public bool IsVisible { get; init; } = true;
        }

        abstract class Pin : IVLPin
        {
            protected readonly Node Node;
            public TInstance Instance;

            protected Pin(Node node, TInstance instance)
            {
                Node = node;
                Instance = instance;
            }

            public abstract object BoxedValue { get; set; }

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

        abstract class Pin<T> : Pin, IVLPin<T>
        {
            protected readonly Func<TInstance, T> Getter;
            protected readonly Action<TInstance, T>? Setter;

            protected Pin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T>? setter) : base(node, instance)
            {
                Getter = getter;
                Setter = setter;
            }

            public sealed override object BoxedValue
            {
                get => Value;
                set => Value = (T)value;
            }

            public abstract T Value { get; set; }
        }

        class InputPin<T> : Pin<T>
        {
            public InputPin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter,
                T initialValue)
                : base(node, instance, getter, setter)
            {
                InitialValue = initialValue;
                setter(instance, initialValue);
            }

            public T InitialValue { get; }

            public override T Value
            {
                get => Getter(Instance);
                set
                {
                    if (Setter is null)
                    {
                        throw new InvalidOperationException("Setter is null");
                    }

                    Setter(Instance, value ?? InitialValue);
                    Node.NeedsUpdate = true;
                }
            }

            public override void Update(TInstance instance)
            {
                var currentValue = Getter(Instance);
                base.Update(instance);
                if (Setter is null)
                {
                    throw new InvalidOperationException("Setter is null");
                }
                
                Setter(instance, currentValue);
            }
        }

        class CachedInputPin<T>: InputPin<T>, IVLPin
        {
            private readonly Func<T, T, bool> _equals;
            private T _lastValue;

            public CachedInputPin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter, T initialValue, Func<T, T, bool>? @equals=null) : base(node, instance, getter, setter, initialValue)
            {
                _equals = @equals  ?? EqualityComparer<T>.Default.Equals;
                _lastValue = initialValue;
            }

            public override T Value
            {
                get=>Getter(Instance);
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
        
        
        
        class OutputPin<T> : Pin<T>
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
                        Node.Update();
                    return Getter(Instance);
                }
                set => throw new InvalidOperationException();
            }
        }
        
        class CachedOutputPin<T>: OutputPin<T>, IDisposable
        {
            private readonly IDisposable? _disposable;
            private T _cached;
            public CachedOutputPin(Node node, TInstance instance, Func<TInstance, T> getter, IDisposable? disposable=null) : base(node, instance, getter)
            {
                _disposable = disposable;
            }

            public override T Value
            {
                get
                {
                    if (Node.NeedsUpdate || _cached==null)
                    {
                         Node.Update();
                         _cached =Getter(Instance);
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
        

        class Node : VLObject, IVLNode
        {
            public Action? UpdateAction;
            public Action? DisposeAction;
            public bool NeedsUpdate = true;
            
            public Node(NodeContext nodeContext) : base(nodeContext)
            {
            }

            public IVLNodeDescription NodeDescription { get; init; }

            public IVLPin[] Inputs { get; set; }

            public IVLPin[] Outputs { get; set; }

            public void Dispose()
            {
                foreach (var p in Outputs)
                    if (p is IDisposable d)
                        d.Dispose();

                DisposeAction?.Invoke();
            }

            public void Update() => UpdateAction?.Invoke();
        }
    }
}

