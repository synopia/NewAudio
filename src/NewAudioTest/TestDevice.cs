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
            return ((TestDevice)vd.Device).MethodCalls;
        }
        public static CircularBuffer RecordingBuffer(this VirtualDevice vd)
        {
            return ((TestDevice)vd.Device).RecordingBuffer;
        }
    }
    
    public class BaseDeviceTest : BaseTest
    {
        private IResourceHandle<DriverManager> _driverManager; 
        public VirtualDevice InputDevice;
        public VirtualDevice OutputDevice;
        public InputDeviceSelection InputEnum;
        public OutputDeviceSelection OutputEnum;
        public OutputDeviceSelection OutputNullEnum;
        public InputDeviceSelection InputNullEnum;
        
        protected BaseDeviceTest()
        {
            _driverManager = Factory.Instance.GetDriverManager();
            _driverManager.Resource.AddDriver(new TestDriver());
            
            InputEnum = new InputDeviceSelection("TEST INPUT");
            OutputEnum = new OutputDeviceSelection("TEST OUTPUT");
            InputNullEnum = new InputDeviceSelection("Null: Input");
            OutputNullEnum = new OutputDeviceSelection("Null: Output");
            InputDevice = _driverManager.Resource.GetInputDevice(InputEnum);
            OutputDevice = _driverManager.Resource.GetOutputDevice(OutputEnum);
        }

        [SetUp]
        public void Init()
        {
            InputDevice.MethodCalls().Clear();
            OutputDevice.MethodCalls().Clear();
        }

        
    }
    public class TestDriver : IDriver
    {
        public string Name => "TEST";

        public IEnumerable<DeviceSelection> GetDeviceSelections()
        {
            return new[] { new DeviceSelection(Name, "TEST INPUT",true, false ), new DeviceSelection(Name, "TEST OUTPUT", false, true), };
        }

        public IDevice CreateDevice(DeviceSelection selection)
        {
            return new TestDevice(selection.Name);
        }
    }
    
    public class TestDevice : BaseDevice
    {
        public List<string> MethodCalls = new();

        protected override void Dispose(bool dispose)
        {
            MethodCalls.Add("Dispose");
        }

        public TestDevice(string name)
        {
            Name = name;
            IsInputDevice = true;
            IsOutputDevice = true;
        }

        protected override Task<bool> Init()
        {
            if (IsRecording)
            {
                MethodCalls.Add($"InitRecording");
            }

            if (IsPlaying)
            {
                MethodCalls.Add($"InitPlayback");
            }
            return  Task.FromResult<bool>(true);
        }

        public override bool Start()
        {
            MethodCalls.Add(IsPlaying ? "Play" : "Record");
            return true;
        }

        public override bool Stop()
        {
            MethodCalls.Add("Stop");
            return true;
        }

        public void ExceptionHappened(Exception e, string method)
        {
            throw new NotImplementedException();
        }

        public Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
        {
            
            return Task.FromResult(new DeviceConfigResponse());
        }
    }
}