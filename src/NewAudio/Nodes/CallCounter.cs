using System.Data;
using Serilog;
using Serilog.Core;

namespace NewAudio.Nodes
{
    public class CallCounter<T>
    {
        private readonly ILogger _logger = Log.ForContext<CallCounter<T>>();
        private int _counts = 0;
        public int Counts => _counts;
        private static int nextId = 1;
        private int id;
            
        public CallCounter()
        {
            id = nextId++;
        }

        public T Update(T value)
        {
            _logger.Information("CC {id} {count} Update Called, value={value} ", id, _counts, value);
            _counts++;
            return value;
        }
        
        
    }
}