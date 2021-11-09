using System.Threading.Tasks.Dataflow;

namespace NewAudio.Core
{
    public class AudioDataflowOptions: AudioParams
    {
        public AudioParam<int> BufferCount;
        public AudioParam<int> MaxBuffersPerTask;
        public AudioParam<bool> EnsureOrdered;
        public AudioParam<bool> SingleProducerConstrained;
        public AudioParam<int> MaxDegreeOfParallelism;

        private DataflowBlockOptions _options;
        
        public bool UpdateOptions(int bufferCount, int maxBuffersPerTask, int maxDegreeOfParallelism,
            bool singleProducerConstrained, bool ensureOrdered)
        {
            BufferCount.Value = bufferCount;
            MaxBuffersPerTask.Value = maxBuffersPerTask;
            EnsureOrdered.Value = ensureOrdered;
            SingleProducerConstrained.Value = singleProducerConstrained;
            MaxDegreeOfParallelism.Value = maxDegreeOfParallelism;

            return HasChanged;
        }

        public DataflowBlockOptions GetAudioDataflowOptions()
        {
            if (_options == null || HasChanged)
            {
                _options = new DataflowBlockOptions()
                {
                    BoundedCapacity = BufferCount.Value,
                    MaxMessagesPerTask = MaxBuffersPerTask.Value,
                    EnsureOrdered = EnsureOrdered.Value
                };
            }

            return _options;
        }
        public ExecutionDataflowBlockOptions GetAudioExecutionOptions()
        {
            if (_options == null || HasChanged)
            {
                _options = new ExecutionDataflowBlockOptions()
                {
                    BoundedCapacity = BufferCount.Value,
                    MaxMessagesPerTask = MaxBuffersPerTask.Value,
                    EnsureOrdered = EnsureOrdered.Value,
                    SingleProducerConstrained = SingleProducerConstrained.Value,
                    MaxDegreeOfParallelism = MaxDegreeOfParallelism.Value
                };
            }

            return (ExecutionDataflowBlockOptions)_options;
        }
    }
}