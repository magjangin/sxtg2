using MelonLoader;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using sxtg2.Helpers.Screen;
using sxtg2.Hooks.Text;

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        public void Initialize()
        {
            if (_isInitialized)
            {
                MelonLogger.Warning("씬 감지 모드가 이미 초기화되었습니다.");
                return;
            }

            try
            {
                // Unity SceneManager 이벤트 구독
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.sceneUnloaded += OnSceneUnloaded;

                // 현재 씬 감지
                CheckCurrentScene();

                _isInitialized = true;
                MelonLogger.Msg("씬 감지 모드 초기화 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"씬 감지 모드 초기화 중 오류 발생: {ex.Message}");
                throw;
            }
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
