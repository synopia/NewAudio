using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    public class InputDevice: IDevice
    {
        private readonly Logger _logger = LogFactory.Instance.Create("InputDevice");
        private readonly AudioBufferFactory _audioBufferFactory = new AudioBufferFactory();
        private readonly IWaveIn _waveInput;
        private AudioFlowSource _flow;
        private WaveInputDevice _device;
        private readonly List<AudioLink> _outputs = new List<AudioLink>();
        private readonly List<IDisposable> _links = new List<IDisposable>();
        public int Overflows => _flow.Buffer.Overflows;
        public int UnderRuns => _flow.Buffer.UnderRuns;
        public int BufferedSamples => _flow.Buffer.BufferedSamples;
        private readonly BroadcastBlock<AudioBuffer> _broadcastBlock;

        public ISourceBlock<AudioBuffer> OutputBuffer => _flow;
        public AudioFormat Format { get; private set; }
        public int BufferSize { get; private set; }
        private int _references;

        public WaveInputDevice Handle => _device;
        public void IncreaseRef()
        {
            _references++;
        }

        public bool DecreaseRef()
        {
            _references--;
            if (_references == 0)
            {
                return true;
            }

            return false;
        }
        
        public bool IsPlaying { get; private set; }
        private readonly BufferBlock<AudioBuffer> _bufferIn =
            new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
                // BoundedCapacity = 2,
                // MaxMessagesPerTask = 2
            });

        public InputDevice( WaveInputDevice device, AudioFormat format)
        {
            _device = device;
            BufferSize = 64 * format.BufferSize;

            var waveFormat = new WaveFormat(format.SampleRate, 16, 2);
            _waveInput = ((IWaveInputFactory)device.Tag).Create(waveFormat, 0);
            
            Format = format.Update(_waveInput.WaveFormat);
            _flow = new AudioFlowSource(Format, BufferSize);
            _flow.Buffer.WaveFormat = Format.WaveFormat;
            _links.Add(_bufferIn.LinkTo(_flow));
            
            _broadcastBlock = new BroadcastBlock<AudioBuffer>(i=>
            {
                _logger.Trace($"Broadcast {i.Count} at {i.Time}");
                return i;
            });
            // _flow.LinkTo(_broadcastBlock);
            _waveInput.DataAvailable += (s, a) =>
            {
                try
                {
                    var b = _audioBufferFactory.FromByteBuffer(format.WaveFormat, a.Buffer, a.BytesRecorded);
                    _logger.Trace($"bufIn {_flow.Buffer.FreeSpace}");
                    _flow.Post(b);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }
            };
        }

        public void Start()
        {
            if (!IsPlaying)
            {
                _logger.Info($"Starting input device {_device?.Value}");
                _waveInput.StartRecording();
                IsPlaying = true;
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                _logger.Info($"Stopping input device {_device?.Value}");
                _waveInput.StopRecording();
                IsPlaying = false;
            }
        }

        public void Dispose()
        {
            Stop();
            foreach (var link in _links)
            {
                link.Dispose();
            }

            foreach (var output in _outputs)
            {
                output.Dispose();
            }
            _waveInput.Dispose();
        }
    }
    
}