using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NAudio.Wave;
using NewAudio.Internal;

namespace NewAudio
{
    public class BlockingSampleProvider : ISampleProvider, IDisposable
    {
        private readonly Logger _logger = LogFactory.Instance.Create("BlockingSampleProvider");
        public AudioFormat Format;
        public WaveFormat WaveFormat => Format.WaveFormat;
        private BufferedSampleProvider _sampleProvider;

        public int Overflows => _sampleProvider.Overflows;
        public int UnderRuns => _underruns;
        private int _latencyCounter;
        private int _latencySum;
        private int _underruns;
        public int Latency
        {
            get
            {
                if (_latencyCounter == 0)
                {
                    return 0;
                }
                var v = _latencySum / _latencyCounter;
                _latencyCounter = 0;
                _latencySum = 0;
                return v;
            }
            private set
            {
                _latencyCounter++;
                _latencySum += value;
            }
        }

        public float LatencyMs => (float)Latency*1000 / WaveFormat.Channels / WaveFormat.SampleRate;

        public BlockingSampleProvider(AudioFormat format, BufferedSampleProvider provider)
        {
            Format = format;
            _sampleProvider = provider;
            _logger.Info($"Created: Format: {Format}");
        }

        private bool _startUp = true;
        private bool _requested;
        private int _bufferSize = 4*512;
        private int _requestBuffer = 4*512;
        public int Read(float[] buffer, int offset, int count)
        {
            _logger.Trace($"READ {offset} {count}, buffer: {_sampleProvider.BufferedSamples}, w={_sampleProvider.WritePos}, r={_sampleProvider.ReadPos}");
            if (_startUp)
            {
                if (_requestBuffer > 0)
                {
                    AudioCore.Instance.Requests.Post(_requestBuffer+count);
                    _logger.Info($"Startup phase, requested additional {_requestBuffer} samples");
                    _requestBuffer = 0;
                }
                if (_sampleProvider.BufferedSamples<_bufferSize+count )
                {
                    return count;
                }

                _logger.Info($"End startup, Samples in buffer: {_sampleProvider.BufferedSamples}");
                _startUp = false;
            }
            if (!_requested)
            {
                AudioCore.Instance.Requests.Post(count);
                _requested = true;
            }

            var retries = 100;
            while (retries>0 && _sampleProvider.BufferedSamples < count)
            {
                Thread.Sleep(TimeSpan.FromTicks(10000));
                retries--;
            }
            if (_sampleProvider.BufferedSamples<count )
            {
                _underruns++;
                _startUp = true;
                // _bufferSize += count;
                // _requestBuffer = _bufferSize;
                _logger.Warn($"Underrun, requested {count}, actual {_sampleProvider.BufferedSamples}");
                Array.Clear(buffer, offset, count);
                return count;
            }
            Latency = _sampleProvider.BufferedSamples - count;
            _requested = false;
            return _sampleProvider.Read(buffer, offset, count);
        }

        public void CopyBuffer(AudioBuffer source, int inChannels)
        {
            /*
            lock (_temp)
            {
                int channels = WaveFormat.Channels;

                for (int i = 0; i < source.Count / channels; i += channels)
                {
                    for (int ch = 0; ch < inChannels; ch++)
                    {
                        _temp[i * channels + ch % channels] +=
                            source.Data[i * inChannels + ch % inChannels];
                    }
                }

                _waitHandle.Set();
            }
        */
        }

        public void Dispose()
        {
        }
    }
}