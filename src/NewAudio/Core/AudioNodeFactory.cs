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
using Message = VL.Lang.Message;

namespace NewAudio.Core
{
    public static class AudioNodeFactory
    {
        public static AudioNodeDesc<T> NewNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor, Action<T>? update=null,
            string? name = null, string? category = null, bool hasStateOutput=false) where T : AudioNode
        {
            return new AudioNodeDesc<T>(factory, ctor: ctx=>CreateInstanceWithErrorToggle(ctx,ctor), name: name, category: category, hasStateOutput: hasStateOutput, update:update);
        }
        public static AudioNodeDesc<AudioProcessorNode<T>> NewProcessorNode<T>(this IVLNodeDescriptionFactory factory, Func<NodeContext, T> ctor, Action<AudioProcessorNode<T>>? update=null,
            string? name = null, string? category = null, bool hasAudioInput = true, bool hasAudioOutput = true, bool hasStateOutput=false) where T : AudioProcessor
        {
            return new AudioProcessorNodeDesc<T>(factory, ctor: ctx =>CreateInstanceWithErrorToggle(ctx, c =>
            {
                var processor = ctor(c);
                return new AudioProcessorNode<T>(processor);
            }),update, name: name, category: category, hasAudioInput:hasAudioInput, hasAudioOutput: hasAudioOutput, hasStateOutput: hasStateOutput);
        }

