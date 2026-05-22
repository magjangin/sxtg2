using MelonLoader;
using UnityEngine;
using sxtg2.Helpers.Screen;

namespace sxtg2.Features
{
    public partial class SceneDetector
    {
        public void Update()
        {
            if (!_isInitialized)
                return;

            // 주기적으로 씬 체크 (이벤트가 작동하지 않는 경우를 대비)
            if (Time.time - _lastCheckTime >= _checkInterval)
            {
                CheckCurrentScene();
                _lastCheckTime = Time.time;
            }

            // MusicSelect 씬 분석 지연 처리 (씬 로드 시 한 번만)
            if (_musicSelectAnalysisDelay > 0f)
            {
                _musicSelectAnalysisDelay -= Time.deltaTime;
                if (_musicSelectAnalysisDelay <= 0f)
                {
                    MelonLogger.Msg("[디버그] Update: 지연 완료, AnalyzeMusicSelectScene 호출");
                    MusicSelectAnalyzer.AnalyzeMusicSelectScene();
                    _musicSelectAnalysisDelay = 0f; // 지연 완료 후 리셋
                }
            }

            // Result 씬 자켓 이미지 설정 지연 처리 (씬 로드 시 한 번만)
            if (_resultScreenImageLogDelay > 0f)
            {
                _resultScreenImageLogDelay -= Time.deltaTime;
                if (_resultScreenImageLogDelay <= 0f)
                {
                    MelonLogger.Msg("[디버그] Update: 지연 완료, Result 화면 자켓 이미지 설정");
                    ResultScreenHelper.SetResultScreenJacketImage();
                    _resultScreenImageLogDelay = 0f; // 지연 완료 후 리셋
                }
            }

            // PlayLoading 씬 풀사이즈 자켓 이미지 설정 지연 처리
            if (_playLoadingScreenImageDelay > 0f)
            {
                _playLoadingScreenImageDelay -= Time.deltaTime;
                if (_playLoadingScreenImageDelay <= 0f)
                {
                    MelonLogger.Msg("[디버그] Update: 지연 완료, PlayLoading 씬 풀사이즈 자켓 이미지 설정");
                    PlayLoadingScreenHelper.SetPlayLoadingScreenJacketImage();
                    _playLoadingScreenImageDelay = 0f;
                }
            }
        }
    }
}
