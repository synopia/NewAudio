using System.Collections.Generic;
using System.Linq;
using NAudio.Wave.SampleProviders;
using NewAudio.Internal;
using VL.Lib.Collections;

namespace NewAudio
{
    /// <summary>Allows any number of inputs to be patched to outputs</summary>
    /// <remarks>Remark?</remarks>
    /// 
    public class Multiplexer: AudioNodeTransformer
    {
        private readonly Logger _logger = LogFactory.Instance.Create("Multiplexer");
        private List<AudioLink> _inputs;
        private List<int> _outputMap;
        private int _inputChannels;

        public Multiplexer()
        {
        }

        private AudioLink[] _lastInputs;
        private int[] _lastOutputMap;
        public void ChangeSettings(Spread<AudioLink> inputs, Spread<int> outputMap)
        {
            var inputEquals = Utils.ArrayEquals(inputs.ToArray(), _lastInputs);
            var outputMapEquals = Utils.ArrayEquals(outputMap.ToArray(), _lastOutputMap);
            if (inputEquals && outputMapEquals)
            {
                return;
            }

            _lastInputs = inputs.ToArray();
            _lastOutputMap = outputMap.ToArray();
            
            /*
            _inputs = inputs.Select(input =>
            {
                if (input != null)
                {
                    return input;
                }

                return AudioCore.Instance.CreateStream(new SilenceProvider());
            }).ToList();
            */
            // _inputChannels = _inputs.Sum(i => i.WaveFormat.Channels);
            if (outputMap.Count == 0)
            {
                InitOutputMap();
            }
            else
            {
                _outputMap = outputMap.ToList();
            }

            _logger.Info($"InputCount: {_inputChannels} OutputCount: {_outputMap.Count}");
            var multiplexer = new MultiplexingSampleProvider(_inputs, _outputMap.Count);
            for (int i = 0; i < _outputMap.Count; i++)
            {
                var input = _outputMap[i];
                multiplexer.ConnectInputToOutput(input, i);
                _logger.Info($" ch: {input} => {i}");
            }

            Output.Format = Output.Format.Update(multiplexer.WaveFormat);
            // Output.FillBuffer = (buffer, offset, count) => multiplexer.Read(buffer, offset, count);
        }

        private void InitOutputMap()
        {
            _outputMap = new List<int>();
            for (int i = 0; i < _inputChannels; i++)
            {
                _outputMap.Add(i);
            }

        }
        
        public override void Dispose()
        {
            
        }
    }
}