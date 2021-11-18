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
    public class AudioEngine : IDisposable
    {
        private readonly IResourceHandle<AudioService> _audioService;
        private readonly IResourceHandle<AudioGraph> _graph;
        private readonly IResourceHandle<DeviceManager> _driverManager;
        private readonly ILogger _logger;

        public readonly AudioEngineParams Params;

        public AudioEngine()
        {
            _graph = Factory.GetAudioGraph();
            _audioService = Factory.GetAudioService();
            _driverManager = Factory.GetDriverManager();
            _logger = _audioService.Resource.GetLogger<AudioGraph>();
            Params = AudioParams.Create<AudioEngineParams>();
        }

        public bool Update(bool enabled, SamplingFrequency samplingFrequency=SamplingFrequency.Hz48000, int framesPerBlock = 512)
        {
            var currentFrame = VLSession.Instance.UserRuntime.Frame;
            Params.Enabled.Value = enabled;
            Params.CurrentFrame.Value = currentFrame;
            Params.SamplingFrequency.Value = samplingFrequency;
            Params.FramesPerBlock.Value = framesPerBlock;
            
            if (Params.CurrentFrame.HasChanged)
            {
                try
                {
                    _driverManager.Resource.UpdateAllDevices();
                }
                catch (Exception e)
                {
                    _logger.Error(e, "Error in DriverManager");
                }
            }

            return enabled;
        }



        public string DebugInfo()
        {
            return _graph.Resource.DebugInfo();
        }

        public void Dispose()
        {
            _graph.Dispose();
            _driverManager.Dispose();
        }
    }
}