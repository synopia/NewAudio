using System;
using System.Collections.Generic;
using NAudio.Wave;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave.SampleProviders;
using NewAudio.Internal;
using Stride.Core;

namespace NewAudio
{
    public interface IDevice : IDisposable
    {
        public void Start();
        public void Stop();
    }
    public class OutputDevice: IDevice
    {
        private Logger _logger = LogFactory.Instance.Create("OutputDevice");
        private IWavePlayer _waveOutput;
        private AudioFormat _format;
        private int _bufferSize;
        private AudioFlowBuffer _buffer;

        private readonly List<AudioLink> _inputs = new List<AudioLink>();
        private readonly List<IDisposable> _links = new List<IDisposable>();

        public BufferedSampleProvider Buffer => _buffer.Buffer;
        
        public OutputDevice(WaveOutputDevice device, AudioFormat format)
        {
            
            int bufferSize = 64 * format.BufferSize;
            _format = format;
            _bufferSize = bufferSize;
            _buffer = new AudioFlowBuffer(format, bufferSize);
            _buffer.Buffer.WaveFormat = format.WaveFormat;

            _waveOutput = ((IWaveOutputFactory)device.Tag).Create(0);
            var wave16 = new SampleToWaveProvider16(new BlockingSampleProvider(format, _buffer));
            _waveOutput.Init(wave16);
        }

        public void Start()
        {
            _waveOutput.Play();
        }

        public void Stop()
        {
            _waveOutput.Stop();
        }

        public void AddAudioLink(AudioLink input)
        {
            var link = input.SourceBlock.LinkTo(_buffer);
            _links.Add(link);
            _inputs.Add(input);
        }

        public void Dispose()
        {
            Stop();
            foreach (var link in _links)
            {
                link.Dispose();
            }

            foreach (var input in _inputs)
            {
                input.Dispose();
            }
            _buffer.Dispose();
            _waveOutput.Dispose();
        }
    }
    public class InputDevice: IDevice
    {
        private Logger _logger = LogFactory.Instance.Create("InputDevice");
        private IWaveIn _waveInput;
        private AudioFormat _format;
        private int _bufferSize;
        private AudioFlowBuffer _buffer;
        private AudioBufferFactory _audioBufferFactory = new AudioBufferFactory();

        private readonly List<AudioLink> _outputs = new List<AudioLink>();
        private readonly List<IDisposable> _links = new List<IDisposable>();
        private SampleTimer _timer;
        public BufferedSampleProvider Buffer => _buffer.Buffer;
        public AudioFlowBuffer OutputBuffer => _buffer;
        
        private readonly BufferBlock<AudioBuffer> _bufferIn =
            new BufferBlock<AudioBuffer>(new DataflowBlockOptions()
            {
                BoundedCapacity = 2,
                MaxMessagesPerTask = 2
            });

        public InputDevice(SampleTimer timer, WaveInputDevice device, AudioFormat format)
        {
            _timer = timer;
            int bufferSize = 64 * format.BufferSize;
            _format = format;
            _bufferSize = bufferSize;
            _buffer = new AudioFlowBuffer(format, bufferSize, format.BufferSize);

            var waveFormat = new WaveFormat(format.SampleRate, 16, 2);

            _waveInput = ((IWaveInputFactory)device.Tag).Create(waveFormat, 0);
            format = format.Update(_waveInput.WaveFormat);
            
            _buffer.Buffer.WaveFormat = format.WaveFormat;
            _links.Add(_bufferIn.LinkTo(_buffer));
            
            _waveInput.DataAvailable += (s, a) =>
            {
                try
                {
                    int time = _timer.Advance(a.BytesRecorded / (format.WaveFormat.BitsPerSample / 8));
                    var b = _audioBufferFactory.FromByteBuffer(time, format.WaveFormat, a.Buffer, a.BytesRecorded);
                    _bufferIn.Post(b);
                }
                catch (Exception e)
                {
                    _logger.Error(e);
                }
            };
            _logger.Info($"Created Format: {_format}, buffer: {_bufferSize}, {device.Value}");
        }

        public void Start()
        {
            _waveInput.StartRecording();
        }

        public void Stop()
        {
            _waveInput.StopRecording();
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
            _buffer.Dispose();
            _waveInput.Dispose();
        }
    }

    public class DeviceManager : IDevice
    {
        private Dictionary<WaveOutputDevice, OutputDevice> _outputDevices = new Dictionary<WaveOutputDevice, OutputDevice>();
        private readonly Dictionary<WaveInputDevice, InputDevice> _inputDevices = new Dictionary<WaveInputDevice, InputDevice>();
        private AudioFormat _audioFormat;
        private SampleTimer _timer = new SampleTimer();
        
        public DeviceManager()
        {
            _audioFormat = new AudioFormat(0, 48000, 256);
        }

        public InputDevice GetInputDevice(WaveInputDevice deviceHandle)
        {
            if (_inputDevices.ContainsKey(deviceHandle))
            {
                return _inputDevices[deviceHandle];
            }

            var device = new InputDevice(_timer, deviceHandle, _audioFormat.WithChannels(2));

            _inputDevices[deviceHandle] = device;
            return device;
        }
        
        public OutputDevice GetOutputDevice(WaveOutputDevice deviceHandle)
        {
            if (_outputDevices.ContainsKey(deviceHandle))
            {
                return _outputDevices[deviceHandle];
            }

            var device = new OutputDevice(deviceHandle, _audioFormat.WithChannels(2));

            _outputDevices[deviceHandle] = device;
            return device;
        }

        public void Start()
        {
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Start();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Start();
            }
        }

        public void Stop()
        {
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Stop();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Stop();
            }
        }

        public void Dispose()
        {
            foreach (var inputDevice in _inputDevices.Values)
            {
                inputDevice.Dispose();
            }

            foreach (var outputDevice in _outputDevices.Values)
            {
                outputDevice.Dispose();
            }
            
        }
    }
}