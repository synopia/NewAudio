using System.Collections.Generic;

namespace NewAudio.Devices
{
    public class NullDriver : IDriver
    {
        public string Name => "Null";

        public IEnumerable<IDevice> GetDevices()
        {
            return new List<IDevice>
            {
                new NullDevice("Null: Input", true, false),
                new NullDevice("Null: Output", false, true)
            };
        }
    }
}