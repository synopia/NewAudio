using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewAudio.Devices;
using NewAudio.Dsp;
using Serilog;
using Serilog.Formatting.Display;
using VL.Lib.Basics.Resources;
using Xt;

namespace NewAudio.Core
{
    public class AudioServiceThread : IAudioService
    {
        private interface IAudioEvent
        {
        }

        private struct OpenDeviceEvent : IAudioEvent
        {
            public string SessionId;
            public string DeviceId;
            public ChannelConfig Config;
        }

        private struct CloseDeviceEvent : IAudioEvent
        {
            public string SessionId;
        }

        private struct OpenStreamEvent : IAudioEvent
        {
            public string SessionId;
        }

        private struct CloseStreamEvent : IAudioEvent
        {
            public string SessionId;
        }

        private struct UpdateFormatEvent : IAudioEvent
        {
            public string DeviceId;
            public FormatConfig Config;
        }

        private struct SearchFormatEvent : IAudioEvent
        {
            public string DeviceId;
        }

        private static readonly IEnumerable<XtSample> FormatList = new[]
        {
            XtSample.Float32,
            XtSample.Int32,
            XtSample.Int16,
            XtSample.Int24,
        };

        private readonly ILogger _logger;
        private Thread _thread;
        private readonly IResourceProvider<IXtPlatform> _platformProvider;
        private IXtPlatform _platform;
        private readonly List<DeviceSelection> _defaultDevices = new();
        private readonly List<DeviceSelection> _deviceSelections = new();
        private readonly Dictionary<XtSystem, IXtService> _services = new();


        private readonly Dictionary<string, DeviceState> _devices = new();
        private readonly Dictionary<string, Session> _sessions = new();

        private readonly ConcurrentQueue<IAudioEvent> _fastQueue = new();
        private ConcurrentQueue<IAudioEvent> _slowQueue = new();

        public bool IsRunning { get; private set; }
        private CancellationTokenSource _cts = new();
        private AutoResetEvent _readyEvent = new(false);

        public AudioServiceThread(ILogger logger, IResourceProvider<IXtPlatform> platformProvider)
        {
            _logger = logger;
            _platformProvider = platformProvider;
            StartThread();
        }

        private void StartThread()
        {
            _thread = new Thread(Loop);
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Priority = ThreadPriority.Highest;
            _thread.IsBackground = true;
            _thread.Start();

            _readyEvent.WaitOne();
        }

        private void StopThread()
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }

            IsRunning = false;
            var res = _thread.Join(250);
            if (!res)
            {
                _thread.Abort();
            }

