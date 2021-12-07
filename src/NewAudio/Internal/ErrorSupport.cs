using System;
using System.Linq;
using System.Reactive.Subjects;
using VL.Lang;

namespace VL.NewAudio.Internal
{
    public class ErrorSupport
    {
        public readonly BehaviorSubject<Message[]> Messages = new(new Message[]{});

        public void ClearError()
        {
            Messages.OnNext(Array.Empty<Message>());
        }

        public void AddError(string error)
        {
            AddMessage(new Message(MessageSeverity.Error, error));
        }
        
        public void AddMessage(Message? message)
        {
            if (message == null)
            {
                return;
            }
            var errors = Messages.Value;
            if (Array.IndexOf(errors, message) == -1)
            {
                Messages.OnNext(errors.Append(message).ToArray());
            }
        }

    }
}