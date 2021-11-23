using System;
using System.Diagnostics;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;
using VL.NewAudio;

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
        public IResourceHandle<DeviceManager> DeviceManager { get; }
        public readonly AudioEngineParams Params;
        public override string NodeName => "AudioEngine";
        private int _framesPerBlock = 0;

        public AudioEngine()
        {
            var audioService = Resources.GetAudioService();

            DeviceManager = Resources.GetDeviceManager();
            InitLogger<AudioEngine>();
            Params = AudioParams.Create<AudioEngineParams>();
            
            Logger.Information("AUDIO SERVICE  {AS}", audioService.GetHashCode());
        }

        public bool Update(bool enable, out int framesPerBlock, SamplingFrequency samplingFrequency=SamplingFrequency.Hz48000, float bufferSize=50)
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
                    _framesPerBlock = DeviceManager.Resource.UpdateFormat(Params.SamplingFrequency, Params.BufferSize);
                }

                if (Params.Enabled.HasChanged)
                {
                    Params.Enabled.Commit();
                    DeviceManager.Resource.SetEnabled(Params.Enabled.Value);
                    
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
                    DeviceManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}