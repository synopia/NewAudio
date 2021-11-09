using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NewAudio.Core;
using NewAudio.Devices;
using SharedMemory;

namespace NewAudioTest
{
    public static class TestDeviceSetup
    {
        public  static TestDevice InputDevice;
        public  static TestDevice OutputDevice;
        public  static WaveInputDevice InputEnum;
        public  static WaveOutputDevice OutputEnum;
        public  static WaveOutputDevice OutputNullEnum;
        public static WaveInputDevice InputNullEnum;
        
        static TestDeviceSetup()
        {
            DriverManager.Instance.AddDriver(new TestDriver());
            InputEnum = new WaveInputDevice("TEST INPUT");
            OutputEnum = new WaveOutputDevice("TEST OUTPUT");
            InputNullEnum = new WaveInputDevice("Null: Input");
            OutputNullEnum = new WaveOutputDevice("Null: Output");
            InputDevice = ((TestDevice)InputEnum.Tag);
            OutputDevice = ((TestDevice)OutputEnum.Tag);
            
        }

        public static void Init()
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
    public class TestDevice : IDevice
    {
        public List<string> MethodCalls = new List<string>();

        public CircularBuffer PlayBuffer;
        public CircularBuffer RecordBuffer;

        public void Dispose()
        {
            MethodCalls.Add("Dispose");
        }

        public string Name { get; }
        public bool IsInputDevice => true;
        public bool IsOutputDevice => true;
        private bool _isPlaying;
        private bool _isRecording;

        public AudioDataProvider AudioDataProvider { get; set; }

        public TestDevice(string name)
        {
            Name = name;
        }

        public LifecyclePhase Phase { get; set; }

        public bool IsInputValid(DeviceConfigRequest config)
        {
            return true;
        }

        public bool Free()
        {
            MethodCalls.Add("Free");
            return true;
        }

        public bool Start()
        {
            MethodCalls.Add(_isPlaying ? "Play" : "Record");
            return true;
        }

        public bool Stop()
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
            _isPlaying = config.IsPlaying;
            _isRecording = config.IsRecording;
            MethodCalls.Add(_isPlaying ? $"InitPlayback" : $"InitRecording");
            PlayBuffer = config.Playing?.Buffer;
            RecordBuffer = config.Recording?.Buffer;
            return Task.FromResult(new DeviceConfigResponse());
        }
    }
}