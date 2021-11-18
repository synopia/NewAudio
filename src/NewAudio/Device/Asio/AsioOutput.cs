using NAudio.Wave;
using NewAudio.Dsp;
using NewAudio.Nodes;

namespace NewAudio.Devices
{
    /*
    public enum SampleType
    {
        
    }
    public class AsioImpl 
    {
        public AsioOut AsioOut;
        public int NumberOfFramesBuffered;
        public int AudioClientNumFrames;
        public int NumberOfChannels;
        public SampleType SampleType;
        public int BytesPerSample;
        public bool AudioClientInvalidated;
        public string DriverName;
        public RingBuffer<byte> RingBuffer;

        protected void CreateAudioClient(string driverName, int channels)
        {
            DriverName = driverName;
            AsioOut = new AsioOut(DriverName);
        }

        public void Init()
        {
            AudioClientNumFrames = AsioOut.FramesPerBuffer;
            var ringBufferSize = AudioClientNumFrames * NumberOfChannels * BytesPerSample * 2;
            RingBuffer = new RingBuffer<byte>(ringBufferSize);
        }

        public void Uninit()
        {
            AsioOut.Stop();
            AsioOut.Dispose();
        }
        
        
    }
    
    
    public class AsioOutput: IDevice
    {
        public int WriteFramesAvailable { get; }
        

        private void InitClient()
        {
            
        }

        private void RenderAudio()
        {
            var 
        }
    }
    */
    
    
}