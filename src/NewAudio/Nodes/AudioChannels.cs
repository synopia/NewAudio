using System;

namespace NewAudio.Nodes
{
    public class AudioChannels
    {
        private float[] _tempInputBuffer;

        private float[] _currentInputBuffer;
        private float[] _currentOutputBuffer;
        private int _currentOutputChannels;
        private int _currentInputChannels;
        private int _currentIndex;

        public void Update(float[] outputBuffer, float[] inputBuffer, int outputChannels = 1, int inputChannels = 1)
        {
            _currentInputBuffer = inputBuffer;
            _currentOutputBuffer = outputBuffer;
            _currentOutputChannels = outputChannels;

            if (inputChannels != _currentInputChannels)
            {
                _currentInputChannels = inputChannels;
                _tempInputBuffer = new float[inputChannels];
            }
        }

        public void UpdateLoop(int inputIndex, int outputIndex)
        {
            if (_currentInputBuffer != null)
            {
                Array.Copy(_currentInputBuffer, inputIndex * _currentInputChannels, _tempInputBuffer, 0,
                    _currentInputChannels);
            }

            _currentIndex = outputIndex;
        }

        public float GetSampleFromChannel(int channel)
        {
            return _tempInputBuffer[channel % _currentInputChannels];
        }

        public void SetSampleToChannel(int channel, float value)
        {
            _currentOutputBuffer[_currentIndex * _currentOutputChannels + channel % _currentOutputChannels] = value;
        }

        public float[] GetAllSamples()
        {
            return _tempInputBuffer;
        }

        public void SetAllSamples(float[] inp)
        {
            if (inp != null)
            {
                for (var i = 0; i < _currentOutputChannels; i++)
                {
                    _currentOutputBuffer[_currentIndex * _currentOutputChannels + i] = inp[i % inp.Length];
                }
            }
        }
    }
}