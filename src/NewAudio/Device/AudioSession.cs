using System;
using System.Buffers;
using System.Diagnostics;
using System.Threading;
using NewAudio.Device;
using NewAudio.Dsp;
using Xt;

namespace VL.NewAudio.Device
{
    public interface IAudioSession
    {
        void Start();
        void Stop();
    }
    public class AudioSession : IAudioSession, IAudioStreamCallback, IDisposable
    {
        private XtAudioDevice _owner;
        private XtDevice _outputDevice;
        private XtDevice _inputDevice;
        private int _maxInputChannels;
        private int _maxOutputChannels;
        private AudioChannels _inputChannels;
        private AudioChannels _outputChannels;
        private int _sampleRate;
        private XtSample _sampleType;
        private double _bufferSize;
        private bool _interleavedInput;
        private bool _interleavedOutput;

        private AudioStream _inputStream;
        private AudioStream _outputStream;

        private int _audioCallbackGuard;
        public double InputLatency { get; private set; }
        public double OutputLatency { get; private set; }
        public int XRuns { get; private set; }
        public int FramesPerBlock => _outputStream.FramesPerBlock;

        public AudioSession(XtAudioDevice owner, XtDevice inputDevice, XtDevice outputDevice, int maxInputChannels,
            AudioChannels inputChannels, int maxOutputChannels, AudioChannels outputChannels, bool interleavedInput,
            bool interleavedOutput, int sampleRate, XtSample sampleType, double bufferSize)
        {
            _owner = owner;
            _inputDevice = inputDevice;
            _outputDevice = outputDevice;
            _maxInputChannels = maxInputChannels;
            _inputChannels = inputChannels;
            _maxOutputChannels = maxOutputChannels;
            _outputChannels = outputChannels;
            _sampleRate = sampleRate;
            _sampleType = sampleType;
            _bufferSize = bufferSize;
            _interleavedInput = interleavedInput;
            _interleavedOutput = interleavedOutput;

            Trace.Assert(outputChannels.Count > 0);

            if (inputChannels.Count == 0)
            {
                _outputStream = new AudioStream(outputDevice, maxOutputChannels, interleavedOutput, true,
                    outputChannels, sampleRate, sampleType, bufferSize, this);
                _outputStream.CreateStream();
            }
            else
            {
                if (_inputDevice == null)
                {
                    _outputStream = new AudioStream(outputDevice, maxOutputChannels, interleavedOutput, true,
                        outputChannels, sampleRate, sampleType, bufferSize, this);
                    
                    _inputStream = new AudioStream(outputDevice, maxInputChannels, interleavedInput, false,
                        inputChannels, sampleRate, sampleType, bufferSize, null);
                    
                    _outputStream.CreateFullDuplexStream(_inputStream);
                }
                else
                {
                    _outputStream = new AudioStream(outputDevice, maxOutputChannels, interleavedOutput, true,
                        outputChannels, sampleRate, sampleType, bufferSize, this);
                    
                    _inputStream = new AudioStream(inputDevice, maxInputChannels, interleavedInput, false,
                        inputChannels, sampleRate, sampleType, bufferSize, this);
                    
                    _outputStream.CreateStream();
                    _inputStream.CreateStream(); // TODO
                }
            }
        }

        private bool _disposed;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _outputStream?.Dispose();
                _inputStream?.Dispose();

                _outputStream = null;
                _inputStream = null;
            }
        }

        public void Start()
        {
            _audioCallbackGuard = 0;
            if (_inputStream != null)
            {
                _inputStream.Start();
            }

            _outputStream.Start();
        }

        public void Stop()
        {
            while (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 1)
            {
                Thread.Sleep(1);
            }

            _inputStream?.Dispose();
            _outputStream?.Dispose();
            _inputStream = null;
            _outputStream = null;

            _audioCallbackGuard = 0;
        }


        public int OnBuffer(XtStream stream, in XtBuffer buffer, object user)
        {
            if (Interlocked.CompareExchange(ref _audioCallbackGuard, 1, 0) == 0)
            {
                try
                {
                    Trace.Assert(stream != null && buffer.output != null);
                    _owner.Process(_inputStream.Bind(buffer), _outputStream.Bind(buffer), buffer.frames);

                    var latency = _outputStream.GetLatency();
                    OutputLatency = latency.output;
                    InputLatency = latency.input;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                finally
                {
                    _audioCallbackGuard = 0;
                }

            }

            return 0;
        }

        public void OnXRun(XtStream stream, int index, object user)
        {
            XRuns++;
        }

        public void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            Console.WriteLine($"OnRunning {running}, {error}");
        }

        
    }
}