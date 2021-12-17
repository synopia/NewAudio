using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VL.NewAudio.Processor;

namespace VL.NewAudio.Dsp
{
    public readonly struct AudioBusState : IEquatable<AudioBusState>
    {
        public readonly AudioChannels[] Inputs;
        public readonly AudioChannels[] Outputs;
        public readonly int TotalNumberOfInputChannels;
        public readonly int TotalNumberOfOutputChannels;
        public int MainBusInputChannels => MainInput.Count;
        public int MainBusOutputChannels => MainOutput.Count;

        public AudioChannels MainInput => Inputs[0];
        public AudioChannels MainOutput => Outputs[0];

        public AudioBusState(AudioChannels[] inputs, AudioChannels[] outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
            var total = 0;
            foreach (var input in inputs)
            {
                total += input.Count;
            }

            TotalNumberOfInputChannels = total;
            total = 0;
            foreach (var output in outputs)
            {
                total += output.Count;
            }

            TotalNumberOfOutputChannels = total;
        }

        public AudioChannels GetChannels(bool input, int index)
        {
            return input ? Inputs[index] : Outputs[index];
        }

        public bool Equals(AudioBusState other)
        {
            return Equals(Inputs, other.Inputs) && Equals(Outputs, other.Outputs);
        }

        public override bool Equals(object obj)
        {
            return obj is AudioBusState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Inputs != null ? Inputs.GetHashCode() : 0) * 397) ^
                       (Outputs != null ? Outputs.GetHashCode() : 0);
            }
        }

        public static bool operator ==(AudioBusState left, AudioBusState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AudioBusState left, AudioBusState right)
        {
            return !left.Equals(right);
        }
    }

    public interface IHasAudioBus
    {
        AudioBusState Bus { get; }

        bool IsBusStateSupported(AudioBusState layout);
        void NumberOfBusesChanged(bool input);
        void ChannelsOfBusChanged(bool input, int busIndex);
        void BusLayoutChanged();
    }

    public class AudioBuses
    {
        public AudioBusState CurrentState { get; private set; }

        private List<AudioBus> Inputs = new();
        private List<AudioBus> Outputs = new();
        public AudioBus MainInput => Inputs[0];
        public AudioBus MainOutput => Outputs[0];

        public int GetBusCount(bool input)
        {
            return input ? Inputs.Count : Outputs.Count;
        }

        public AudioBus GetBus(bool input, int index)
        {
            Trace.Assert(index >= 0 && index < GetBusCount(input));
            return input ? Inputs[index] : Outputs[index];
        }

        public AudioChannels GetChannels(bool input, int index)
        {
            return GetBusState(input)[index];
        }

        public AudioChannels[] GetBusState(bool input)
        {
            return input ? CurrentState.Inputs : CurrentState.Outputs;
        }

        public AudioBusState Build()
        {
            return new AudioBusState(
                Inputs.Select(i => i.CurrentLayout).ToArray(),
                Outputs.Select(o => o.CurrentLayout).ToArray());
        }

        public AudioBuses WithInput(string name, AudioChannels channels, bool enabledByDefault = true)
        {
            Add(name, true, channels, enabledByDefault);
            return this;
        }

        public AudioBuses WithOutput(string name, AudioChannels channels, bool enabledByDefault = true)
        {
            Add(name, false, channels, enabledByDefault);
            return this;
        }

        public void Add(string name, bool input, AudioChannels channels, bool enabledByDefault)
        {
            var bus = new AudioBus(name, input, channels, enabledByDefault);
            (input ? Inputs : Outputs).Add(bus);
        }

        public void Apply(IHasAudioBus owner)
        {
            var state = Build();
            if (CurrentState == state)
            {
                return;
            }

            if (owner.IsBusStateSupported(state))
            {
                var old = CurrentState;
                CurrentState = state;
                owner.BusLayoutChanged();
                foreach (var input in new bool[] { false, true })
                {
                    var newChannels = GetBusState(input);
                    var oldChannels = input ? old.Inputs : old.Outputs;
                    if ((oldChannels?.Length ?? 0) != newChannels.Length)
                    {
                        owner.NumberOfBusesChanged(input);
                    }

                    for (var i = 0; i < Math.Min(oldChannels?.Length ?? 0, newChannels.Length); i++)
                    {
                        if (oldChannels?[i].Count != newChannels[i].Count)
                        {
                            owner.ChannelsOfBusChanged(input, i);
                        }
                    }
                }
            }
        }

        public void SetAllEnabled(bool input, bool enabled)
        {
            foreach (var bus in input ? Inputs : Outputs)
            {
                bus.SetEnable(enabled);
            }
        }

        public void SetMainEnabled(bool enabled)
        {
            MainInput.SetEnable(enabled);
            MainOutput.SetEnable(enabled);
        }

        public void SetNonMainEnabled(bool input, bool enabled)
        {
            foreach (var bus in input ? Inputs : Outputs)
            {
                if (!bus.IsMain)
                {
                    bus.SetEnable(enabled);
                }
            }
        }

        public int GetAbsolutChannelOffset(bool input, int busIndex, int channelIndex)
        {
            var bus = GetBusState(input);
            for (var i = 0; i < busIndex && i < bus.Length; i++)
            {
                channelIndex += bus[i].Count;
            }

            return channelIndex;
        }

        public int GetRelativeChannelOffset(bool input, int absoluteChannelIndex, out int busIndex)
        {
            var bus = GetBusState(input);
            var numChannels = 0;
            for (busIndex = 0;
                 busIndex < bus.Length && absoluteChannelIndex >= (numChannels = bus[busIndex].Count);
                 busIndex++)
            {
                absoluteChannelIndex -= numChannels;
            }

            return busIndex >= numChannels ? -1 : absoluteChannelIndex;
        }


        public AudioBuffer GetBusBuffer(AudioBuffer processBlockBuffer, bool input, int busIndex)
        {
            var bus = GetBusState(input);

            var channels = bus[busIndex].Count;
            var offset = GetAbsolutChannelOffset(input, busIndex, 0);

            var newMemory = new AudioChannel[channels];
            Array.Copy(processBlockBuffer.GetWriteChannels(), offset, newMemory, 0, channels);
            return new AudioBuffer(newMemory, channels, processBlockBuffer.NumberOfFrames);
        }
    }

    public class AudioBus
    {
        public string Name { get; }
        private AudioChannels _layout;
        private AudioChannels _defaultLayout;
        private AudioChannels _lastLayout;
        private bool _enabledByDefault;
        private int _countCache;

        public bool IsInput { get; }
        public bool IsOutput => !IsInput;
        public int Index { get; }
        public bool IsMain => Index == 0;

        public AudioChannels DefaultLayout => _defaultLayout;
        public AudioChannels CurrentLayout => _layout;
        public AudioChannels LastEnabledLayout => _lastLayout;
        public bool IsEnabledByDefault;

        public bool IsEnabled => !_layout.IsDisabled;

        public int NumberOfChannels => _countCache;

        public AudioBus(string name, bool isInput, AudioChannels channels, bool enabledByDefault)
        {
            Name = name;
            IsInput = isInput;
            _defaultLayout = channels;
            IsEnabledByDefault = enabledByDefault;
            _lastLayout = channels;
        }

        public void Enable()
        {
            SetCurrentLayout(_lastLayout);
        }

        public void Disable()
        {
            SetCurrentLayout(AudioChannels.Disabled);
        }

        public void SetEnable(bool b)
        {
            if (b)
            {
                Enable();
            }
            else
            {
                Disable();
            }
        }

        public void SetCurrentLayout(AudioChannels layout)
        {
            _layout = layout;
        }

        public void SetCurrentLayoutWithoutEnabling(AudioChannels layout)
        {
            if (IsEnabled)
            {
                _layout = layout;
            }
            else
            {
                _lastLayout = layout;
            }
        }

        public void SetNumberOfChannels(int no)
        {
            SetCurrentLayout(AudioChannels.Channels(no));
        }
    }
}