using System;
using System.Globalization;
using NLayer;
using VL.Lib.IO;
using VL.NewAudio.Dsp;
using VL.NewAudio.Internal;
using VL.NewAudio.Sources;

namespace VL.NewAudio.Nodes
{
    public class AudioFileNode
    {
        private MemoryAudioSource? _source;
        public MemoryAudioSource? Source => _source;
        private Path? _path;

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
                if (lower.EndsWith("mp3"))
                {
                    _source = new MemoryAudioSource(Mp3File.Load(path).ToAudioBuffer());
                    _source.IsLooping = true;
                }
                else if (lower.EndsWith("wav"))
                {
                    _source = new MemoryAudioSource(WaveFile.Load(path).ToAudioBuffer());
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
    }
}