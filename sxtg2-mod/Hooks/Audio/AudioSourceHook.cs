using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Audio
{
    public static class AudioSourceHook
    {
        private static bool _isInitialized = false;
        private static AudioClip _keyBlueTamClipCache = null;

        public static void Initialize()
        {
            MelonLogger.Msg("[AudioSourceHook] Initialize() 호출됨!");

            if (_isInitialized)
            {
                MelonLogger.Msg("[AudioSourceHook] 이미 초기화됨, 리턴");
                return;
            }

            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
                MelonLogger.Msg("[AudioSourceHook] 초기화 완료");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioSourceHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error($"[AudioSourceHook] 스택 트레이스: {ex.StackTrace}");
            }
        }

        public static AudioClip GetCachedKeyBlueTamClip()
        {
            if (_keyBlueTamClipCache == null)
            {
                CacheKeyBlueTamClipFromResources();
            }

            return _keyBlueTamClipCache;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                string sceneName = scene.name;
                if (ModLog.IsVerbose)
                {
                    MelonLogger.Msg($"[AudioSourceHook] 씬 로드됨: {sceneName}");
                }

                if (sceneName.Contains("Warning") || sceneName == "Warning")
                {
                    CacheKeyBlueTamClipFromResources();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AudioSourceHook] OnSceneLoaded 오류: {ex.Message}");
            }
        }

        private static void CacheKeyBlueTamClipFromResources()
        {
            try
            {
                if (_keyBlueTamClipCache != null)
                    return;

                var allAudioClips = Resources.LoadAll<AudioClip>("");
                if (allAudioClips == null || allAudioClips.Length == 0)
                {
                    if (ModLog.IsVerbose)
                    {
                        MelonLogger.Msg("[AudioSourceHook] Resources에서 오디오 클립을 찾을 수 없습니다.");
                    }

                    return;
                }

                foreach (var clip in allAudioClips)
                {
                    if (clip != null && clip.name == "KeyBlue_Tam")
                    {
                        _keyBlueTamClipCache = clip;
                        if (ModLog.IsVerbose)
                        {
                            MelonLogger.Msg("[AudioSourceHook] KeyBlue_Tam 오디오 클립 캐싱 완료");
                        }

                        return;
                    }
                }

                if (ModLog.IsVerbose)
                {
                    MelonLogger.Msg("[AudioSourceHook] KeyBlue_Tam 오디오 클립을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AudioSourceHook] KeyBlue_Tam 캐싱 실패: {ex.Message}");
            }
        }
    }
}
