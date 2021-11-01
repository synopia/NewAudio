using System.Threading;
using NewAudio.Core;
using Serilog;

namespace VL.NewAudio.Core
{
    public class PlayPauseStop
    {
        private ILogger _logger;
        private LifecyclePhase _phase;
        private CancellationTokenSource _tokenSource;
        private CancellationToken _currentToken;

        public PlayPauseStop()
        {
            _logger = AudioService.Instance.Logger;
            _phase = LifecyclePhase.Booting;
            _tokenSource = new CancellationTokenSource();
            _currentToken = _tokenSource.Token;
        }

        public CancellationToken GetToken()
        {
            return _currentToken;
        }

        public void Stop()
        {
            _logger.Information("TOKEN STOP");
            _tokenSource.Cancel();
        }

        public void Play()
        {
            _tokenSource = new CancellationTokenSource();
            _currentToken = _tokenSource.Token;
        }
    }
}