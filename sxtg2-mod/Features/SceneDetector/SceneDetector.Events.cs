using System;
using MelonLoader;
using UnityEngine.SceneManagement;
using sxtg2.Hooks.Text;

namespace sxtg2.Features
{
    public partial class SceneDetector
    {
        private void CheckCurrentScene()
        {
            try
            {
                Scene activeScene = SceneManager.GetActiveScene();
                string sceneName = activeScene.name;
                int sceneIndex = activeScene.buildIndex;

                // 씬이 변경되었는지 확인
                if (sceneName != _currentSceneName)
                {
                    if (!string.IsNullOrEmpty(_currentSceneName))
                    {
                        MelonLogger.Msg($"[씬 변경] {_currentSceneName} → {sceneName} (인덱스: {sceneIndex})");
                    }
                    else
                    {
                        MelonLogger.Msg($"[씬 감지] 현재 씬: {sceneName} (인덱스: {sceneIndex})");
                    }

                    _currentSceneName = sceneName;
                    MusicSelectAnalyzer.ResetAnalysisFlag(); // 씬 변경 시 분석 플래그 리셋
                    TextHook.ResetInjectionFlag(); // 씬 변경 시 BMS 주입 플래그 리셋
                    MelonLogger.Msg("[디버그] CheckCurrentScene: 씬 변경, 분석 플래그 리셋");

                    // MusicSelect 씬인 경우 분석 수행 (지연 후)
                    if (sceneName.Contains("MusicSelect") || sceneName == "MusicSelect")
                    {
                        MelonLogger.Msg("[디버그] CheckCurrentScene: MusicSelect 씬 감지, 지연 설정 (0.5초)");
                        _musicSelectAnalysisDelay = 0.5f; // 0.5초 후 분석
                    }

                    // Result 씬인 경우 이미지 오브젝트 출력 및 자켓 이미지 설정
                    if (sceneName.Contains("Result") || sceneName == "Result")
                    {
                        MelonLogger.Msg("[디버그] CheckCurrentScene: Result 씬 감지, 이미지 오브젝트 출력 및 자켓 이미지 설정 예정 (0.5초 후)");
                        _resultScreenImageLogDelay = 0.5f; // 0.5초 후 출력 및 설정
                    }

                    // PlayLoading 씬인 경우 풀사이즈 자켓 이미지 설정
                    if (sceneName.Contains("PlayLoading") || sceneName == "PlayLoading")
                    {
                        MelonLogger.Msg("[디버그] CheckCurrentScene: PlayLoading 씬 감지, 풀사이즈 자켓 이미지 설정 예정 (0.5초 후)");
                        _playLoadingScreenImageDelay = 0.5f; // 0.5초 후 설정
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"씬 체크 중 오류 발생: {ex.Message}");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                MelonLogger.Msg($"[씬 로드] 씬 이름: {scene.name}, 인덱스: {scene.buildIndex}, 모드: {mode}");
                _currentSceneName = scene.name;
                MusicSelectAnalyzer.ResetAnalysisFlag();
                TextHook.ResetInjectionFlag(); // 씬 변경 시 BMS 주입 플래그 리셋
                MelonLogger.Msg("[디버그] OnSceneLoaded: 분석 플래그 리셋");

                // MusicSelect 씬인 경우 분석 수행
                if (scene.name.Contains("MusicSelect") || scene.name == "MusicSelect")
                {
                    MelonLogger.Msg("[디버그] OnSceneLoaded: MusicSelect 씬 감지, 지연 설정 (0.5초)");
                    // 씬이 완전히 로드될 때까지 약간의 지연 후 분석 (0.5초)
                    _musicSelectAnalysisDelay = 0.5f;
                }

                // Result 씬인 경우 이미지 오브젝트 출력 및 자켓 이미지 설정
                if (scene.name.Contains("Result") || scene.name == "Result")
                {
                    MelonLogger.Msg("[디버그] OnSceneLoaded: Result 씬 감지, 이미지 오브젝트 출력 및 자켓 이미지 설정 예정 (0.5초 후)");
                    // 씬이 완전히 로드될 때까지 약간의 지연 후 이미지 오브젝트 출력 및 자켓 이미지 설정 (0.5초)
                    _resultScreenImageLogDelay = 0.5f;
                }

                // PlayLoading 씬인 경우 풀사이즈 자켓 이미지 설정
                if (scene.name.Contains("PlayLoading") || scene.name == "PlayLoading")
                {
                    MelonLogger.Msg("[디버그] OnSceneLoaded: PlayLoading 씬 감지, 풀사이즈 자켓 이미지 설정 예정 (0.5초 후)");
                    // 씬이 완전히 로드될 때까지 약간의 지연 후 풀사이즈 자켓 이미지 설정 (0.5초)
                    _playLoadingScreenImageDelay = 0.5f;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"씬 로드 이벤트 처리 중 오류 발생: {ex.Message}");
            }
        }

        private void OnSceneUnloaded(Scene scene)
        {
            try
            {
                MelonLogger.Msg($"[씬 언로드] 씬 이름: {scene.name}, 인덱스: {scene.buildIndex}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"씬 언로드 이벤트 처리 중 오류 발생: {ex.Message}");
            }
        }
    }
}
