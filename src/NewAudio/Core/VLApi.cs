using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;

namespace NewAudio.Core
{
    public interface IVLApi
    {
        IResourceHandle<AudioService> GetAudioService();
        IResourceHandle<AudioGraph> GetAudioGraph();
        IResourceHandle<DriverManager> GetDriverManager();
        IResourceHandle<IDevice> GetInputDevice(WaveInputDevice device);
        IResourceHandle<IDevice> GetOutputDevice(WaveOutputDevice device);

    }
    public class VLApi : IVLApi
    {
        public static IVLApi Instance = new VLApi();

        public IResourceHandle<AudioGraph> GetAudioGraph()
        {
            var pool = ResourceProvider.NewPooledPerApp(NodeContext.Current, "AudioGraph", s => new AudioGraph());
            return pool.GetHandle();
        }

        public IResourceHandle<AudioService> GetAudioService()
        {
            var pool = ResourceProvider.NewPooledSystemWide("AudioService", s => new AudioService());
            return pool.GetHandle();
        }
        public IResourceHandle<DriverManager> GetDriverManager()
        {
            var pool = ResourceProvider.NewPooledSystemWide("DriverManager", s => new DriverManager());
            return pool.GetHandle();
        }

        public IResourceHandle<IDevice> GetInputDevice(WaveInputDevice device)
        {
            var name = device.Value;
            var pool = ResourceProvider.NewPooledSystemWide($"Device.{name}", s => (IDevice)device.Tag);
            return pool.GetHandle();
        }
        public IResourceHandle<IDevice> GetOutputDevice(WaveOutputDevice device)
        {
            var name = device.Value;
            var pool = ResourceProvider.NewPooledSystemWide($"Device.{name}", s => (IDevice)device.Tag);
            return pool.GetHandle();
        }
    }
}