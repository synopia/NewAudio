﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;

namespace NewAudio
{
    public class OutputDevice: IDevice
    {
        private ILogger _logger = Log.ForContext<OutputDevice>();
        private IWavePlayer _waveOutput;
        private WaveOutputDevice _device;

        private readonly List<AudioLink> _inputs = new List<AudioLink>();
        private readonly List<IDisposable> _links = new List<IDisposable>();
        private readonly List<AudioFlowBuffer> _buffers = new List<AudioFlowBuffer>();
        private BlockingSampleProvider _provider;
        public BlockingSampleProvider SampleProvider => _provider;
        private AudioFlowSink _flow;
        public float Latency => _provider.LatencyMs;
        public int Overflows => _provider.Overflows;
        public int UnderRuns => _provider.UnderRuns;

        public bool IsPlaying { get; private set; }
        public AudioFormat Format { get; private set; }
        public int BufferSize { get; private set; }
        private int _references;
        public WaveOutputDevice Handle => _device;

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

        public OutputDevice(WaveOutputDevice device, AudioFormat format)
        {
            _device = device;
            BufferSize = 64 * format.BufferSize;
            Format = format;
            _flow = new AudioFlowSink(Format, BufferSize);

            _waveOutput = ((IWaveOutputFactory)device.Tag).Create(0);
            _provider = new BlockingSampleProvider(format, _flow.Buffer);
            var wave16 = new SampleToWaveProvider16(_provider);
            _waveOutput.Init(wave16);
        }

        public void Start()
        {
            if (!IsPlaying)
            {
                _logger.Information("Starting output device {device}", _device?.Value);
                _waveOutput.Play();
                IsPlaying = true;
            }
        }

        public void Stop()
        {
            if (IsPlaying)
            {
                _logger.Information("Stopping output device {device}", _device?.Value);
                _waveOutput.Stop();
                IsPlaying = false;
            }
        }

        public void AddAudioLink(AudioLink input)
        {

            _links.Add(input.SourceBlock.LinkTo(_flow));

            _inputs.Add(input);
            _logger.Information("New input assigned, total links: {links}, total inputs: {inputs}", _links.Count, _inputs.Count);
        }

        public void RemoveAudioLink(AudioLink input)
        {
            var index = _inputs.IndexOf(input);
            _logger.Information("Removing link [{input}] (index {index})", input, index);
            if (index != -1)
            {
                _links[index].Dispose();
                _links.RemoveAt(index);
                _inputs.RemoveAt(index);
            }
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
            _waveOutput.Dispose();
        }
    }
}