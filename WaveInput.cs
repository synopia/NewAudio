using System;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VL.NewAudio
{
    public class WaveInput : IDisposable
    {
        private BufferedWaveProvider bufferedWave;
        private IWaveIn waveIn;
        private AudioSampleBuffer inputBridge;
        private string errorIn = "";
        private WaveFormat inputFormat;


        public AudioSampleBuffer Update(WaveInputDevice device, out string status,
            out string error,
            out WaveFormat waveFormat, int latency = 300, bool reset = false)
        {
            if (reset)
            {
                if (waveIn != null)
                {
                    waveIn.StopRecording();
                    waveIn.Dispose();
                }

                try
                {
                    waveIn = ((IWaveInputFactory) device.Tag).Create(latency);

                    waveIn.DataAvailable += (s, a) => { bufferedWave.AddSamples(a.Buffer, 0, a.BytesRecorded); };
                    bufferedWave = new BufferedWaveProvider(waveIn.WaveFormat);
                    var sampleProvider = new WaveToSampleProvider(bufferedWave);
                    inputFormat = sampleProvider.WaveFormat;
                    waveIn.StartRecording();
                    inputBridge = new AudioSampleBuffer(sampleProvider.WaveFormat);
                    inputBridge.Update = (b, o, l) => { sampleProvider.Read(b, o, l); };
                }
                catch (Exception e)
                {
                    AudioEngine.Log(e.ToString());
                    errorIn = e.Message;
                    waveIn = null;
                }
            }

            error = errorIn;
            status = waveIn != null ? "Recording" : "Uninitialized";
            waveFormat = inputFormat;
            return inputBridge;
        }

        public void Dispose()
        {
            waveIn.StopRecording();
            waveIn.Dispose();
        }
    }
}