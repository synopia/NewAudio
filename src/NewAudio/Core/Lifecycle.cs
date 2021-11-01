using System;
using System.Collections.Generic;
using System.Threading;
using NewAudio.Core;
using Serilog;

namespace VL.NewAudio.Core
{
    public class Lifecycle 
    {
        private LifecyclePhase _phase;
        private LifecyclePhase _nextPhase;
        
        public LifecyclePhase Phase
        {
            get => _phase;
            set => _nextPhase = value;
        }

        private CancellationTokenSource _tokenSource;
        private CancellationToken _currentToken;

        public  Action OnBoot;
        public  Action OnPlay;
        public  Action OnStop;
        public  Action OnShutdown;

        public Lifecycle()
        {
            _phase = LifecyclePhase.Booting;
            
            CreateToken();
        }

        public void Update()
        {
            if (_phase != _nextPhase)
            {
                _phase = _nextPhase;
                switch (_phase)
                {
                    case LifecyclePhase.Booting: OnBoot?.Invoke(); break;
                    case LifecyclePhase.Playing: CreateToken(); OnPlay?.Invoke(); break;
                    case LifecyclePhase.Stopped: _tokenSource.Cancel(); OnStop?.Invoke(); break;
                    case LifecyclePhase.Shutdown: OnShutdown?.Invoke(); break;
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
            if (_tokenSource==null || _currentToken.IsCancellationRequested)
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