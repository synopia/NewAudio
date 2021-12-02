using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NewAudio.Dsp;
using Serilog;
using VL.Lib.Basics.Resources;
using NewAudio;

namespace NewAudio.Processor
{
 
    public abstract class AudioProcessor : IDisposable, IHasAudioBus
    {
        protected ILogger Logger { get; private set; }

        public abstract string Name { get; }
        public AudioBusState Bus => _bus.CurrentState;
        private AudioBuses _bus;
        public int TotalNumberOfInputChannels => _bus.CurrentState.TotalNumberOfInputChannels;
        public int TotalNumberOfOutputChannels => _bus.CurrentState.TotalNumberOfOutputChannels;
        public int MainBusInputChannels => _bus.CurrentState.MainBusInputChannels;
        public int MainBusOutputChannels => _bus.CurrentState.MainBusOutputChannels;
        public int SampleRate { get; private set; }
        public int FramesPerBlock { get; private set; }
        public int LatencySamples { get; set; }

        public object ProcessLock => new();
        public object ListenerLock => new();
        public bool SuspendProcessing { get; set; }
        public AudioPlayHead PlayHead { get; set; }


        protected AudioProcessor(): this(new AudioBuses()
            .WithInput("Input", AudioChannels.Stereo)
            .WithOutput("Output", AudioChannels.Stereo)){}
        protected AudioProcessor(AudioBuses buses)
        {
            _bus = buses;
            _bus.SetMainEnabled(true);
            _bus.Apply(this);
        }

        public abstract void PrepareToPlay(int sampleRate, int framesPerBlock);
        public abstract void ReleaseResources();
        public virtual void Process(AudioBuffer buffer)
        {
            for (int ch = MainBusInputChannels; ch < TotalNumberOfOutputChannels; ch++)
            {
                buffer.ZeroChannel(ch);
            }
        }

        public virtual void ProcessBypassed(AudioBuffer buffer)
        {
            for (int ch = MainBusInputChannels; ch < TotalNumberOfOutputChannels; ch++)
            {
                buffer.ZeroChannel(ch);
            }
        }


        public bool IsBusStateSupported(AudioBusState layout)
        {
            return layout.MainBusInputChannels == 2 && layout.MainBusOutputChannels == 2;
        }


        public void NumberOfBusesChanged(bool input)
        {
            
        }

        public void ChannelsOfBusChanged(bool input, int busIndex)
        {
            
        }

        public void BusLayoutChanged()
        {
            
        }

        public virtual void Reset(){}

        public void SetPlayConfig(int numIns, int numOuts, int sampleRate, int framesPerBlock)
        {
            SetRateAndFrameSize(sampleRate, framesPerBlock);
        }
        public void SetRateAndFrameSize(int sampleRate, int framesPerBlock)
        {
            SampleRate = sampleRate;
            FramesPerBlock = framesPerBlock;
        }

        public override string ToString()
        {
            return Name;
        }

        private bool _disposedValue;

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Information("Dispose called for AudioBlock {This} ({Disposing})", Name, disposing);
            if (!_disposedValue)
            {
                if (disposing)
                {
                }

                _disposedValue = true;
            }
        }

    }
}