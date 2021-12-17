using System;
using System.Globalization;
using NLayer;
using VL.Lib.IO;
using VL.NewAudio.Core;
using VL.NewAudio.Dsp;
using VL.NewAudio.Files;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Nodes
{
    public class AudioFileNode : IDisposable
    {
        private AudioFileReaderSource? _source;
        public AudioFileReaderSource? Source => _source;
        private Path? _path;
        private bool _disposedValue;
        private AudioFileBufferedReader _bufferedReader;
        public int SampleRate { get; private set; }

        public Path? Path
        {
            get => _path;
            set
            {
                if (_path == value)
                {
                    return;
                }

                _path = value;
                if (_path == null || !_path.IsFile || !_path.Exists)
                {
                    return;
                }

                var path = _path.ToString();
                var lower = path.ToLower();
                IAudioFileReader? reader = null;

                if (lower.EndsWith("mp3"))
                {
                    reader = new Mp3FileReader();
                }
                else if (lower.EndsWith("wav"))
                {
                    reader = new WavFileReader();
                }


                if (reader != null)
                {
                    _bufferedReader = new AudioFileBufferedReader(reader, 1 << 18);
                    _bufferedReader.Open(path);
                    _source = new AudioFileReaderSource(_bufferedReader);
                    SampleRate = reader.SampleRate;
                    _source.IsLooping = true;
                }
                else
                {
                    _source = null;
                }
            }
        }

        public void Reset()
        {
            if (_source != null)
            {
                _source.NextReadPos = 0;
            }
        }

        public AudioFileNode()
        {
        }

        public void Dispose()
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
            }
        }
    }
}