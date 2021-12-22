using System;
using VL.Lib.IO;
using VL.NewAudio.Files;

namespace VL.NewAudio.Nodes
{
    public class AudioFileNode : IDisposable
    {
        private AudioFileReaderSource? _source;
        public AudioFileReaderSource? Source => _source;
        private bool _disposedValue;
        private IAudioFileReader? _reader;
        private AudioFileBufferedReader? _bufferedReader;
        public int SampleRate { get; private set; }
        public bool IsLooping { get; set; }
        
        public Path? Path { get; set; }
        
        public void Update()
        {
            _bufferedReader?.Dispose();
            _reader?.Dispose();
            _bufferedReader = null;
            _reader = null;
            if (Path == null || !Path.IsFile || !Path.Exists)
            {
                return;
            }

            var path = Path.ToString();
            var lower = path.ToLower();

            if (lower.EndsWith("mp3"))
            {
                _reader = new Mp3FileReader();
            }
            else if (lower.EndsWith("wav"))
            {
                _reader = new WavFileReader();
            }

            if (_reader != null)
            {
                _bufferedReader = new AudioFileBufferedReader(_reader, 1 << 18);
                _bufferedReader.Open(path);
                _source = new AudioFileReaderSource(_bufferedReader);
                _source.IsLooping = IsLooping;
                SampleRate = _reader.SampleRate;
            }
            else
            {
                _source = null;
            }
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _bufferedReader?.Dispose();
                _reader?.Dispose();
                _disposedValue = true;
            }
        }
    }
}