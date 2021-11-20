using System;
using NewAudio.Block;
using NewAudio.Devices;
using VL.Core;
using VL.Lib.Basics.Resources;

namespace NewAudio.Core
{
    public static class Factory 
    {
        public static IResourceHandle<AudioGraph> GetAudioGraph()
        {
            try
            {
                var nodeContext = NodeContext.Current;
                var pool = ResourceProvider.NewPooledPerApp(nodeContext, "AudioGraph", _ => new AudioGraph());
                return pool.GetHandle();
            }
            catch (InvalidOperationException e)
            {
                var pool = ResourceProvider.NewPooledSystemWide( "AudioGraph", _ => new AudioGraph());
                return pool.GetHandle();
                
            }

            
        }

        public static IResourceHandle<AudioService> GetAudioService()
        {
            var pool = ResourceProvider.NewPooledSystemWide("AudioService", _ => new AudioService());
            return pool.GetHandle();
        }

        public static IResourceHandle<DriverManager> GetDriverManager()
        {
            var pool = ResourceProvider.NewPooledSystemWide("DriverManager", _ => new DriverManager());
            return pool.GetHandle();
        }
    }
}