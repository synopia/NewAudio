using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NewAudio.Core;
using NewAudio.Devices;
using NewAudio.Nodes;
using NUnit.Framework;
using Serilog;
using SharedMemory;

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
        private InputDevice _input;
        private OutputDevice _output;

        public class TestDevice : IDevice
        {
            public List<string> MethodCalls = new List<string>();
            public List<int> Threads = new List<int>();
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

            public Task<bool> FreeResources()
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add("Free");
                return Task.FromResult(true);
            }

            public Task<bool> StartProcessing()
            {
                
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add(_isPlaying ? "Play" : "Record");
                return Task.FromResult(true);
            }

            public Task<bool> StopProcessing()
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add("Stop");
                return Task.FromResult(true);
            }

            public void ExceptionHappened(Exception e, string method)
            {
                throw new NotImplementedException();
            }

            public Task<DeviceConfigResponse> CreateResources(DeviceConfigRequest config)
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                _isPlaying = config.IsPlaying;
                _isRecording = config.IsRecording;

                // await Task.Delay(1);
                MethodCalls.Add(_isPlaying ? "InitPlayback" : "InitRecording");
                return Task.FromResult(new DeviceConfigResponse());
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

        [SetUp]
        protected void Setup()
        {
            AudioService.Instance.Init();

            DriverManager.Instance.AddDriver(new TestDriver());
            _inputEnum = new WaveInputDevice("TEST INPUT");
            _outputEnum = new WaveOutputDevice("TEST OUTPUT");
            _inputNullEnum = new WaveInputDevice("Null: Input");
            _outputNullEnum = new WaveOutputDevice("Null: Output");
            _inputDevice = ((TestDevice)_inputEnum.Tag);
            _outputDevice = ((TestDevice)_outputEnum.Tag);
            _inputDevice.MethodCalls.Clear();
            _outputDevice.MethodCalls.Clear();
            Assert.AreEqual("TEST INPUT", _inputDevice.Name);
            Assert.AreEqual("TEST OUTPUT", _outputDevice.Name);
        }

        [Test]
        public void TestLifecyclePlayStopPlay()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            
            _input = new InputDevice();
            _output = new OutputDevice();
            _input.Update(null);
            _output.Update(null, null);
            Assert.AreEqual(LifecyclePhase.Uninitialized, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Uninitialized, _output.Phase);

            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);

            Assert.AreEqual(LifecyclePhase.Ready, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Ready, _output.Phase);

            _input.Config.Playing.Value = true;
            _output.Config.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);

            _input.Config.Playing.Value = true;
            _output.Config.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);

            _input.Config.Playing.Value = false;
            _output.Config.Playing.Value = false;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Ready, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Ready, _output.Phase);

            _input.Config.Playing.Value = true;
            _output.Config.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);

            Log.Logger.Warning("{x}", _inputDevice.MethodCalls);
            Assert.AreEqual(new string[] { "InitRecording", "Record", "Stop", "Record" }, _inputDevice.MethodCalls,
                string.Join(", ", _inputDevice.MethodCalls));
            Assert.AreEqual(new string[] { "InitPlayback", "Play", "Stop", "Play" }, _outputDevice.MethodCalls,
                string.Join(", ", _outputDevice.MethodCalls));
            
            Assert.AreEqual(1, _inputDevice.Threads.Distinct().Count());
            Assert.AreEqual(mainThreadId, _inputDevice.Threads[0]);
            Assert.AreEqual(1, _outputDevice.Threads.Distinct().Count());
            Assert.AreEqual(mainThreadId, _outputDevice.Threads[0]);
            _inputDevice.Dispose();
            _outputDevice.Dispose();
        }


        [Test]
        public void TestLifecycleChangeDevice()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _input = new InputDevice();
            _output = new OutputDevice();

            _input.Config.Playing.Value = true;
            _output.Config.Playing.Value = true;
             _input.Update(_inputEnum);
             _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);
            
            _input.Config.Playing.Value = false;
            _output.Config.Playing.Value = false;
             _input.Update(_inputEnum);
             _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Ready, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Ready, _output.Phase);
            
             _input.Update(_inputNullEnum);
             _output.Update(_input.Output, _outputNullEnum);
            Assert.AreEqual(LifecyclePhase.Ready, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Ready, _output.Phase);

            _input.Config.Playing.Value = true;
            _output.Config.Playing.Value = true;
             _input.Update(_inputEnum);
             _output.Update(_input.Output, _outputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);

             _output.Update(_input.Output, _outputNullEnum);
             _output.Update(_input.Output, _outputEnum);

             _input.Update(_inputNullEnum);
             _input.Update(_inputEnum);
            Assert.AreEqual(LifecyclePhase.Playing, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Playing, _output.Phase);

            Assert.AreEqual(
                new[]
                {
                    "InitRecording", "Record", "Stop","Free", "InitRecording", "Record", "Stop", "Free", "InitRecording", "Record"
                }, _inputDevice.MethodCalls, string.Join(", ", _inputDevice.MethodCalls));
            Assert.AreEqual(
                new[] { "InitPlayback", "Play", "Stop", "Free", "InitPlayback", "Play", "Stop","Free", "InitPlayback", "Play" },
                _outputDevice.MethodCalls, string.Join(", ", _outputDevice.MethodCalls));
            Assert.AreEqual(1, _inputDevice.Threads.Distinct().Count());
            Assert.AreEqual(mainThreadId, _inputDevice.Threads[0]);
            Assert.AreEqual(1, _outputDevice.Threads.Distinct().Count());
            Assert.AreEqual(mainThreadId, _outputDevice.Threads[0]);
            _inputDevice.Dispose();
            _outputDevice.Dispose();
        }
    }
}