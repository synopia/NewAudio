using System;
using System.Collections.Generic;
using VL.NewAudio.Backend;
using VL.NewAudio.Core;

namespace VL.NewAudio.Nodes
{
    // _graph = Resources.GetAudioGraph().Resource;
    // AudioProcessorPlayer graphPlayer = new();
    // graphPlayer.SetProcessor(_graph);
    // _control.AddAudioCallback(graphPlayer);
    // private readonly AudioGraph _graph;
    // private readonly AudioGraph.Node _graphOutputNode;
    /**
     *             var ins = _control.TotalInputChannels;
            var outs = _control.TotalOutputChannels;
            _graph.SetChannels(ins, outs);

     */
    public class AudioSessionNode : AudioNode
    {
        private readonly AudioStreamBuilder _streamBuilder = new();
        private readonly IAudioControl _control;
        private IAudioCallback? _input;
        private bool _disposed;
        private IAudioSession? _session;

        public override bool IsEnabled => _control.IsRunning;

        /// <summary>
        /// Primary audio output configuration. This device will drive the output.
        /// </summary>
        /// <remarks>
        /// TODO multiple output devices
        /// </remarks>
        public AudioStreamConfig? Primary
        {
            get => _streamBuilder.Primary;
            set => _streamBuilder.Primary = value;
        }

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
        public IEnumerable<AudioStreamConfig> Secondary
        {
            get => _streamBuilder.Secondary;
            set => _streamBuilder.Secondary = value;
        }

        public int XRuns => _session?.XRuns ?? 0;
        public double CpuUsage => _session?.CpuUsage ?? 0.0;
        public IEnumerable<string> Times => _session?.Times ?? Array.Empty<string>();
        public double InputLatency => _session?.InputLatency ?? 0;
        public double OutputLatency => _session?.OutputLatency ?? 0;

        /// <summary>
        /// Returns the current type of the session. 
        /// </summary>
        public AudioStreamType? Type => _session?.Type;

        public IAudioCallback? Input
        {
            get => _input;
            set
            {
                if (_input == value)
                {
                    return;
                }

                if (_input != null)
                {
                    _control.RemoveAudioCallback(_input);
                }

                _input = value;
                if (_input != null)
                {
                    _control.AddAudioCallback(_input);
                }
            }
        }


        public AudioSessionNode()
        {
            _control = new XtAudioControl(AudioService);
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


        public void Update()
        {
            if (Primary == null)
            {
                return;
            }

            if (!IsEnable)
            {
                _control.Close();
                _session = null;
                return;
            }


            _session = _control.Open(_streamBuilder);
        }
    }
}