using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using NewAudio.Core;
using NewAudio.Device;
using NewAudio.Processor;
using Serilog;
using VL.Core.Diagnostics;
using VL.Lang;
using Message = VL.Lang.Message;

namespace NewAudio.Nodes
{
    public abstract class AudioNode : IDisposable
    {
        protected ILogger Logger = Resources.GetLogger<AudioNode>();
        public IAudioService AudioService { get; }

        public string[] InputPinNames { get; set; }
        public virtual bool IsEnable { get; set; }
        public abstract bool IsEnabled { get; }
        private bool _disposedValue;

        public readonly BehaviorSubject<Message[]> Messages = new(new Message[]{});

        public void AddError(Message error)
        {
            var errors = Messages.Value;
            if (Array.IndexOf(errors, error) == -1)
            {
                Messages.OnNext(errors.Append(error).ToArray());
            }
        }
        
        protected AudioNode()
        {
            Logger.Information("Created audio node {@This}", this);
            AudioService = Resources.GetAudioService();
            InputPinNames = Array.Empty<string>();
        }

        public bool HasChanged(string memberName, ulong mask)
        {
            var index = Array.IndexOf(InputPinNames, memberName);
            if (index != -1)
            {
                return (mask & ((ulong)1 << index)) != 0;
            }

            return false;
        }

        public abstract Message? Update(ulong mask);
        

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    Logger.Information("Disposing audio node {@This}", this);
                    Messages.Dispose();
                }

                _disposedValue = true;
            }
        }

    }
}