        private static (T, Action) CreateInstanceWithErrorToggle<T>(NodeContext ctx, Func<NodeContext, T> ctor) where T : AudioNode
        {
            var instance = ctor(ctx);
            Message[] visibleErrors = {};
                
            var unsubscribe = instance.Messages.Subscribe(ToggleMessage);
            return (instance, ()=>
            {
                foreach (var error in visibleErrors)
                {
                    Session.ToggleMessage(error, false);
                }

                unsubscribe.Dispose();
                instance.Dispose();
            });
            void ToggleMessage(Message[] errors)
            {
                foreach (var error in visibleErrors)
                {
                    if (Array.IndexOf(errors, error) == -1)
                    {
                        Session.ToggleMessage(error, false);                        
                    }
                }

                foreach (var error in errors)
                {
                    if (Array.IndexOf(visibleErrors, error) == -1)
                    {
                        Session.ToggleMessage(error, true);
                    }
                }

                visibleErrors = errors;
            }   
        }
    }

    public class AudioProcessorNodeDesc<TProcessor> : AudioNodeDesc<AudioProcessorNode<TProcessor>>
        where TProcessor : AudioProcessor
    {
        public AudioProcessorNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (AudioProcessorNode<TProcessor>, Action)> ctor,Action<AudioProcessorNode<TProcessor>>? update, string? name, string? category, bool hasAudioInput, bool hasAudioOutput, bool hasStateOutput) : base(factory, ctor, update, name, category, hasStateOutput)
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
        private readonly Action<TInstance>? Update;

        public AudioNodeDesc(IVLNodeDescriptionFactory factory, Func<NodeContext, (TInstance, Action)> ctor, Action<TInstance>? update,
            string? name, string? category, bool hasStateOutput)
        {
            Factory = factory;
            _ctor = ctor;
            Name = name ?? typeof(TInstance).Name;
            Category = category ?? string.Empty;
            Update = update;
            
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

        public IEnumerable<VL.Core.Diagnostics.Message> Messages => Enumerable.Empty<VL.Core.Diagnostics.Message>();

        public string Summary => typeof(TInstance).GetSummary();

        public string Remarks => typeof(TInstance).GetRemarks();
        public IObservable<object> Invalidated => Observable.Empty<object>();

        public IVLNode CreateInstance(NodeContext context)
        {
            var (instance, onDispose) = _ctor(context);
            var node = new Node(context)
            {
                NodeDescription = this,
            };
            int index = 0;
            IVLPin[] inputs = _inputs.Select(p => (IVLPin)p.CreatePin(node, index++, instance)).ToArray();
            IVLPin[] outputs = _outputs.Select(p => (IVLPin)p.CreatePin(node, index++, instance)).ToArray();
            instance.InputPinNames = _inputs.Select(i => i.MemberName).Concat(_outputs.Select(i=>i.MemberName).ToArray()).ToArray();
            
            node.Inputs = inputs;
            node.Outputs = outputs;
            List<Message> messages = new();
            
            node.UpdateAction = () =>
            {
                if (node.NeedsUpdate)
                {
                    try
                    {
                        var message = instance.Update(node.UpdateMask);
                        if (message != null)
                        {
                            instance.AddError(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        instance.AddError(new Message(MessageSeverity.Error, exception.Message));
                    }
                    finally
                    {
                        node.UpdateMask = 0;
                    }
                }
                Update?.Invoke(instance);
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
                CreatePin = (node, index, instance) => new CachedInputPin<T>(node, index, instance, getter, setter, getter(instance)),
                IsVisible = isVisible
            });
            return this;
        }

        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter, Func<T,T,bool> equals,
            string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, index, instance) => new CachedInputPin<T>(node, index, instance, getter, setter, getter(instance), equals),
                IsVisible = isVisible
            });
            return this;
        }
        public AudioNodeDesc<TInstance> AddInput<T>(string name, Func<TInstance, T> getter, Action<TInstance, T> setter, T defaultValue,
            string? summary = null, string? remarks = null, bool isVisible = true, string? displayName=null)
        {
            _inputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = displayName ?? name.InsertSpaces(),
                CreatePin = (node, index, instance) => new CachedInputPin<T>(node, index, instance, getter, setter, defaultValue),
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
        
        public AudioNodeDesc<TInstance> AddListInput<T>(string name, Func<TInstance, IEnumerable<T>> getter, Action<TInstance, IEnumerable<T>> setter)
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
                CreatePin = (node, index, instance) => new OutputPin<T>(node, index, instance, getter),
                IsVisible = isVisible
            });
            return this;
        }
        public AudioNodeDesc<TInstance> AddOutput<T>(string name, Func<(Func<TInstance, T>,IDisposable)> ctor, string? summary = null, string? remarks = null, bool isVisible = true)
        {
            _outputs.Add(new AudioPinDesc(name, typeof(T), summary, remarks)
            {
                Name = name.InsertSpaces(),
                CreatePin = (node, index, instance) =>
                {
                    var (getter, disposable) = ctor();
                    return new CachedOutputPin<T>(node, index, instance, getter, disposable);
                },
                IsVisible = isVisible
            });
            return this;
        }
        public AudioNodeDesc<TInstance> WithEnabledPins(bool output=true)
        {
            AddInput(nameof(AudioNode.IsEnable), x => x.IsEnable, (x, v) => x.IsEnable = v, false, displayName:"Enable");
            if (output)
            {
                AddOutput(nameof(AudioNode.IsEnabled), x => x.IsEnabled);
            }
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
            public Func<Node, int, TInstance, Pin> CreatePin { get; init; }
            public string Summary => _summary ??= typeof(TInstance).GetSummary(_memberName);
            public string Remarks => _remarks ??= typeof(TInstance).GetRemarks(_memberName);
            public bool IsVisible { get; init; } = true;
        }

        private abstract class Pin : IVLPin
        {
            protected readonly Node Node;
            public TInstance Instance;
            protected int Index;

            protected Pin(Node node, int index, TInstance instance)
            {
                Index = index;
                Node = node;
                Instance = instance;
            }

            protected abstract object BoxedValue { get; set; }

            protected virtual void Update(TInstance instance)
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

            protected Pin(Node node, int index, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T>? setter) : base(node, index, instance)
            {
                Getter = getter;
                Setter = setter;
            }

            protected sealed override object BoxedValue
            {
                get => Value;
                set => Value = (T)value;
            }

            public abstract T Value { get; set; }
        }

        private class InputPin<T> : Pin<T>
        {

            protected InputPin(Node node, int index, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter,
                T initialValue)
                : base(node, index, instance, getter, setter)
            {
                Index = index;
                InitialValue = initialValue;
                setter(instance, initialValue);
            }

            private T InitialValue { get; }

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
                    
                    Node.UpdateMask |= ((ulong)1<<Index);
                }
            }

            protected override void Update(TInstance instance)
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

        private class CachedInputPin<T>: InputPin<T>
        {
            private readonly Func<T, T, bool> _equals;
            private T _lastValue;

            public CachedInputPin(Node node, int index, TInstance instance, Func<TInstance, T> getter, Action<TInstance, T> setter, T initialValue, Func<T, T, bool>? @equals=null) : base(node,index, instance,getter, setter, initialValue)
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


        private class OutputPin<T> : Pin<T>
        {
            public OutputPin(Node node, int index, TInstance instance, Func<TInstance, T> getter)
                : base(node, index, instance, getter, null)
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
            private T? _cached;
            public CachedOutputPin(Node node, int index, TInstance instance, Func<TInstance, T> getter, IDisposable? disposable=null) : base(node, index, instance, getter)
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
        

        class Node : VLObject, IVLNode
        {
            public Action? UpdateAction;
            public Action? DisposeAction;
            public ulong UpdateMask;
            public bool NeedsUpdate => UpdateMask != 0;
            public List<Message>? CachedMessages;
            
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

