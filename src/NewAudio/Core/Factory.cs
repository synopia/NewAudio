using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;
using VL.Model;

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
    }
}