            _cts = null;
            _thread = null;
        }

        private void Loop()
        {
            try
            {
                _logger.Information("============================================");
                _logger.Information("Initializing Audio Service");
                _cts = new();
                _platform = _platformProvider.GetHandle().Resource;
                _platform.OnError += (msg) =>
                {
                    _logger.Error("============================================");
                    _logger.Error("Error: {Msg}", msg);
                };

                XtAudio.SetOnError(_platform.DoOnError);
                InitSelectionEnums();
                _logger.Information(
                    "Found {Inputs} input and {Outputs} output devices", GetInputDevices().Count(),
                    GetOutputDevices().Count());

                while (!_cts.Token.IsCancellationRequested)
                {
                    IsRunning = true;
                    _readyEvent.Set();

                    Thread.Sleep(20);
                    // Trace.WriteLine(
                        // $"Alive {Thread.CurrentThread.ManagedThreadId} {Thread.CurrentThread.ThreadState}");

                    IAudioEvent audioEvent;
                    var result = _fastQueue.TryDequeue(out audioEvent);
                    if (result)
                    {
                        Process(audioEvent);
                    }
                    
                }
                _logger.Information("============================================");
                _logger.Information("Shutting down Audio Service");
                
            }
            catch (Exception e)
            {
                _logger.Error("============================================");
                _logger.Error(e, "AudioService.Loop");
            }
            finally
            {
                foreach (var state in _devices.Values)
                {
                    state.XtStream?.Stop();
                    state.XtDevice?.Dispose();
                }
                _platform.Dispose();
            }
        }

        private void Process(IAudioEvent audioEvent)
        {
            if (audioEvent is OpenDeviceEvent openDevice)
            {
                InitializeDevice(openDevice.SessionId, openDevice.DeviceId, openDevice.Config);
            }
            else if (audioEvent is CloseDeviceEvent closeDevice)
            {
                UninitializeDevice(closeDevice.SessionId);
            }
            else if (audioEvent is OpenStreamEvent openStream)
            {
                StartStream(openStream.SessionId);
            }
            else if (audioEvent is CloseStreamEvent closeStream)
            {
                StopStream(closeStream.SessionId);
            }
        }

        private int OnBuffer(XtStream stream, in XtBuffer deviceBuffer, object user)
        {
            /*
            if (deviceBuffer.output == IntPtr.Zero || _outputDeviceBlocks.Count != 1)
            {
                return 0;
            }

            var inputBlock = _inputDeviceBlocks.Count == 1 ? _inputDeviceBlocks[0] : null;
            
            Trace.Assert(this == _outputDeviceBlocks[0].Device);

            int frames = deviceBuffer.frames;
            // read inputs
            if (inputBlock != null )
            {
                _convertReader.Read(deviceBuffer, 0, inputBlock.InputBuffer, 0, frames);
                _inputBufferPos += frames;
            }

            // render output
            _internalOutputBuffer = _outputDeviceBlocks[0].RenderInputs(frames);
            _convertWriter.Write(_internalOutputBuffer, 0, deviceBuffer, 0, frames);
            _outputBufferPos += frames;
            */

            return 0;
        }

        private void OnRunning(XtStream stream, bool running, ulong error, object user)
        {
            if (error != 0)
            {
                // IsProcessing = false;
                // _error = error;
                _logger.Error("XtAudio Error: {Error}", XtAudio.GetErrorInfo(error));
            }

            _logger.Information("OnRunning {Running}", running);
            if (!running)
            {
                // IsProcessing = false;
            }
        }

        private void OnRun(XtStream stream, int index, object user)
        {
            _logger.Information("OnRun {Index}", index);
        }


        private void StopStream(string sessionId)
        {
            _logger.Information("StopStream for Session={Session}", sessionId);
            var session = _sessions[sessionId];
            if (!session.IsProcessing)
            {
                _logger.Error("Session not running!");
                return;
            }

            session.IsProcessing = false;

            var allSessions = _sessions.Values.Where(s => s.DeviceId == session.DeviceId).ToArray();
            if (!allSessions.Any(s => s.IsProcessing))
            {
                _logger.Information("No more sessions playing on device {Device}. Stopping", session.DeviceId);
                var device = OpenDevice(session.DeviceId);

                device.IsProcessing = false;
                device.XtStream.Stop();
            }
        }

        private void StartStream(string sessionId)
        {
            _logger.Information("StartStream for Session={Session}", sessionId);
            var session = _sessions[sessionId];
            if (!session.IsInitialized)
            {
                _logger.Error("Session not initialized!");
                return;
            }

            var device = OpenDevice(session.DeviceId);
            if (!device.IsInitialized)
            {
                _logger.Error("Device not initialized!");
                return;
            }
            if (!device.IsProcessing)
            {
                _logger.Information("Device not playing. Starting {Device} ", session.DeviceId);
                device.XtStream.Start();
                device.IsProcessing = true;
            }

            session.IsProcessing = true;
        }


        private void UninitializeDevice(string sessionId)
        {
            _logger.Information("UninitializeDevice Session={Session}", sessionId);
            var session = _sessions[sessionId];
            session.IsProcessing = false;
            session.IsInitialized = false;

            var all = _sessions.Values.Where(s => s.DeviceId == session.DeviceId).ToArray();
            if (!all.Any(d => d.IsInitialized))
            {
                _logger.Information("No more sessions initialized to play on device {Device}. Stopping", session.DeviceId);
                var entry = _devices[session.DeviceId];
                if (entry.XtDevice != null)
                {
                    entry.XtDevice.Dispose();
                    entry.XtDevice = null;
                }

                entry.IsInitialized = false;
                entry.IsProcessing = false;
            }

            _sessions.Remove(sessionId);
        }

        private void InitializeDevice(string sessionId, string deviceId, ChannelConfig config)
        {
            _logger.Information("InitializeDevice {Device}, Session={Session}, In={In}, Out={Out}", deviceId, sessionId,
                config.InputChannels, config.OutputChannels);
            var session = _sessions[sessionId];
            session.ChannelConfig = config;
            
            var device = OpenDevice(deviceId);
            if (device.IsProcessing || device.IsInitialized)
            {
                if (config.InputChannels <= device.Channels.InputChannels &&
                    config.OutputChannels <= device.Channels.OutputChannels &&
                    (session.Format.SampleRate == 0 || session.Format.SampleRate == device.Format.SampleRate))
                {
                    _logger.Information("Device running, format fits.. nothing more to do");
                    session.Format.FramesPerBlock = device.Format.FramesPerBlock;
                    session.Format.SampleRate = device.Format.SampleRate;
                    session.Channels.InputChannels = config.InputChannels;
                    session.Channels.OutputChannels = config.OutputChannels;
                    session.IsInitialized = true;
                    session.IsProcessing = false;
                    return;
                }

                _logger.Information("Device running, format does not fit. Stopping all playing streams");
                device.XtStream?.Stop();
                device.XtStream?.Dispose();
                device.XtStream = null;
                device.IsInitialized = false;
                foreach (var s in _sessions.Values.Where(s => s.DeviceId == deviceId).ToArray())
                {
                    s.IsInitialized = false;
                }
            }

            if (!device.IsInitialized)
            {
                _logger.Information("Initializing device");
                var all = _sessions.Values.Where(s => s.DeviceId == device.DeviceId).ToArray();

                var outputChannels = all.Length > 0 ? all.Max(b => b.ChannelConfig.OutputChannels) : 0;
                var inputChannels = all.Length > 0 ? all.Max(b => b.ChannelConfig.InputChannels) : 0;
                if (outputChannels > device.Caps.MaxOutputChannels ||
                    inputChannels > device.Caps.MaxInputChannels)
                {
                    _logger.Error("More channels requested then allowed");
                    // todo error?
                    return;
                }

                device.Channels.OutputChannels = outputChannels;
                device.Channels.InputChannels = inputChannels;

                var allSampleRates = all.Select(s => s.Format.SampleRate).Where(s => s > 0).Concat(new[] { 44100 })
                    .Distinct().OrderBy(s => -s).ToArray();
                var format = SearchFormat(device.XtDevice, allSampleRates, inputChannels, outputChannels);
                if (format.mix.rate == 0)
                {
                    _logger.Error("No working format found");
                    // todo error
                    return;
                }

                device.Format.SampleRate = format.mix.rate;
                device.Format.SampleType = format.mix.sample;

                var (convReader, convWriter) = GetConverter(device.Caps, format.mix.sample);

                device.ConvertReader = convReader;
                device.ConvertWriter = convWriter;

                var bufferSize = device.XtDevice.GetBufferSize(format);
                var chosenBufferSize = AudioMath.ClampD(device.Format.BufferSizeMs, bufferSize.min,
                    bufferSize.max);

                device.Format.BufferSizeMs = chosenBufferSize;
                device.Caps.BufferSizeMsMin = bufferSize.min;
                device.Caps.BufferSizeMsMax = bufferSize.max;

                var streamParams = new XtStreamParams(!device.Caps.NonInterleaved, OnBuffer, OnRun,
                    OnRunning);
                var deviceParams = new XtDeviceStreamParams(streamParams, format, chosenBufferSize);
                device.XtStream = device.XtDevice.OpenStream(deviceParams, null);

                device.Format.FramesPerBlock = device.XtStream.GetFrames();
                _logger.Information("Device started, Sample rate={SampleRate}, Format={Type}", device.Format.SampleRate,
                    device.Format.SampleType);
                device.IsInitialized = true;
                
                for (var i = 0; i < all.Length; i++)
                {
                    all[i].Channels = all[i].ChannelConfig;
                    all[i].Format = device.Format;
                    all[i].IsInitialized = true;
                }
            }
        }


        private XtFormat SearchFormat(IXtDevice device, IEnumerable<int> allSampleRates, int inputChannels,
            int outputChannels)
        {
            foreach (var sampleRate in allSampleRates)
            {
                foreach (var sample in FormatList)
                {
                    var mix = new XtMix(sampleRate, sample);
                    var format = new XtFormat(mix, new XtChannels(inputChannels, 0, outputChannels, 0));
                    _logger.Information("Testing SampleRate={SampleRate}, SampleType={SampleType}", format.mix.rate,
                        format.mix.sample);
                    if (device.SupportsFormat(format))
                    {
                        return format;
                    }
                }
            }

            return default;
        }


        private (IConvertReader, IConvertWriter) GetConverter(DeviceCaps caps, XtSample sample)
        {
            if (!caps.NonInterleaved)
            {
                IConvertWriter convertWriter = sample switch
                {
                    XtSample.Float32 => new ConvertWriter<Float32Sample, Interleaved>(),
                    XtSample.Int16 => new ConvertWriter<Int16LsbSample, Interleaved>(),
                    XtSample.Int24 => new ConvertWriter<Int24LsbSample, Interleaved>(),
                    XtSample.Int32 => new ConvertWriter<Int32LsbSample, Interleaved>(),
                    _ => throw new NotImplementedException()
                };
                IConvertReader convertReader = sample switch
                {
                    XtSample.Float32 => new ConvertReader<Float32Sample, Interleaved>(),
                    XtSample.Int16 => new ConvertReader<Int16LsbSample, Interleaved>(),
                    XtSample.Int24 => new ConvertReader<Int24LsbSample, Interleaved>(),
                    XtSample.Int32 => new ConvertReader<Int32LsbSample, Interleaved>(),
                    _ => throw new NotImplementedException()
                };
                return (convertReader, convertWriter);
            }

            IConvertWriter convertWriter2 = sample switch
            {
                XtSample.Float32 => new ConvertWriter<Float32Sample, NonInterleaved>(),
                XtSample.Int16 => new ConvertWriter<Int16LsbSample, NonInterleaved>(),
                XtSample.Int24 => new ConvertWriter<Int24LsbSample, NonInterleaved>(),
                XtSample.Int32 => new ConvertWriter<Int32LsbSample, NonInterleaved>(),
                _ => throw new NotImplementedException()
            };
            IConvertReader convertReader2 = sample switch
            {
                XtSample.Float32 => new ConvertReader<Float32Sample, NonInterleaved>(),
                XtSample.Int16 => new ConvertReader<Int16LsbSample, NonInterleaved>(),
                XtSample.Int24 => new ConvertReader<Int24LsbSample, NonInterleaved>(),
                XtSample.Int32 => new ConvertReader<Int32LsbSample, NonInterleaved>(),
                _ => throw new NotImplementedException()
            };
            return (convertReader2, convertWriter2);
        }

        public int UpdateFormat(string deviceId, FormatConfig config)
        {
            _fastQueue.Enqueue(new UpdateFormatEvent() { DeviceId = deviceId, Config = config});
            return 0;
        }

        public DeviceCaps GetDeviceInfo(DeviceSelection selection)
        {
            return _devices[selection.DeviceId].Caps;
        }

        private DeviceState OpenDevice(string deviceId)
        {
            var entry = _devices[deviceId];
            if (entry.XtDevice != null)
            {
                return entry;
            }

            _logger.Information("OpenDevice {Device}", entry.Caps.Name);
            entry.XtDevice = GetService(entry.Caps.System).OpenDevice(deviceId);
            entry.Channels = new ChannelConfig();
            entry.Format = new FormatConfig();
            entry.ConvertReader = null;
            entry.ConvertWriter = null;
            entry.XtStream = null;
            entry.IsInitialized = false;
            entry.IsProcessing = false;
            return entry;
        }

        public Session OpenDevice(string deviceId, ChannelConfig config)
        {
            var sessionId = Guid.NewGuid().ToString();
            var session = new Session()
            {
                SessionId = sessionId,
                DeviceId = deviceId,
                ChannelConfig = config,
                Channels = new ChannelConfig(),
                Format = new FormatConfig()
            };
            _sessions[sessionId] = session;
            _fastQueue.Enqueue(new OpenDeviceEvent { SessionId = sessionId,DeviceId = deviceId, Config = config });
            return session;
        }

        public void CloseDevice(string sessionId)
        {
            _fastQueue.Enqueue(new CloseDeviceEvent { SessionId = sessionId });
        }

        public void OpenStream(string sessionId)
        {
            _fastQueue.Enqueue(new OpenStreamEvent
            {
                SessionId = sessionId,
            });
        }

        public void CloseStream(string sessionId)
        {
            _fastQueue.Enqueue(new CloseStreamEvent { SessionId = sessionId });
        }
        
        public IEnumerable<DeviceSelection> GetInputDevices()
        {
            return _deviceSelections.Where(i => i.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetOutputDevices()
        {
            return _deviceSelections.Where(i => i.IsOutputDevice);
        }

        public IEnumerable<DeviceSelection> GetDefaultInputDevices()
        {
            return _defaultDevices.Where(i => i.IsInputDevice);
        }

        public IEnumerable<DeviceSelection> GetDefaultOutputDevices()
        {
            return _defaultDevices.Where(i => i.IsOutputDevice);
        }


        private IXtService GetService(XtSystem system)
        {
            if (_services.ContainsKey(system))
            {
                return _services[system];
            }

            var service = _platform.GetService(system);
            _services[system] = service;

            return service;
        }

        private void InitSelectionEnums()
        {
            _deviceSelections.Clear();
            var systems = new[] { XtSystem.ASIO, XtSystem.WASAPI, XtSystem.DirectSound };
            foreach (var system in systems)
            {
                using var list = GetService(system).OpenDeviceList(XtEnumFlags.All);
                var outputDefault = GetService(system).GetDefaultDeviceId(true);
                var inputDefault = GetService(system).GetDefaultDeviceId(true);


                for (int d = 0; d < list.GetCount(); d++)
                {
                    string id = list.GetId(d);
                    try
                    {
                        using IXtDevice device = GetService(system).OpenDevice(id);

                        var caps = list.GetCapabilities(id);

                        var deviceId = id;
                        var deviceSelection = new DeviceSelection(system, deviceId, list.GetName(id),
                            (caps & XtDeviceCaps.Input) != 0, (caps & XtDeviceCaps.Output) != 0);
                        _devices[deviceId] = new DeviceState(){
                            DeviceId = deviceId,
                            Caps = new DeviceCaps()
                            {
                                Caps = caps,
                                DeviceId = deviceId,
                                Name = list.GetName(id),
                                System = system,
                                MaxInputChannels = device.GetChannelCount(false),
                                MaxOutputChannels = device.GetChannelCount(true),
                                Interleaved = device.SupportsAccess(true),
                                NonInterleaved = device.SupportsAccess(false),
                            }
                        };
                        _deviceSelections.Add(deviceSelection);
                        if (id == outputDefault || id == inputDefault)
                        {
                            _defaultDevices.Add(deviceSelection);
                        }
                    }
                    catch (XtException e)
                    {
                        _devices[id] = new DeviceState()
                        {
                            ThrowsException = true,
                            Error = e.Message
                        };
                    }
                }
            }

            _deviceSelections.Sort((a, b) => string.Compare(a.ToString(), b.ToString(), StringComparison.Ordinal));

            OutputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetOutputDevices())
            {
                OutputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }

            InputDeviceDefinition.Instance.Clear();
            foreach (var selection in GetInputDevices())
            {
                InputDeviceDefinition.Instance.AddEntry(selection.ToString(), selection);
            }
        }

        public void Dispose()
        {
            StopThread();
            _thread = null;
        }
    }
}