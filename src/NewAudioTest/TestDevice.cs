using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NewAudio.Core;
using NewAudio.Devices;
using NUnit.Framework;
using SharedMemory;
using VL.Lib.Basics.Resources;

namespace NewAudioTest
{
    using NewAudioTest;
    
    public static class Ext
    {
        public static List<String> MethodCalls(this VirtualDevice vd)
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
        public static CircularBuffer RecordingBuffer(this VirtualDevice vd)
        {
            return ((TestDevice)vd.Device).RecordingBuffer;
        }
    }
    
    public class BaseDeviceTest : BaseTest
    {
        public IResourceHandle<DriverManager> DriverManager; 
        public InputDeviceSelection InputEnum;
        public OutputDeviceSelection OutputEnum;
        public OutputDeviceSelection OutputNullEnum;
        public InputDeviceSelection InputNullEnum;
        private TestDriver _testDriver;

        protected BaseDeviceTest()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            _testDriver = new TestDriver();
            DriverManager.Resource.AddDriver(_testDriver);
            InputEnum = new InputDeviceSelection("TEST: INPUT");
            OutputEnum = new OutputDeviceSelection("TEST: OUTPUT");
            InputNullEnum = new InputDeviceSelection("Null: Input");
            OutputNullEnum = new OutputDeviceSelection("Null: Output");
        }

        [SetUp]
        public void Init()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            _testDriver.InputMethodCalls.Clear();
            _testDriver.OutputMethodCalls.Clear();
        }

        [TearDown]
        public void Teardown()
        {
            DriverManager = Factory.Instance.GetDriverManager();
            Assert.IsEmpty(DriverManager.Resource.CheckPools());
        }
    }
    public class TestDriver : IDriver
    {
        public string Name => "TEST";
        public List<string> InputMethodCalls = new();
        public List<string> OutputMethodCalls = new();

        public void AddCall(string who, string what)
        {
            if( who=="INPUT" ) InputMethodCalls.Add(what);
            if(who=="OUTPUT") OutputMethodCalls.Add(what);
        }
        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            return new[] { new DeviceSelection(Name, "INPUT",true, false ), new DeviceSelection(Name, "OUTPUT", false, true), };
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
        

        protected override Task<bool> Init()
        {
            _driver.AddCall(Name, "Init");
            return  Task.FromResult<bool>(true);
        }

        public override bool Start()
        {
            _driver.AddCall(Name, "Start");
            return true;
        }

        public override bool Stop()
        {
            _driver.AddCall(Name, "Stop");
            return true;
        }

        protected override void Dispose(bool dispose)
        {
            _driver.AddCall(Name, "Dispose");
            base.Dispose(dispose);
        }
    }
}