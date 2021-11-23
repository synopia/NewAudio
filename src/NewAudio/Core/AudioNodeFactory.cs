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
        /*
        private readonly NodeFactoryCache _factoryCache = new();
        private readonly string _identifier = "NewAudio";
        public string Identifier => _identifier;
        private ImmutableArray<IVLNodeDescription> _descriptions;
        public ImmutableArray<IVLNodeDescription> NodeDescriptions => _descriptions;
        public IObservable<object> Invalidated => Observable.Empty<object>();
        public AudioNodeFactory()
        {
            var builder = ImmutableArray.CreateBuilder<IVLNodeDescription>();
            builder.Add(new ModelDescription(this, "Test"));
            _descriptions = builder.ToImmutable();
        }

        string GetIdentifierForPath(string path) => $"{_identifier} ({path})";
        
        public IVLNodeDescriptionFactory ForPath(string path)
        {
            var identifier = GetIdentifierForPath(path);
            return _factoryCache.GetOrAdd(identifier, () =>
            {
                return new AudioNodeFactory();
            });
        }
        */

        class ModelPinDescription : IVLPinDescription, IInfo
        {
            public string Name { get; }
            public Type Type { get; }
            public object DefaultValue { get; }
            public string Summary { get; }
            public string Remarks { get; }

            public ModelPinDescription(string name, Type type, object defaultValue, string summary, string remarks)
            {
                Name = name;
                Type = type;
                DefaultValue = defaultValue;
                Summary = summary;
                Remarks = remarks;
            }
        }

        class ModelDescription : IVLNodeDescription, IInfo
        {
            private List<ModelPinDescription> _inputs = new ();
            private List<ModelPinDescription> _outputs = new ();
            private string _name;
            
            public ModelDescription(IVLNodeDescriptionFactory factory, string name)
            {
                Factory = factory;
                _name = name;
                _inputs.Add(new ModelPinDescription("ONE", typeof(int), 1, "Summary", "Remarks"));
                _outputs.Add(new ModelPinDescription("OUT", typeof(int), 1, "Summary", "Remarks"));
            }

            public IVLNode CreateInstance(NodeContext context)
            {
                return new AudioNode(this, context);
            }

            public bool OpenEditor()
            {
                return true;
            }

            public IVLNodeDescriptionFactory Factory { get; }
            public string Name => _name;
            public string Category => "NewAudio";
            public bool Fragmented => false;
            public IReadOnlyList<IVLPinDescription> Inputs => _inputs;
            public IReadOnlyList<IVLPinDescription> Outputs => _outputs;

            public IEnumerable<Message> Messages
            {
                get
                {
                    yield return new Message(MessageType.Warning, "Soooo");
                }
            }

            public IObservable<object> Invalidated => Observable.Empty<object>();
            public string Summary => "";
            public string Remarks => "";
        }

        class AudioNode : VLObject, IVLNode
        {
            class AudioPin : IVLPin
            {
                public object Value { get; set; }
                public string Name { get; set; }
            }

            private readonly ModelDescription _description;

            public AudioNode(ModelDescription description, NodeContext nodeContext) : base(nodeContext)
            {
                _description = description;
                Inputs = description.Inputs.Select(d => new AudioPin(){Name=d.Name}).ToArray();
                Outputs = description.Outputs.Select(d => new AudioPin(){Name=d.Name}).ToArray();
            }

            public void Dispose()
            {
            
            }

            public void Update()
            {
            
            }

            public IVLNodeDescription NodeDescription => _description;
            public IVLPin[] Inputs { get; }
            public IVLPin[] Outputs { get; }
        }
        

        public static IEnumerable<IVLNodeDescription> CreateNode(IVLNodeDescriptionFactory factory)
        {
            yield return new ModelDescription(factory, "TEST");
        }
    }
}