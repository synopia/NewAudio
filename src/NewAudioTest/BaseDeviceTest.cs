using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Internal;
using NewAudio.Nodes;
using NUnit.Framework;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudioTest
{
    using NewAudioTest;

    public static class Ext
    {
        public static List<string> MethodCalls(this IVirtualDevice vd)
        {
            if (vd.Name == "INPUT")
            {
                return ((TestDevice)vd.Device).Driver.InputMethodCalls;
            }
            else
            {
                return ((TestDevice)vd.Device).Driver.OutputMethodCalls;
            }
        }

        public static void OnDataReceived(this IVirtualDevice vd, byte[] data)
        {
            ((TestDevice)vd.Device).OnDataReceived(data);
        }

        public static IMixBuffer GetReadBuffer(this IVirtualDevice vd)
        {
            return ((TestDevice)vd.Device).GetReadBuffer();
        }

    }

    public class BaseDeviceTest : BaseTest
    {
        public IResourceHandle<DriverManager> DriverManager;
        public TestDriver InputDriver;
        public TestDriver OutputDriver;
        public TestDriver InOutDriver;
        
        public InputDeviceSelection InputDevice = new("VIN: INPUT");
        public OutputDeviceSelection OutputDevice = new("VOUT: OUTPUT");
        public InputDeviceSelection InOutDeviceIn = new("VINOUT: INPUT");
        public OutputDeviceSelection InOutDeviceOut = new("VINOUT: OUTPUT");

        protected BaseDeviceTest()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            InputDriver = new TestDriver("VIN", true, false);
            OutputDriver = new TestDriver("VOUT",false, true);
            InOutDriver = new TestDriver("VINOUT",true, true);
            DriverManager.Resource.AddDriver(InputDriver);
            DriverManager.Resource.AddDriver(OutputDriver);
            DriverManager.Resource.AddDriver(InOutDriver);
        }

        public void UpdateDevices()
        {
            DriverManager.Resource.UpdateAllDevices();
        }

        [SetUp]
        public void Init()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            InputDriver.InputMethodCalls.Clear();
            InputDriver.OutputMethodCalls.Clear();
            OutputDriver.InputMethodCalls.Clear();
            OutputDriver.OutputMethodCalls.Clear();
            InOutDriver.InputMethodCalls.Clear();
            InOutDriver.OutputMethodCalls.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            Assert.IsEmpty(DriverManager.Resource.CheckPools());
        }

        protected float[] BuildSignal(AudioFormat format, int offset = 0, bool interleaved = true)
        {
            var signal = new float[format.BufferSize];
            if (interleaved)
            {
                var pos = 0;
                for (var s = 0; s < format.SampleCount; s++)
                {
                    for (var ch = 0; ch < format.Channels; ch++)
                    {
                        signal[pos] = offset + ch;
                        pos++;
                    }
                }
            }
            else
            {
                var pos = 0;
                for (var ch = 0; ch < format.Channels; ch++)
                {
                    for (var s = 0; s < format.SampleCount; s++)
                    {
                        signal[pos] = ch;
                        pos++;
                    }
                }
            }

            return signal;
        }

        public void AssertSignal(float[] expected, float[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "Length must match");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "Differs at {0}", i);
            }
        }
    }

    public class TestDriver : IDriver
    {
        public string Name { get; }
        public List<string> InputMethodCalls = new();
        public List<string> OutputMethodCalls = new();
        private bool _inputDriver;
        private bool _outputDriver;

        public TestDriver(string name, bool inputDriver, bool outputDriver)
        {
            Name = name;
            _inputDriver = inputDriver;
            _outputDriver = outputDriver;
        }

        public void AddCall(string who, string what)
        {
            if (who == "INPUT")
            {
                InputMethodCalls.Add(what);
            }

            if (who == "OUTPUT")
            {
                OutputMethodCalls.Add(what);
            }
        }

        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            var list = new List<DeviceSelection>();
            if (_inputDriver)
            {
                list.Add(new DeviceSelection(Name, Name,"INPUT", true, false));
            }

            if (_outputDriver)
            {
                list.Add(new DeviceSelection(Name, Name,"OUTPUT", false, true));
            }

            return list;
        }

        public IDevice CreateDevice(DeviceSelection selection)
        {
            return new TestDevice(this, selection.Name);
        }
    }

    public class TestDevice : BaseDevice
    {
        private TestDriver _driver;

        public TestDriver Driver => _driver;

        public TestDevice(TestDriver driver, string name)
        {
            _driver = driver;
            Name = name;
            InitLogger<TestDevice>();
            IsInputDevice = true;
            IsOutputDevice = true;
        }


        protected override bool Init()
        {
            _driver.AddCall(Name, "Init");
            return true;
        }

        protected override bool Stop()
        {
            return true;
        }

        protected override void Dispose(bool dispose)
        {
            _driver.AddCall(Name, "Dispose");
            base.Dispose(dispose);
        }
    }
}