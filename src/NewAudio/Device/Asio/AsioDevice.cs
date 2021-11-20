using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.Asio;
using NAudio.Wave.SampleProviders;
using NewAudio.Block;
using NewAudio.Core;
using NewAudio.Dsp;
using NewAudio.Nodes;

namespace NewAudio.Devices
{
    public class AsioDevice : BaseDevice
    {
        public override string Name { get; }
        private int _numberOfInputChannels;
        private int _numberOfOutputChannels;
        private int _inputChannelOffset;
        private int _outputChannelOffset;
        public override int NumberOfInputChannels => _numberOfInputChannels;
        public override int NumberOfOutputChannels => _numberOfOutputChannels;
        public override int InputChannelOffset => _inputChannelOffset;
        public override int OutputChannelOffset => _outputChannelOffset;

        public AsioDevice(AsioDriver driver, string name, AudioBlockFormat format): base(driver)
        {
            Name = name;
            InitLogger<AsioDevice>();
        }
        
        public override void Initialize()
        {
            Driver.Initialize();
            
            _numberOfOutputChannels = Math.Min(Output.NumberOfChannels, Driver.MaxNumberOfOutputChannels);
            _numberOfInputChannels = _numberOfOutputChannels;
            _inputChannelOffset = 0;
            
            if (Output.OutputChannelOffset >= 0 && Output.OutputChannelOffset + _numberOfOutputChannels <
                Driver.MaxNumberOfOutputChannels)
            {
                _outputChannelOffset = Output.OutputChannelOffset;
            }
             
        }

        public override void Uninitialize()
        {
            
        }

        public override void EnableProcessing()
        {
            Driver.EnableProcessing();
        }

        public override void DisableProcessing()
        {
        }

        

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = disposing;
            }

            base.Dispose(disposing);
        }

        // public string DebugInfo()
        // {
            // return $"[{this}, {_asioOut?.PlaybackState}, {base.DebugInfo()}]";
        // }
    }
}