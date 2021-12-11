using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VL.NewAudio.Dsp;
using Serilog;
using VL.Lib.Basics.Resources;
using VL.NewAudio;

namespace VL.NewAudio.Processor
{
 
    public abstract class AudioProcessor : IDisposable, IHasAudioBus
    {
        protected ILogger Logger = Resources.GetLogger<AudioProcessor>();

        public abstract string Name { get; }
        public AudioBusState Bus => BusConfig.CurrentState;
        protected readonly AudioBuses BusConfig;
        public int TotalNumberOfInputChannels => BusConfig.CurrentState.TotalNumberOfInputChannels;
        public int TotalNumberOfOutputChannels => BusConfig.CurrentState.TotalNumberOfOutputChannels;
        public int MainBusInputChannels => BusConfig.CurrentState.MainBusInputChannels;
        public int MainBusOutputChannels => BusConfig.CurrentState.MainBusOutputChannels;
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
        
        protected AudioProcessor(AudioBuses busesConfig)
        {
            BusConfig = busesConfig;
            BusConfig.Apply(this);
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


        public virtual bool IsBusStateSupported(AudioBusState layout)
        {
            return true;// layout.MainBusInputChannels == 2 && layout.MainBusOutputChannels == 2;
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

        public void SetChannels(int numIns, int numOuts)
        {
            if (TotalNumberOfInputChannels != numIns)
            {
                BusConfig.MainInput.SetNumberOfChannels(numIns);
            }

            if (TotalNumberOfOutputChannels != numOuts)
            {
                BusConfig.MainOutput.SetNumberOfChannels(numOuts);
            }
            BusConfig.Apply(this);
        }
        
        public void SetPlayConfig(int numIns, int numOuts, int sampleRate, int framesPerBlock)
        {
            SetChannels(numIns, numOuts);
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