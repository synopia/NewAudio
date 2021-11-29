using System;
using System.Diagnostics;
using NewAudio.Processor;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;
using NewAudio;

namespace NewAudio.Nodes
{
    public class AudioEngineParams : AudioParams
    {
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<float> BufferSize;
        public AudioParam<ulong> CurrentFrame;
        public AudioParam<bool> Enabled;
    }

    public class AudioEngine : AudioNode
    {
        public readonly AudioEngineParams Params;
        public override string NodeName => "AudioEngine";
        private int _framesPerBlock = 0;
        public readonly string SessionId = Guid.NewGuid().ToString();

        public AudioEngine()
        {
            var audioService = Resources.GetAudioService();

            InitLogger<AudioEngine>();
            Params = AudioParams.Create<AudioEngineParams>();

            Logger.Information("AUDIO SERVICE  {AS}", audioService.GetHashCode());
        }

        public bool Update(bool enable, out int framesPerBlock,
            SamplingFrequency samplingFrequency = SamplingFrequency.Hz48000, float bufferSize = 50)
        {
            try
            {
                var currentFrame = VLSession.Instance.UserRuntime.Frame;
                Params.Enabled.Value = enable;
                Params.CurrentFrame.Value = currentFrame;
                Params.SamplingFrequency.Value = samplingFrequency;
                Params.BufferSize.Value = bufferSize;

                if (Params.CurrentFrame.HasChanged)
                {
                    Params.CurrentFrame.Commit();
                    _framesPerBlock = AudioService.UpdateFormat(SessionId,
                        new FormatConfig
                        {
                            SampleRate = (int)Params.SamplingFrequency.Value, BufferSizeMs = Params.BufferSize.Value
                        });
                }

                if (Params.Enabled.HasChanged)
                {
                    Params.Enabled.Commit();
                    Graph.SetEnabled(Params.Enabled.Value);
                }

                framesPerBlock = _framesPerBlock;
                return Graph.IsEnabled;
            }
            catch (Exception e)
            {
                ExceptionHappened(e, "AudioEngine.Update");
                framesPerBlock = 0;
                return false;
            }
        }

        public override string DebugInfo()
        {
            return Graph.DebugInfo();
        }

        private bool _disposedValue;
        private Random _random;

        protected override void Dispose(bool disposing)
        {
            Trace.WriteLine($"Dispose called for AudioEngine {NodeName} ({disposing})");

            if (!_disposedValue)
            {
                if (disposing)
                {
                    Graph.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}