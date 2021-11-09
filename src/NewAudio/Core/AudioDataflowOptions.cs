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

        public DataflowBlockOptions DataflowBlockOptions =>  new()
        {
            BoundedCapacity = BufferCount.Value,
            MaxMessagesPerTask = MaxBuffersPerTask.Value,
            EnsureOrdered = EnsureOrdered.Value
        };
        public ExecutionDataflowBlockOptions ExecutionDataflowBlockOptions => new()
        {
            BoundedCapacity = BufferCount.Value,
            MaxMessagesPerTask = MaxBuffersPerTask.Value,
            EnsureOrdered = EnsureOrdered.Value,
            SingleProducerConstrained = SingleProducerConstrained.Value,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism.Value
        };
        
        public bool UpdateAudioDataflowOptions(int bufferCount, int maxBuffersPerTask, bool ensureOrdered)
        {
            BufferCount.Value = bufferCount;
            MaxBuffersPerTask.Value = maxBuffersPerTask;
            EnsureOrdered.Value = ensureOrdered;

            return HasChanged;
        }
        public bool UpdateAudioExecutionOptions(int bufferCount, int maxBuffersPerTask, int maxDegreeOfParallelism,
            bool singleProducerConstrained, bool ensureOrdered)
        {
            BufferCount.Value = bufferCount;
            MaxBuffersPerTask.Value = maxBuffersPerTask;
            EnsureOrdered.Value = ensureOrdered;
            SingleProducerConstrained.Value = singleProducerConstrained;
            MaxDegreeOfParallelism.Value = maxDegreeOfParallelism;
            return HasChanged;
        }
    }
}