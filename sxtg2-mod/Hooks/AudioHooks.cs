using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Video;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Audio
{
    internal static class BgaFileResolver
    {
        public static string FindForAlbum(string albumFolder)
        {
            try
            {
                var albumFile = FindFirstMp4(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGAPlayerHook] 앨범 폴더에서 BGA 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] BGA 파일 검색 실패: {ex.Message}");
                return null;
            }
        }

        private static string FindFirstMp4(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            var files = Directory.GetFiles(folder, "*.mp4", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0] : null;
        }
    }

    internal static class BgmFileResolver
    {
        private static readonly string[] AudioPatterns = { "*.ogg", "*.mp3", "*.wav" };
        private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

        public static string FindForAlbum(string albumFolder)
        {
            try
            {
                var albumFile = FindNamedAudioFile(albumFolder, "music")
                    ?? FindFirstAudioFile(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 앨범 폴더에서 BGM 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 파일 검색 실패: {ex.Message}");
                return null;
            }
        }

        public static string FindPreviewForAlbum(string albumFolder)
        {
            return FindNamedAudioFile(albumFolder, "demo")
                ?? FindNamedAudioFile(albumFolder, "music")
                ?? FindFirstAudioFile(albumFolder);
        }

        private static string FindNamedAudioFile(string folder, string baseName)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;

            foreach (string extension in AudioExtensions)
            {
                string path = Path.Combine(folder, baseName + extension);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static string FindFirstAudioFile(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            foreach (var pattern in AudioPatterns)
            {
                var files = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            return null;
        }
    }

    public static class BGABGMSyncHook
    {
        private static float _lastSyncCheckTime = 0f;

        public static void CheckAndSync()
        {
            try
            {
                var videoPlayer = BGAPlayerHook.GetCurrentVideoPlayer();
                if (videoPlayer == null)
                    return;

                var bgmAudioSource = BGMPlayerHook.GetCurrentAudioSource();
                if (bgmAudioSource == null)
                    return;

                if (bgmAudioSource.isPlaying)
                {
                    if (!videoPlayer.isPlaying)
                    {
                        videoPlayer.time = bgmAudioSource.time;
                        videoPlayer.Play();
                        ModLog.Verbose($"[BGABGMSyncHook] BGM 재생 감지 -> BGA 재생 재개 (시점: {bgmAudioSource.time:F2}s)");
                    }

                    if (Time.time - _lastSyncCheckTime < 0.1f)
                        return;
                    _lastSyncCheckTime = Time.time;

                    var bgaTime = (float)videoPlayer.time;
                    var bgmTime = bgmAudioSource.time;

                    if (bgaTime <= 0 || bgmTime <= 0) return;

                    var timeDifference = bgaTime - bgmTime;
                    var absDifference = Mathf.Abs(timeDifference);

                    if (absDifference > 0.5f)
                    {
                        videoPlayer.time = bgmTime;
                        if (videoPlayer.canSetPlaybackSpeed) videoPlayer.playbackSpeed = 1.0f;
                        ModLog.Verbose($"[BGABGMSyncHook] 하드 재동기화: BGA {bgaTime:F3} -> BGM {bgmTime:F3} (차이 {timeDifference:F3})");
                    }
                    else
                    {
                        bool isAdjusting = Mathf.Abs(videoPlayer.playbackSpeed - 1.0f) > 0.001f;
                        float threshold = isAdjusting ? 0.01f : 0.05f;

                        if (absDifference > threshold)
                        {
                            if (videoPlayer.canSetPlaybackSpeed)
                            {
                                float adjustmentFactor = (absDifference > 0.1f) ? 0.05f : 0.02f;
                                float targetSpeed = (timeDifference > 0) ? (1.0f - adjustmentFactor) : (1.0f + adjustmentFactor);

                                if (Mathf.Abs(videoPlayer.playbackSpeed - targetSpeed) > 0.001f)
                                {
                                    videoPlayer.playbackSpeed = targetSpeed;
                                    ModLog.Verbose($"[BGABGMSyncHook] 소프트 동기화: 속도 {targetSpeed:F3} (차이 {timeDifference:F4})");
                                }
                            }
                        }
                        else
                        {
                            if (videoPlayer.canSetPlaybackSpeed && Mathf.Abs(videoPlayer.playbackSpeed - 1.0f) > 0.001f)
                            {
                                videoPlayer.playbackSpeed = 1.0f;
                                ModLog.Verbose($"[BGABGMSyncHook] 동기화 안정: 속도 1.0 복귀");
                            }
                        }
                    }
                }
                else
                {
                    if (videoPlayer.isPlaying)
                    {
                        videoPlayer.Pause();
                        ModLog.Verbose("[BGABGMSyncHook] BGM 일시정지 감지 -> BGA 일시정지");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGABGMSyncHook] 동기화 체크 실패: {ex.Message}");
            }
        }
    }

    public static class BGAPlayerHook
    {
        private static bool _isReplaced = false;
        private static string _bgaFilePath = null;
        private static VideoPlayer _currentVideoPlayer = null;

        public static void ResetReplacementFlag()
        {
            _isReplaced = false;
            _currentVideoPlayer = null;
        }

        public static VideoPlayer GetCurrentVideoPlayer()
        {
            return _currentVideoPlayer;
        }

        public static bool ReplacePlaySceneBGA(VideoPlayer videoPlayer, string customAlbumFolder)
        {
            if (_isReplaced)
            {
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(customAlbumFolder))
                {
                    MelonLogger.Msg("[BGAPlayerHook] 커스텀 앨범 폴더가 없어 BGA 교체를 건너뜁니다.");
                    return false;
                }

                MelonLogger.Msg($"[BGAPlayerHook] ReplacePlaySceneBGA 호출: albumFolder={customAlbumFolder}");

                string bgaFile = BgaFileResolver.FindForAlbum(customAlbumFolder);

                if (string.IsNullOrEmpty(bgaFile))
                {
                    return false;
                }

                if (videoPlayer == null)
                {
                    MelonLogger.Warning("[BGAPlayerHook] 대상 VideoPlayer가 없습니다.");
                    return false;
                }

                return LoadRegularBGA(videoPlayer, bgaFile);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] BGA 교체 실패: {ex.Message}");
                return false;
            }
        }

        private static bool LoadRegularBGA(VideoPlayer videoPlayer, string bgaFilePath)
        {
            try
            {
                var videoUrl = "file://" + bgaFilePath.Replace("\\", "/");

                if (videoPlayer.isPlaying)
                {
                    videoPlayer.Stop();
                }

                videoPlayer.url = videoUrl;
                videoPlayer.source = VideoSource.Url;
                videoPlayer.clip = null;

                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
                videoPlayer.skipOnDrop = true;

                try
                {
                    videoPlayer.Prepare();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BGAPlayerHook] VideoPlayer.Prepare 실패 (계속 진행): {ex.Message}");
                }

                _currentVideoPlayer = videoPlayer;
                _bgaFilePath = bgaFilePath;
                _isReplaced = true;
                MelonLogger.Msg($"[BGAPlayerHook] BGA 교체 완료: {Path.GetFileName(_bgaFilePath)}");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BGAPlayerHook] 일반 BGA 로드 실패: {ex.Message}");
                return false;
            }
        }
    }

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

        public static void ReplacePlaySceneBGM(AudioSource target, string albumFolder)
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

        private static IEnumerator LoadAudio(AudioSource target, string filePath, int version)
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
                MelonLogger.Msg($"[BGMPlayerHook] BGM 교체 완료: {Path.GetFileName(filePath)}");
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
                        Path.GetExtension(filePath).Equals(".wav", StringComparison.OrdinalIgnoreCase) ||
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
