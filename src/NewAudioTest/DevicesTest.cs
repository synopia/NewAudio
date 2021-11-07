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

            public bool IsInputValid(DeviceConfigRequest config)
            {
                return true;
            }

            public bool Free()
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add("Free");
                return true;
            }

            public bool Start()
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add(_isPlaying ? "Play" : "Record");
                return true;
            }

            public bool Stop()
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                MethodCalls.Add("Stop");
                return true;
            }

            public void ExceptionHappened(Exception e, string method)
            {
                throw new NotImplementedException();
            }

            public Task<DeviceConfigResponse> Create(DeviceConfigRequest config)
            {
                Threads.Add(Thread.CurrentThread.ManagedThreadId);
                _isPlaying = config.IsPlaying;
                _isRecording = config.IsRecording;

                // await Task.Delay(1);
                MethodCalls.Add(_isPlaying ? $"InitPlayback" : $"InitRecording");
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

        private void Wait()
        {
            _input.Lifecycle.WaitForEvents.WaitOne();
            _output.Lifecycle.WaitForEvents.WaitOne();
        }

        [Test]
        public void TestLifecyclePlayStopPlay()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;

            _input = new InputDevice();
            _output = new OutputDevice();
            _input.Update(null);
            _output.Update(null, null);
            Wait();
            Assert.AreEqual(LifecyclePhase.Uninitialized, _output.Phase);
            Assert.AreEqual(LifecyclePhase.Uninitialized, _input.Phase);

            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);

            Wait();
            Assert.AreEqual(LifecyclePhase.Init, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, _output.Phase);

            _input.PlayParams.Playing.Value = true;
            _output.PlayParams.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            _input.PlayParams.Playing.Value = true;
            _output.PlayParams.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            _input.PlayParams.Playing.Value = false;
            _output.PlayParams.Playing.Value = false;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Wait();
            Assert.AreEqual(LifecyclePhase.Init, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, _output.Phase);

            _input.PlayParams.Playing.Value = true;
            _output.PlayParams.Playing.Value = true;
            _input.Update(_inputEnum);
            _output.Update(_input.Output, _outputEnum);
            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            Log.Logger.Warning("{x}", _inputDevice.MethodCalls);
            Assert.AreEqual(new[] { "InitRecording", "Record" }, _inputDevice.MethodCalls,
                string.Join(", ", _inputDevice.MethodCalls));
            Assert.AreEqual(new[] { "InitPlayback", "Play" }, _outputDevice.MethodCalls,
                string.Join(", ", _outputDevice.MethodCalls));

            // Assert.AreEqual(1, _inputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, _inputDevice.Threads[0]);
            // Assert.AreEqual(1, _outputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, _outputDevice.Threads[0]);
            _inputDevice.Dispose();
            _outputDevice.Dispose();
        }


        [Test]
        public void TestLifecycleChangeDevice()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _input = new InputDevice();
            _output = new OutputDevice();

            _input.PlayParams.Playing.Value = true;
            _output.PlayParams.Playing.Value = true;
            _input.Update(_inputEnum, SamplingFrequency.Hz8000, 1);
            _output.Update(_input.Output, _outputEnum, SamplingFrequency.Hz8000, 1);
            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            _input.PlayParams.Playing.Value = false;
            _output.PlayParams.Playing.Value = false;
            _input.Update(_inputEnum, SamplingFrequency.Hz8000, 1);
            _output.Update(_input.Output, _outputEnum, SamplingFrequency.Hz8000, 1);
            Wait();
            Assert.AreEqual(LifecyclePhase.Init, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, _output.Phase);

            _input.Update(_inputNullEnum, SamplingFrequency.Hz8000, 2);
            _output.Update(_input.Output, _outputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait();
            Assert.AreEqual(LifecyclePhase.Init, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Init, _output.Phase);

            _input.PlayParams.Playing.Value = true;
            _output.PlayParams.Playing.Value = true;
            _input.Update(_inputEnum, SamplingFrequency.Hz8000, 1);
            _output.Update(_input.Output, _outputEnum, SamplingFrequency.Hz8000, 1);
            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            _output.Update(_input.Output, _outputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait();
            _output.Update(_input.Output, _outputEnum, SamplingFrequency.Hz8000, 1);
            Wait();

            _input.Update(_inputNullEnum, SamplingFrequency.Hz8000, 2);
            Wait();
            _input.Update(_inputEnum, SamplingFrequency.Hz8000, 1);

            Wait();
            Assert.AreEqual(LifecyclePhase.Play, _input.Phase);
            Assert.AreEqual(LifecyclePhase.Play, _output.Phase);

            Assert.AreEqual(
                new[]
                {
                    "InitRecording", "Record", "Stop", "Free", 
                    "InitRecording", "Record", "Stop", "Free",
                    "InitRecording", "Record"
                }, _inputDevice.MethodCalls, string.Join(", ", _inputDevice.MethodCalls));
            Assert.AreEqual(
                new[]
                {
                    "InitPlayback", "Play", "Stop", "Free", 
                    "InitPlayback", "Play", "Stop", "Free", 
                    "InitPlayback", "Play"
                },
                _outputDevice.MethodCalls, string.Join(", ", _outputDevice.MethodCalls));
            // Assert.AreEqual(1, _inputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, _inputDevice.Threads[0]);
            // Assert.AreEqual(1, _outputDevice.Threads.Distinct().Count());
            // Assert.AreEqual(mainThreadId, _outputDevice.Threads[0]);
            _inputDevice.Dispose();
            _outputDevice.Dispose();
        }
    }
}