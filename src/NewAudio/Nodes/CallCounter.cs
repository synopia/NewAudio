using Serilog;

namespace NewAudio.Nodes
{
    public class CallCounter<T>
    {
        private static int nextId = 1;
        private readonly ILogger _logger = Log.ForContext<CallCounter<T>>();
        private readonly int id;

        public CallCounter()
        {
            id = nextId++;
        }

        public int Counts { get; private set; }

        public T Update(T value)
        {
            _logger.Information("CC {id} {count} Update Called, value={value} ", id, Counts, value);
            Counts++;
            return value;
        }
    }
}