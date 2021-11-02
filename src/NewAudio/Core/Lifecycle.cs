using System;
using System.Threading;
using NewAudio.Core;

namespace VL.NewAudio.Core
{
    public class Lifecycle : IDisposable
    {
        private CancellationToken _currentToken;
        private LifecyclePhase _nextPhase;
        private LifecyclePhase _phase;

        private CancellationTokenSource _tokenSource;

        public Action OnBoot;
        public Action OnPlay;
        public Action OnShutdown;
        public Action OnStop;

        public Lifecycle()
        {
            _phase = LifecyclePhase.Booting;

            CreateToken();
        }

        public LifecyclePhase Phase
        {
            get => _phase;
            set => _nextPhase = value;
        }

        public void Dispose()
        {
            _tokenSource.Cancel();
        }

        public void Update()
        {
            if (_phase != _nextPhase)
            {
                _phase = _nextPhase;
                switch (_phase)
                {
                    case LifecyclePhase.Booting:
                        OnBoot?.Invoke();
                        break;
                    case LifecyclePhase.Playing:
                        CreateToken();
                        OnPlay?.Invoke();
                        break;
                    case LifecyclePhase.Stopped:
                        _tokenSource.Cancel();
                        OnStop?.Invoke();
                        break;
                    case LifecyclePhase.Shutdown:
                        _tokenSource.Cancel();
                        OnShutdown?.Invoke();
                        break;
                }
            }
        }

        public CancellationToken GetToken()
        {
            return _currentToken;
        }

        public void Stop()
        {
            Phase = LifecyclePhase.Stopped;
        }

        private void CreateToken()
        {
            if (_tokenSource == null || _currentToken.IsCancellationRequested)
            {
                _tokenSource = new CancellationTokenSource();
                _currentToken = _tokenSource.Token;
            }
        }

        public void Start()
        {
            Phase = LifecyclePhase.Playing;
        }
    }
}