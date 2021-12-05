using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Forms;
using NewAudio;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Nodes;
using NewAudio.Processor;
using VL.Lang;
using Message = VL.Lang.Message;

namespace VL.NewAudio.Nodes
{
    public class AudioStreamNode : AudioNode
    {
        private readonly AudioGraph _graph;
        private readonly IAudioControl _control;
        // private AudioLink? _input;
        // private readonly AudioGraph.Node _graphOutputNode;
        private bool _disposed;
        private IAudioSession? _session;

        public override bool IsEnabled => _control.IsRunning;
        /// <summary>
        /// Primary audio output configuration. This device will drive the output.
        /// </summary>
        /// <remarks>
        /// TODO multiple output devices
        /// </remarks>
        public AudioStreamConfig? Primary { get; set; }
        /// <summary>
        /// Secondary audio input configurations. You may use as many devices/configurations as you wish. However,
        /// only few combinations will allow low latency.  
        /// </summary>
        /// <remarks>
        /// If possible:
        /// <list type="bullet">
        /// <item>Use one ASIO device for input and output. Best performance, lowest latency.</item>
        /// <item>Use Wasapi only for aggregation support. May perform better than using ASIO, since its native code.</item>
        /// <item>Any other combination only works using ring buffers, which will introduce latency</item>
        /// </list>
        /// </remarks>
        public IEnumerable<AudioStreamConfig> Secondary { get; set; }

        public double InputLatency => _session?.InputLatency ?? 0;
        public double OutputLatency => _session?.OutputLatency ?? 0;
        
        /// <summary>
        /// Returns the current type of the session. 
        /// </summary>
        public AudioStreamType? Type => _session?.Type;
        /*
        public AudioLink? Input
        {
            get => _input;
            set
            {
                if (_input == value)
                {
                    return;
                }

                _input?.Disconnect(_graph);

                _input = value;

                _input?.Connect(_graph, _graphOutputNode);
            }
        }
        */

        public AudioStreamNode()
        {
            _graph = Resources.GetAudioGraph().Resource;
            
            _control = new XtAudioControl(AudioService);
            // AudioGraphIOProcessor graphOutput = new(true);
            // _graphOutputNode = _graph.AddNode(graphOutput)!;
            
            AudioProcessorPlayer graphPlayer = new();
            graphPlayer.SetProcessor(_graph);
            _control.AddAudioCallback(graphPlayer);

            Secondary = Array.Empty<AudioStreamConfig>();
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _disposed = true;
                    _control.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        public override Message? Update(ulong mask)
        {
            if (Primary == null)
            {
                return null;
            }

            if (!IsEnable)
            {
                _control.Close();
                return null;
            }
            
            if (HasChanged(nameof(IsEnable), mask) || HasChanged(nameof(Primary), mask) || HasChanged(nameof(Secondary), mask))
            {
                
                _session = _control.Open(Primary, Secondary.ToArray());
                var ins = _session.ActiveInputChannels.Count;
                var outs = _session.ActiveOutputChannels.Count;
                _graph.SetChannels(ins, outs);
            }

            return null;
        }
    }
}