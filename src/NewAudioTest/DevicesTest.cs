using System.Collections.Generic;
using System.Threading;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;
using Serilog;
using SharedMemory;
using NewAudio.Core;

namespace NewAudioTest
{
    
    [TestFixture]
    public class DevicesTest
    {
        private TestDevice _inputDevice;
        private TestDevice _outputDevice;
        private WaveInputDevice _inputEnum;
        private WaveOutputDevice _outputEnum;
        private WaveOutputDevice _outputNullEnum;
        private WaveInputDevice _inputNullEnum;

        public class TestDevice : IDevice
        {
            public List<string> MethodCalls = new List<string>();
            public void Dispose()
            {
                MethodCalls.Add("Dispose");
            }

            public string Name { get; }
            public bool IsInputDevice => true;
            public bool IsOutputDevice => true;
            
            public AudioDataProvider AudioDataProvider { get; set; }

            public TestDevice(string name)
            {
                Name = name;
            }

            public void InitPlayback(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
            {
                MethodCalls.Add("InitPlayback");
            }

            public void InitRecording(int desiredLatency, CircularBuffer buffer, WaveFormat waveFormat)
            {
                MethodCalls.Add("InitRecording");
            }

            public void Play()
            {
                MethodCalls.Add("Play");

            }

            public void Record()
            {
                MethodCalls.Add("Record");
            }

            public void Stop()
            {
                MethodCalls.Add("Stop");

            }
        }
        public class TestDriver : IDriver
        {
            public string Name => "TEST";
            public IEnumerable<IDevice> GetDevices()
            {
                return new[] { new TestDevice("TEST INPUT"),new TestDevice("TEST OUTPUT"), };
            }
        }
        [SetUp]
        protected void Setup()
        {
            AudioService.Instance.Reset();
            AudioService.Instance.Init();
            
            DriverManager.Instance.AddDriver(new TestDriver());
            _inputEnum = new WaveInputDevice("TEST INPUT");
            _outputEnum = new WaveOutputDevice("TEST OUTPUT");
            _inputNullEnum = new WaveInputDevice("Null: Input"); 
            _outputNullEnum = new WaveOutputDevice("Null: Output"); 
            _inputDevice = ((TestDevice)_inputEnum.Tag);
            _outputDevice = ((TestDevice)_outputEnum.Tag);
            Assert.AreEqual("TEST INPUT", _inputDevice.Name);
            Assert.AreEqual("TEST OUTPUT", _outputDevice.Name);

        }
        [Test]
        public void TestLifecyclePlayStopPlay()
        {
            InputDevice input = new InputDevice();
            OutputDevice output = new OutputDevice();
            input.Config.Phase = LifecyclePhase.Booting;
            output.Config.Phase = LifecyclePhase.Booting;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);

            
            Thread.Sleep(1);
            Log.Logger.Information("PHASE {i}->{phase} ", input.Config.Phase, output.Config.Phase);
            Assert.AreEqual(LifecyclePhase.Booting, input.Config.Phase);
            Assert.AreEqual(LifecyclePhase.Booting, output.Config.Phase);
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Config.Phase = LifecyclePhase.Stopped;
            output.Config.Phase = LifecyclePhase.Stopped;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            Assert.AreEqual(new string[]{"InitRecording", "Record", "Stop", "Record"}, _inputDevice.MethodCalls);
            Assert.AreEqual(new string[]{"InitPlayback", "Play", "Stop", "Play"}, _outputDevice.MethodCalls);
        }

        [Test]
        public void TestLifecycleChangeDevice()
        {
            InputDevice input = new InputDevice();
            OutputDevice output = new OutputDevice();
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Config.Phase = LifecyclePhase.Stopped;
            output.Config.Phase = LifecyclePhase.Stopped;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            input.Update(_inputNullEnum);
            output.Update(input.Output, _outputNullEnum);
            
            input.Config.Phase = LifecyclePhase.Playing;
            output.Config.Phase = LifecyclePhase.Playing;
            input.Update(_inputEnum);
            output.Update(input.Output, _outputEnum);
            
            output.Update(input.Output, _outputNullEnum);
            output.Update(input.Output, _outputEnum);

            input.Update(_inputNullEnum);
            input.Update(_inputEnum);

            Assert.AreEqual(new string[]{"InitRecording", "Record", "Stop","InitRecording", "Record", "Stop", "InitRecording", "Play"}, _inputDevice.MethodCalls);
            Assert.AreEqual(new string[]{"InitPlayback", "Play", "Stop", "InitPlayback", "Play", "Stop", "InitPlayback", "Play"}, _outputDevice.MethodCalls);
        }
        
        
    }
}