using System;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Audio
{
    public static partial class BGMPlayerHook
    {
        private static bool _isInitialized = false;
        private static bool _isReplaced = false;
        private static bool _isLoading = false; // 로딩 중 플래그
        private static string _bgmFilePath = null;
        private static AudioSource _currentAudioSource = null;
        private static AudioSource _managerPlayBGM = null;
        private static CoroutineRunner _coroutineRunner = null;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BGMPlayerHook] 초기화 실패: {ex.Message}");
            }
        }

        public static void ResetReplacementFlag()
        {
            _isReplaced = false;
            _isLoading = false;
            _currentAudioSource = null;
        }

        /// <summary>
        /// BGM이 교체되었는지 확인합니다.
        /// </summary>
        public static bool IsReplaced()
        {
            return _isReplaced;
        }

        /// <summary>
        /// BGM이 로딩 중인지 확인합니다.
        /// </summary>
        public static bool IsLoading()
        {
            return _isLoading;
        }

        public static void SetManagerPlayBGM(AudioSource bgmAudioSource)
        {
            _managerPlayBGM = bgmAudioSource;
        }

        public static void ReplacePlaySceneBGM(string customAlbumFolder = null)
        {
            // 이미 교체되었거나 로딩 중이면 무시
            if (_isReplaced || _isLoading)
            {
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(customAlbumFolder))
                {
                    MelonLogger.Msg("[BGMPlayerHook] 커스텀 앨범 폴더가 없어 BGM 교체를 건너뜁니다.");
                    return;
                }

                MelonLogger.Msg($"[BGMPlayerHook] ReplacePlaySceneBGM 호출: albumFolder={customAlbumFolder}");

                string bgmFile = BgmFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false);
                if (string.IsNullOrEmpty(bgmFile))
                {
                    MelonLogger.Warning("[BGMPlayerHook] BGM 파일을 찾을 수 없습니다.");
                    return;
                }
                
                MelonLogger.Msg($"[BGMPlayerHook] BGM 파일 발견: {bgmFile}");

                AudioSource targetAudioSource = BgmAudioSourceFinder.Find(_managerPlayBGM);
                if (targetAudioSource == null)
                {
                    MelonLogger.Warning("[BGMPlayerHook] AudioSource를 찾을 수 없습니다.");
                    return;
                }

                _currentAudioSource = targetAudioSource;
                _bgmFilePath = bgmFile; // 업데이트

                // 로딩 시작 플래그 설정
                _isLoading = true;
                
                GetOrCreateCoroutineRunner().StartCoroutine(LoadAndReplaceBGM(targetAudioSource));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 교체 실패: {ex.Message}");
            }
        }

        private static CoroutineRunner GetOrCreateCoroutineRunner()
        {
            if (_coroutineRunner != null)
            {
                return _coroutineRunner;
            }

            var coroutineRunnerObj = new GameObject("BGMPlayerHook_CoroutineRunner");
            _coroutineRunner = coroutineRunnerObj.AddComponent<CoroutineRunner>();
            UnityEngine.Object.DontDestroyOnLoad(coroutineRunnerObj);
            return _coroutineRunner;
        }

        /// <summary>
        /// 현재 AudioSource 인스턴스를 반환합니다.
        /// </summary>
        public static AudioSource GetCurrentAudioSource()
        {
            return _currentAudioSource;
        }

        // MonoBehaviour 헬퍼 클래스 (코루틴 실행용)
        private class CoroutineRunner : UnityEngine.MonoBehaviour { }
    }
}
