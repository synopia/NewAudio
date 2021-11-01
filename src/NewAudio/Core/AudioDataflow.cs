using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Blocks;
using Serilog;
using SharedMemory;

namespace NewAudio.Core
{
    public class AudioDataflow : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IList<IDataflowBlock> _blocks = new List<IDataflowBlock>();
        public IEnumerable<IDataflowBlock> Blocks => _blocks;
        private readonly IList<AudioInputBlock> _inputBlocks = new List<AudioInputBlock>();
        private readonly IList<AudioOutputBlock> _outputBlocks = new List<AudioOutputBlock>();
        
        public AudioDataflow(ILogger logger)
        {
            _logger = logger;
        }


        public void PostRequest(AudioDataRequestMessage message)
        {
            if (_inputBlocks.Count == 0)
            {
                return;
            }

            _inputBlocks[0].Post(message);

            for (int i = 1; i < _inputBlocks.Count;i++)
            {
                var next = new AudioDataRequestMessage(message.RequestedSamples);
                _inputBlocks[i].Post(next);
            }
        }
        public void PostLifecycleMessage(LifecyclePhase old, LifecyclePhase newPhase)
        {
            _logger.Information("Lifecycle Message started {old} => {new}", old, newPhase);
            foreach (var block in _inputBlocks)
            {
                block.Post(new LifecycleMessage(old, newPhase));
            }
        }

        public void Add(IDataflowBlock block)
        {
            if (block is AudioInputBlock inputBlock)
            {
                _inputBlocks.Add(inputBlock);
            }

            if (block is AudioOutputBlock outputBlock)
            {
                _outputBlocks.Add(outputBlock);
            }
            _blocks.Add(block);
        }

        public void Remove(IDataflowBlock block)
        {
            Log.Logger.Verbose("Removing block {block}, Waiting for completion", block);
            if (block is AudioInputBlock inputBlock)
            {
                _inputBlocks.Remove(inputBlock);
            }
            if (block is AudioOutputBlock outputBlock)
            {
                _outputBlocks.Remove(outputBlock);
            }

            _blocks.Remove(block);
            
            block.Complete();
            block.Completion.ContinueWith(task =>
            {
                Log.Logger.Verbose("Completed {block}", block);
            });
        }

        public string DebugInfo()
        {
            return $"Blocks: {_blocks.Count}, Input Blocks: {_inputBlocks.Count}, Output Blocks: {_outputBlocks.Count}";
        }
        
        public void Dispose()
        {
            _blocks.Clear();
            _inputBlocks.Clear();
            _outputBlocks.Clear();
        }
    }
}