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
    internal static class MediaUrl
    {
        /// <summary>
        /// 로컬 파일 경로를 UnityWebRequest/VideoPlayer용 file:// URL로 바꾼다. URL에서 특별한 뜻을 갖는 `%`, `#`, `?`만
        /// 이스케이프하고 나머지(한글/일본어/공백/작은따옴표 등)는 지금까지와 똑같이 둔다. 예전에는 폴더 이름에 `#`나 `%`가 있으면
        /// 경로가 잘려 BGM/BGA/미리듣기를 못 읽었다.
        /// </summary>
        public static string FromPath(string filePath)
        {
            string path = filePath.Replace('\\', '/');
            path = path.Replace("%", "%25").Replace("#", "%23").Replace("?", "%3F");
            return "file://" + path;
        }
    }

    internal static class BgaFileResolver
    {
        public static string FindForAlbum(string albumFolder)
        {
            try
            {
                var albumFile = FindFirstMp4(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    ModLog.Msg($"[BGAPlayerHook] 앨범 폴더에서 BGA 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[BGAPlayerHook] BGA 파일 검색 실패: {ex.Message}");
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
            if (files.Length == 0)
                return null;

            // Directory.GetFiles의 순서는 보장되지 않으므로 이름순으로 정해 항상 같은 파일이 고르게 한다.
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            return files[0];
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
                var albumFile = FindNamedAudioFile(albumFolder, "music");
                if (string.IsNullOrEmpty(albumFile))
                {
                    albumFile = FindLargestAudioFile(albumFolder);
                    if (!string.IsNullOrEmpty(albumFile))
                    {
                        ModLog.Warning(
                            $"[BGMPlayerHook] music.ogg/mp3/wav가 없어 폴더에서 가장 큰 오디오 파일을 BGM으로 씁니다: {Path.GetFileName(albumFile)} " +
                            "(곡 음원은 music.ogg 같은 이름으로 두는 것을 권장합니다)");
                    }
                }

                if (!string.IsNullOrEmpty(albumFile))
                {
                    ModLog.Msg($"[BGMPlayerHook] 앨범 폴더에서 BGM 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[BGMPlayerHook] BGM 파일 검색 실패: {ex.Message}");
                return null;
            }
        }

        public static string FindPreviewForAlbum(string albumFolder)
        {
            return FindNamedAudioFile(albumFolder, "demo")
                ?? FindNamedAudioFile(albumFolder, "music")
                ?? FindLargestAudioFile(albumFolder);
        }

        public static AudioType GetAudioType(string extension)
        {
            switch (extension?.ToLowerInvariant())
            {
                case ".ogg": return AudioType.OGGVORBIS;
                case ".mp3": return AudioType.MPEG;
                case ".wav": return AudioType.WAV;
                default: return AudioType.UNKNOWN;
            }
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

        /// <summary>
        /// music.*가 없을 때의 폴백. 예전에는 폴더의 첫 .ogg(없으면 .mp3, .wav)를 골라서, 키음이 많은 폴더에서는
        /// 키음 하나가 BGM이 됐다. 곡 음원은 키음보다 훨씬 크므로 가장 큰 오디오 파일을 고른다.
        /// </summary>
        private static string FindLargestAudioFile(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            string best = null;
            long bestSize = -1;
            foreach (var pattern in AudioPatterns)
            {
                foreach (string file in Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly))
                {
                    long size = new FileInfo(file).Length;
                    if (size > bestSize)
                    {
                        best = file;
                        bestSize = size;
                    }
                }
            }

            return best;
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

                if (!bgmAudioSource.isPlaying)
                {
                    PauseVideoWhileBgmPaused(videoPlayer);
                    return;
                }

                float bgmTime = bgmAudioSource.time;

                // 영상이 곡보다 짧으면 영상이 끝난 뒤에는 건드리지 않는다. 예전에는 "BGM은 재생 중인데 영상은 멈춤"으로 보고
                // 매 프레임 time을 곡 위치로 옮기고 Play()를 다시 불렀다(끝난 영상을 계속 재시작하려 듦).
                if (IsVideoFinished(videoPlayer, bgmTime))
                    return;

                if (!videoPlayer.isPlaying)
                {
                    videoPlayer.time = bgmTime;
                    videoPlayer.Play();
                    ModLog.Verbose($"[BGABGMSyncHook] BGM 재생 감지 -> BGA 재생 재개 (시점: {bgmTime:F2}s)");
                }

                if (Time.time - _lastSyncCheckTime < 0.1f)
                    return;
                _lastSyncCheckTime = Time.time;

                AdjustVideoToBgm(videoPlayer, bgmTime);
            }
            catch (Exception ex)
            {
                ModLog.WarningThrottled("BGABGMSyncHook.Check", $"[BGABGMSyncHook] 동기화 체크 실패: {ex.Message}");
            }
        }

        /// <summary>영상 길이를 알 수 있고(준비 완료), BGM 위치가 이미 영상 끝을 넘었는지.</summary>
        private static bool IsVideoFinished(VideoPlayer videoPlayer, float bgmTime)
        {
            return videoPlayer.isPrepared && videoPlayer.length > 0.0 && bgmTime >= videoPlayer.length;
        }

        private static void PauseVideoWhileBgmPaused(VideoPlayer videoPlayer)
        {
            if (!videoPlayer.isPlaying)
                return;

            videoPlayer.Pause();
            ModLog.Verbose("[BGABGMSyncHook] BGM 일시정지 감지 -> BGA 일시정지");
        }

        /// <summary>BGA와 BGM의 시간 차이를 보고 하드 싱크(위치 이동) 또는 소프트 싱크(재생 속도 ±2~5%)를 한다.</summary>
        private static void AdjustVideoToBgm(VideoPlayer videoPlayer, float bgmTime)
        {
            float bgaTime = (float)videoPlayer.time;
            if (bgaTime <= 0 || bgmTime <= 0)
                return;

            float timeDifference = bgaTime - bgmTime;
            float absDifference = Mathf.Abs(timeDifference);

            if (absDifference > 0.5f)
            {
                videoPlayer.time = bgmTime;
                if (videoPlayer.canSetPlaybackSpeed) videoPlayer.playbackSpeed = 1.0f;
                ModLog.Verbose($"[BGABGMSyncHook] 하드 재동기화: BGA {bgaTime:F3} -> BGM {bgmTime:F3} (차이 {timeDifference:F3})");
                return;
            }

            if (!videoPlayer.canSetPlaybackSpeed)
                return;

            bool isAdjusting = Mathf.Abs(videoPlayer.playbackSpeed - 1.0f) > 0.001f;
            float threshold = isAdjusting ? 0.01f : 0.05f;

            if (absDifference > threshold)
            {
                float adjustmentFactor = (absDifference > 0.1f) ? 0.05f : 0.02f;
                float targetSpeed = (timeDifference > 0) ? (1.0f - adjustmentFactor) : (1.0f + adjustmentFactor);

                if (Mathf.Abs(videoPlayer.playbackSpeed - targetSpeed) > 0.001f)
                {
                    videoPlayer.playbackSpeed = targetSpeed;
                    ModLog.Verbose($"[BGABGMSyncHook] 소프트 동기화: 속도 {targetSpeed:F3} (차이 {timeDifference:F4})");
                }
            }
            else if (isAdjusting)
            {
                videoPlayer.playbackSpeed = 1.0f;
                ModLog.Verbose("[BGABGMSyncHook] 동기화 안정: 속도 1.0 복귀");
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
                    ModLog.Msg("[BGAPlayerHook] 커스텀 앨범 폴더가 없어 BGA 교체를 건너뜁니다.");
                    return false;
                }

                ModLog.Msg($"[BGAPlayerHook] ReplacePlaySceneBGA 호출: albumFolder={customAlbumFolder}");

                string bgaFile = BgaFileResolver.FindForAlbum(customAlbumFolder);

                if (string.IsNullOrEmpty(bgaFile))
                {
                    return false;
                }

                if (videoPlayer == null)
                {
                    ModLog.Warning("[BGAPlayerHook] 대상 VideoPlayer가 없습니다.");
                    return false;
                }

                return LoadRegularBGA(videoPlayer, bgaFile);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[BGAPlayerHook] BGA 교체 실패: {ex.Message}");
                return false;
            }
        }

        private static bool LoadRegularBGA(VideoPlayer videoPlayer, string bgaFilePath)
        {
            try
            {
                var videoUrl = MediaUrl.FromPath(bgaFilePath);

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
                    ModLog.Warning($"[BGAPlayerHook] VideoPlayer.Prepare 실패 (계속 진행): {ex.Message}");
                }

                _currentVideoPlayer = videoPlayer;
                _bgaFilePath = bgaFilePath;
                _isReplaced = true;
                ModLog.Msg($"[BGAPlayerHook] BGA 교체 완료: {Path.GetFileName(_bgaFilePath)}");
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
                ModLog.Warning("[BGMPlayerHook] BGM 대상 또는 파일을 찾을 수 없습니다.");
                return;
            }

            int version = ++_requestVersion;
            _isLoading = true;
            _currentAudioSource = target;
            GetRunner().StartCoroutine(LoadAudio(target, filePath, version));
        }

        private static IEnumerator LoadAudio(AudioSource target, string filePath, int version)
        {
            string url = MediaUrl.FromPath(filePath);
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
                    ModLog.Warning($"[BGMPlayerHook] BGM 로드 실패: {request.error}");
                    yield break;
                }

                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null || target == null)
                    yield break;

                if (target.isPlaying)
                    target.Stop();

                target.clip = clip;
                target.loop = false;
                ModLog.Msg($"[BGMPlayerHook] BGM 교체 완료: {Path.GetFileName(filePath)}");
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
                AudioType audioType = BgmFileResolver.GetAudioType(Path.GetExtension(filePath));
                var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType);
                if (request.downloadHandler is DownloadHandlerAudioClip handler)
                {
                    // 5MB를 넘는 파일만 스트리밍한다. 예전에는 .wav도 항상 스트리밍했는데, 게임은 곡 종료를 bgm.clip.length로
                    // 판단하므로 길이가 확실한 통째 로드가 안전하다(.wav도 압축 해제된 PCM이라 크지만 한 곡 분량이다).
                    handler.streamAudio = new FileInfo(filePath).Length > 5 * 1024 * 1024;
                }
                return request;
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[BGMPlayerHook] BGM 요청 생성 실패: {ex.Message}");
                return null;
            }
        }

        private static void FinishLoading(int version)
        {
            if (version == _requestVersion)
                _isLoading = false;
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
