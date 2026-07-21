using MelonLoader;
using System.Collections;
using System.IO;
using System;
using UnityEngine.Networking;
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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static IEnumerator LoadAndReplaceBGM(AudioSource audioSource)
        {
            if (!TryBuildBgmWebRequest(out UnityWebRequest www, out string buildError))
            {
                MelonLogger.Warning($"[BGMPlayerHook] {buildError}");
                _isLoading = false;
                yield break;
            }

            yield return www.SendWebRequest();

            try
            {
                ApplyBgmWebRequestResult(www, audioSource);
            }
            finally
            {
                www?.Dispose();
            }
        }

        private static bool TryBuildBgmWebRequest(out UnityWebRequest www, out string errorMessage)
        {
            www = null;
            errorMessage = null;

            var fileUrl = "file://" + _bgmFilePath.Replace("\\", "/");
            var extension = Path.GetExtension(_bgmFilePath).ToLower();
            var audioType = MapExtensionToAudioType(extension);
            bool useStreaming = ShouldUseStreamingForBgm(extension);

            try
            {
                www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, audioType);
                var handler = new DownloadHandlerAudioClip(fileUrl, audioType);
                handler.streamAudio = useStreaming;
                www.downloadHandler = handler;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"BGM 요청 생성 실패: {ex.Message}";
                return false;
            }
        }

        private static AudioType MapExtensionToAudioType(string extension)
        {
            if (extension == ".ogg") return AudioType.OGGVORBIS;
            if (extension == ".mp3") return AudioType.MPEG;
            if (extension == ".wav") return AudioType.WAV;
            return AudioType.UNKNOWN;
        }

        private static bool ShouldUseStreamingForBgm(string extension)
        {
            if (extension == ".wav")
                return true;

            try
            {
                var fileInfo = new FileInfo(_bgmFilePath);
                if (fileInfo.Length > 5 * 1024 * 1024)
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 대용량 파일 감지({fileInfo.Length / 1024 / 1024}MB): 스트리밍 모드 사용");
                    return true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[BGMPlayerHook] 파일 크기 확인 실패(스트리밍 기본값 사용): {ex.Message}");
            }

            return false;
        }

        private static void ApplyBgmWebRequestResult(UnityWebRequest www, AudioSource audioSource)
        {
            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[BGMPlayerHook] BGM 로드 실패: {www.error}");
                    _isLoading = false;
                    return;
                }

                var handler = www.downloadHandler as DownloadHandlerAudioClip;
                if (handler == null)
                {
                    MelonLogger.Warning("[BGMPlayerHook] DownloadHandler가 올바른 타입이 아닙니다.");
                    return;
                }

                var audioClip = handler.audioClip;
                if (audioClip == null)
                {
                    MelonLogger.Warning("[BGMPlayerHook] AudioClip이 null입니다.");
                    return;
                }

                if (audioSource.isPlaying)
                    audioSource.Stop();

                audioSource.clip = audioClip;
                audioSource.loop = true;
                audioSource.Play();

                _isReplaced = true;
                _isLoading = false;
                MelonLogger.Msg($"[BGMPlayerHook] BGM 교체 완료: {Path.GetFileName(_bgmFilePath)}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 처리 중 오류: {ex.Message}");
                _isLoading = false;
            }
        }
    
    }
}
