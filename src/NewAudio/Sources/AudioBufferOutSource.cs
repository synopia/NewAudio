using VL.Lib.Collections;

namespace VL.NewAudio.Sources
{
    public class AudioBufferOutSource: BaseBufferOutSource
    {
        public Spread<float> Buffer { get; set; } = Spread<float>.Empty;

        protected override void OnDataReady(float[] data)
        {
            Buffer = Spread.Create(data);
        }
    }
}