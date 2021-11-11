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
    public class BaseDeviceTest : BaseTest
    {
        private IResourceHandle<DriverManager> _driverManager; 
        public TestDevice InputDevice;
        public TestDevice OutputDevice;
        public WaveInputDevice InputEnum;
        public WaveOutputDevice OutputEnum;
        public WaveOutputDevice OutputNullEnum;
        public WaveInputDevice InputNullEnum;
        
        protected BaseDeviceTest()
        {
            _driverManager = VLApi.Instance.GetDriverManager();
            _driverManager.Resource.AddDriver(new TestDriver());
            
            InputEnum = new WaveInputDevice("TEST INPUT");
            OutputEnum = new WaveOutputDevice("TEST OUTPUT");
            InputNullEnum = new WaveInputDevice("Null: Input");
            OutputNullEnum = new WaveOutputDevice("Null: Output");
            InputDevice = ((TestDevice)InputEnum.Tag);
            OutputDevice = ((TestDevice)OutputEnum.Tag);
        }

        [SetUp]
        public void Init()
        {
            InputDevice.MethodCalls.Clear();
            OutputDevice.MethodCalls.Clear();
        }
    }
    public class TestDriver : IDriver
    {
        public string Name => "TEST";

        public IEnumerable<IDevice> GetDevices()
        {
            return new[] { new TestDevice("TEST INPUT"), new TestDevice("TEST OUTPUT"), };
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