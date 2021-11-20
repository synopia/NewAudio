using System;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Devices;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Nodes
{
    public class AudioEngineParams : AudioParams
    {
        public AudioParam<SamplingFrequency> SamplingFrequency;
        public AudioParam<int> FramesPerBlock;
        public AudioParam<ulong> CurrentFrame;
        public AudioParam<bool> Enabled;
    }
    public class AudioEngine : AudioNode
    {
        private readonly IResourceHandle<AudioGraph> _graph;
        private readonly IResourceHandle<DeviceManager> _driverManager;

        public readonly AudioEngineParams Params;
        public override string NodeName => "AudioEngine";

        public AudioEngine()
        {
            _driverManager = Factory.GetDriverManager();
            InitLogger<AudioEngine>();
            Params = AudioParams.Create<AudioEngineParams>();
        }

        public bool Update(bool enable, SamplingFrequency samplingFrequency=SamplingFrequency.Hz48000, int framesPerBlock = 512)
        {
            try
            {
                var currentFrame = VLSession.Instance.UserRuntime.Frame;
                Params.Enabled.Value = enable;
                Params.CurrentFrame.Value = currentFrame;
                Params.SamplingFrequency.Value = samplingFrequency;
                Params.FramesPerBlock.Value = framesPerBlock;
            
                if (Params.CurrentFrame.HasChanged)
                {
                    Params.CurrentFrame.Commit();
                    _driverManager.Resource.Update();
                }

                if (Params.HasChanged)
                {
                    Params.Commit();
                    // ((AsioOutputDevice)Graph.OutputBlock).Device.UpdateFormat((int)Params.SamplingFrequency.Value, Params.FramesPerBlock.Value);
                    
                    Graph.SetEnabled(Params.Enabled.Value);
                }
                
                return Graph.IsEnabled;
            }
            catch (Exception e)
            {
                ExceptionHappened(e, "AudioEngine.Update");
                return false;
            }
        }

        public override string DebugInfo()
        {
            return _graph.Resource.DebugInfo();
        }
        private bool _disposedValue;
        private Random _random;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _graph.Dispose();
                    _driverManager.Dispose();
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }
    }
}