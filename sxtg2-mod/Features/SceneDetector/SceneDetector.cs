namespace sxtg2.Features
{
    public partial class SceneDetector
    {
        private string _currentSceneName = "";
        private bool _isInitialized = false;
        private float _checkInterval = 0.5f; // 0.5초마다 체크
        private float _lastCheckTime = 0f;
        private float _musicSelectAnalysisDelay = 0f;
        private float _resultScreenImageLogDelay = 0f;
        private float _playLoadingScreenImageDelay = 0f;
    }
}
