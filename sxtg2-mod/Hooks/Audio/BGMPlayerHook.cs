using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace sxtg2.Hooks.Audio
{
    public static class BGMPlayerHook
    {
        private static bool _isLoading;
        private static int _requestVersion;
        private static AudioSource _currentAudioSource;
        private static CoroutineRunner _coroutineRunner;

        public static bool IsLoading()
        {
            return _isLoading;
        }

        public static AudioSource GetCurrentAudioSource()
        {
            return _currentAudioSource;
        }

        public static void ResetReplacementFlag()
        {
            _requestVersion++;
            _isLoading = false;
            _currentAudioSource = null;
        }

        public static void ReplacePlaySceneBGM(
            AudioSource target,
            string albumFolder)
        {
            string filePath = BgmFileResolver.FindForAlbum(albumFolder);
            if (target == null || string.IsNullOrEmpty(filePath))
            {
                MelonLogger.Warning("[BGMPlayerHook] BGM 대상 또는 파일을 찾을 수 없습니다.");
                return;
            }

            int version = ++_requestVersion;
            _isLoading = true;
            _currentAudioSource = target;
            GetRunner().StartCoroutine(LoadAudio(target, filePath, version));
        }

        private static IEnumerator LoadAudio(
            AudioSource target,
            string filePath,
            int version)
        {
            string url = "file://" + filePath.Replace("\\", "/");
            var request = CreateRequest(url, filePath);
            if (request == null)
            {
                FinishLoading(version);
                yield break;
            }

            yield return request.SendWebRequest();

            try
            {
                if (version != _requestVersion)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[BGMPlayerHook] BGM 로드 실패: {request.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || target == null)
                    yield break;

                if (target.isPlaying)
                    target.Stop();

                target.clip = clip;
                target.loop = false;
                MelonLogger.Msg(
                    $"[BGMPlayerHook] BGM 교체 완료: {Path.GetFileName(filePath)}");
            }
            finally
            {
                request.Dispose();
                FinishLoading(version);
            }
        }

        private static UnityWebRequest CreateRequest(string url, string filePath)
        {
            try
            {
                AudioType audioType = GetAudioType(Path.GetExtension(filePath));
                var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
                if (request.downloadHandler is DownloadHandlerAudioClip handler)
                {
                    handler.streamAudio =
                        Path.GetExtension(filePath).Equals(
                            ".wav",
                            StringComparison.OrdinalIgnoreCase) ||
                        new FileInfo(filePath).Length > 5 * 1024 * 1024;
                }
                return request;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 요청 생성 실패: {ex.Message}");
                return null;
            }
        }

        private static void FinishLoading(int version)
        {
            if (version == _requestVersion)
                _isLoading = false;
        }

        private static AudioType GetAudioType(string extension)
        {
            switch (extension?.ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                case ".wav": return AudioType.WAV;
                default: return AudioType.UNKNOWN;
            }
        }

        private static CoroutineRunner GetRunner()
        {
            if (_coroutineRunner != null)
                return _coroutineRunner;

            var runnerObject = new GameObject("sxtg2 BGM Loader");
            _coroutineRunner = runnerObject.AddComponent<CoroutineRunner>();
            UnityEngine.Object.DontDestroyOnLoad(runnerObject);
            return _coroutineRunner;
        }

        private sealed class CoroutineRunner : MonoBehaviour
        {
        }
    }
}
