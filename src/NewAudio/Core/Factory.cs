using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public interface IFactory
    {
        IResourceHandle<AudioService> GetAudioService();
        IResourceHandle<AudioGraph> GetAudioGraph();
        IResourceHandle<DriverManager> GetDriverManager();
    }

    public class Factory : IFactory
    {
        public static IFactory Instance = new Factory();

        public IResourceHandle<AudioGraph> GetAudioGraph()
        {
            var pool = ResourceProvider.NewPooledPerApp(NodeContext.Current, "AudioGraph", _ => new AudioGraph());
            return pool.GetHandle();
        }

        public IResourceHandle<AudioService> GetAudioService()
        {
            var pool = ResourceProvider.NewPooledSystemWide("AudioService", _ => new AudioService());
            return pool.GetHandle();
        }

        public IResourceHandle<DriverManager> GetDriverManager()
        {
            var pool = ResourceProvider.NewPooledSystemWide("DriverManager", _ => new DriverManager());
            return pool.GetHandle();
        }
    }
}