using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using VL.Core;
using VL.Core.Diagnostics;

namespace NewAudio.Core
{
    public static class AudioNodeFactory
    {
        public static AudioNodeDesc<T> NewNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor,
            string name = default, string category = default, bool hasStateOutput = true) where T : class
        {
            return new AudioNodeDesc<T>(factory, ctor: ctx =>
            {
                var instance = ctor(ctx);
                return (instance, default);
            }, name: name, category: category, hasStateOutput: hasStateOutput);
        }
    }

    public class AudioNodeDesc<TInstance> : IVLNodeDescription, IInfo where TInstance : class
    {
        private readonly List<AudioPinDesc> _inputs = new List<AudioPinDesc>();
        private readonly List<AudioPinDesc> _outputs = new List<AudioPinDesc>();
        private readonly Func<NodeContext, (TInstance, Action)> _ctor;

        public AudioNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (TInstance, Action)> ctor,
            string name, string category, bool hasStateOutput)
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
        public bool Fragmented => true;
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
            IVLPin[] inputs = _inputs.Select(p => (IVLPin)p.CreatePin(node, instance)).ToArray();
            IVLPin[] outputs = _outputs.Select(p => (IVLPin)p.CreatePin(node, instance)).ToArray();
            node.Inputs = inputs;
            node.Outputs = outputs;
            node.UpdateAction = () => { };
            node.DisposeAction = () =>
            {
                if (instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                onDispose?.Invoke();
            };
            return node;
        }

        public bool OpenEditor()
        {
            return false;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter,
            string summary = default, string remarks = default, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, summary, remarks)
            {
                Name = name.InsertSpaces(),
                Type = typeof(T),
                CreatePin = (node, instance) => new InputPin<T>(node, instance, getter, setter, getter(instance)),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddOutput<T>(string name, Func<TInstance, T> getter, string summary = default,
            string remarks = default, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, summary, remarks)
            {
                Name = name.InsertSpaces(),
                Type = typeof(T),
                CreatePin = (node, instance) => new OutputPin<T>(node, instance, getter),
                IsVisible = isVisible
            });
            return this;
        }

        class AudioPinDesc : IVLPinDescription, IInfo, IVLPinDescriptionWithVisibility
        {
            private readonly string _memberName;
            private string _summary;
            private string _remarks;

            public AudioPinDesc(string memberName, string summary = default, string remarks = default)
            {
                _memberName = memberName;
                _summary = summary;
                _remarks = remarks;
            }

            public string Name { get; set; }
            public Type Type { get; set; }
            public object DefaultValue { get; set; }
            public Func<Node, TInstance, Pin> CreatePin { get; set; }
            public string Summary => _summary ??= typeof(TInstance).GetSummary(_memberName);
            public string Remarks => _remarks ??= typeof(TInstance).GetRemarks(_memberName);
            public bool IsVisible { get; set; } = true;
        }

        abstract class Pin : IVLPin
        {
            public readonly Node Node;
            public TInstance Instance;

            public Pin(Node node, TInstance instance)
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
            public Func<TInstance, T> getter;
            public Action<TInstance, T> setter;

            public Pin(Node node, TInstance instance) : base(node, instance)
            {
            }

            public override sealed object BoxedValue
            {
                get => Value;
                set => Value = (T)value;
            }

            public abstract T Value { get; set; }
        }

        class InputPin<T> : Pin<T>, IVLPin
        {
            public InputPin(Node node, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter,
                T initialValue)
                : base(node, instance)
            {
                this.getter = getter;
                this.setter = setter;
                this.InitialValue = initialValue;
                setter(instance, initialValue);
            }

            public T InitialValue { get; }

            public override T Value
            {
                get => getter(Instance);
                set
                {
                    // Normalize the value first
                    if (value is null)
                        value = InitialValue;

                    setter(Instance, value);
                    Node.NeedsUpdate = true;
                }
            }

            public override void Update(TInstance instance)
            {
                var currentValue = getter(Instance);
                base.Update(instance);
                setter(instance, currentValue);
            }
        }

        class OutputPin<T> : Pin<T>
        {
            public OutputPin(Node node, TInstance instance, Func<TInstance, T> getter)
                : base(node, instance)
            {
                this.getter = getter;
            }

            public override T Value
            {
                get
                {
                    if (Node.NeedsUpdate)
                        Node.Update();
                    return getter(Instance);
                }
                set => throw new InvalidOperationException();
            }
        }

        class Node : VLObject, IVLNode
        {
            public Action UpdateAction;
            public Action DisposeAction;
            public bool NeedsUpdate = true;

            public Node(NodeContext nodeContext) : base(nodeContext)
            {
            }

            public IVLNodeDescription NodeDescription { get; set; }

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

