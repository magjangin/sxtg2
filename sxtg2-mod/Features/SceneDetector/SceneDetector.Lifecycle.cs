using System;
using MelonLoader;
using UnityEngine.SceneManagement;

namespace sxtg2.Features
{
    public partial class SceneDetector
    {
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

        public void Cleanup()
        {
            if (!_isInitialized)
                return;

            try
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneUnloaded -= OnSceneUnloaded;

                _isInitialized = false;
                MelonLogger.Msg("씬 감지 모드 정리 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"씬 감지 모드 정리 중 오류 발생: {ex.Message}");
            }
        }

        public string GetCurrentSceneName()
        {
            return _currentSceneName;
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }
    }
}
