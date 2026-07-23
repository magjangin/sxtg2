using HarmonyLib;
using MelonLoader;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using System;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine;
using RhythmGame.MusicSelect;
using sxtg2.Helpers.Track;
using sxtg2.Helpers;
using sxtg2.Loaders;

namespace sxtg2.Hooks.Manager
{
    [HarmonyPatch(typeof(ManagerMusicSelect))]
    public static partial class ManagerMusicSelectHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            MelonLogger.Msg("[ManagerMusicSelectHook] Initialize() - 자동 HarmonyPatch 적용 상태");
            _isInitialized = true;
        }

        [HarmonyPatch("ChangeTrackCursor")]
        [HarmonyPostfix]
        private static void ChangeTrackCursorPostfix(ManagerMusicSelect __instance, int delta)
        {
            try
            {
                InjectThumbnailAndDemo(__instance);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] ChangeTrackCursorPostfix 오류: {ex.Message}");
            }
        }


        /// <summary>
        /// 썸네일 및 demo.ogg를 주입합니다.
        /// </summary>
        private static void InjectThumbnailAndDemo(ManagerMusicSelect managerInstance)
        {
            try
            {
                if (managerInstance == null || managerInstance.currentSelectedTrack == null)
                    return;

                TrackData trackData = managerInstance.currentSelectedTrack;
                string trackId = trackData.ID;
                string displayName = trackData.DisplayName;

                // 커스텀 트랙 확인
                bool isCustomTrack = CustomTrackHelper.IsCustomTrack(trackData);

                if (!isCustomTrack)
                {
                    CustomTrackHelper.ClearSelectedTrack();
                    return;
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 커스텀 트랙 감지: ID={trackId}, DisplayName={displayName}");

                // 앨범 폴더 찾기 (DisplayName 기반)
                string albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);

                // 선택된 트랙 정보 캐싱 (PlayLoading, Result 화면에서 사용)
                CustomTrackHelper.SetSelectedTrack(trackId, displayName, albumFolder);

                // 썸네일 주입 (앨범 폴더 포함)
                LoadCustomThumbnail(trackId, albumFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] InjectThumbnailAndDemo 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// GameObject의 전체 경로를 반환합니다.
        /// </summary>
        private static string GetGameObjectPath(UnityEngine.GameObject obj)
        {
            try
            {
                if (obj == null)
                    return "null";
                
                string path = obj.name;
                UnityEngine.Transform current = obj.transform.parent;
                
                while (current != null)
                {
                    path = current.name + "/" + path;
                    current = current.parent;
                }
                
                return path;
            }
            catch
            {
                return obj != null ? obj.name : "unknown";
            }
        }

        public static string ResolveAlbumFolderForTrack(string displayName, string trackId)
        {
            return FindAlbumFolderByDisplayName(displayName, trackId);
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static readonly Dictionary<string, string> AlbumFolderByDisplayNameCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> AlbumFolderByTrackIdCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] BmsExtensions = { "*.bms", "*.bme", "*.bml" };

        /// <summary>
        /// DisplayName을 기반으로 앨범 폴더를 찾습니다.
        /// </summary>
        private static string FindAlbumFolderByDisplayName(string displayName, string trackId)
        {
            try
            {
                string gamePath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                if (!Directory.Exists(hwaFolder))
                {
                    return hwaFolder; // 기본값 반환
                }

                if (TryGetCachedAlbumFolder(hwaFolder, displayName, trackId, out var cachedFolder))
                {
                    return cachedFolder;
                }

                // 1. 모든 앨범 폴더에서 txt 파일 검색하여 DisplayName과 매칭
                string albumFolder = FindAlbumByDisplayName(hwaFolder, displayName);
                if (albumFolder != null)
                {
                    CacheAlbumFolder(displayName, trackId, albumFolder);
                    return albumFolder;
                }

                // 2. Track ID 기반으로 찾기
                if (!string.IsNullOrEmpty(trackId))
                {
                    albumFolder = FindAlbumByTrackId(hwaFolder, trackId);
                    if (albumFolder != null)
                    {
                        CacheAlbumFolder(displayName, trackId, albumFolder);
                        return albumFolder;
                    }
                }

                // 3. 기본 hwa 폴더 반환
                return hwaFolder;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 앨범 폴더 찾기 실패: {ex.Message}");
                string gamePath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                return Path.Combine(gamePath, "hwa");
            }
        }

        private static string FindAlbumByDisplayName(string hwaFolder, string displayName)
        {
            foreach (var albumFolder in Directory.EnumerateDirectories(hwaFolder))
            {
                foreach (var txtFile in Directory.EnumerateFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, Path.GetFileNameWithoutExtension(txtFile));
                        if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title == displayName)
                        {
                            MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 발견 (DisplayName 매칭): {Path.GetFileName(albumFolder)}");
                            return albumFolder;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ManagerMusicSelectHook] 트랙 정보 파싱 실패 ({txtFile}): {ex.Message}");
                    }
                }
            }
            return null;
        }

        private static string FindAlbumByTrackId(string hwaFolder, string trackId)
        {
            foreach (var albumFolder in Directory.EnumerateDirectories(hwaFolder))
            {
                foreach (var extension in BmsExtensions)
                {
                    string pattern = $"{trackId}{extension.Substring(1)}";
                    foreach (var _ in Directory.EnumerateFiles(albumFolder, pattern, SearchOption.TopDirectoryOnly))
                    {
                        MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 발견 (Track ID 매칭): {Path.GetFileName(albumFolder)}");
                        return albumFolder;
                    }
                }
            }
            return null;
        }

        private static bool TryGetCachedAlbumFolder(string hwaFolder, string displayName, string trackId, out string albumFolder)
        {
            albumFolder = null;

            if (!string.IsNullOrEmpty(trackId) &&
                AlbumFolderByTrackIdCache.TryGetValue(trackId, out var byTrackId) &&
                IsValidCachedAlbumFolder(hwaFolder, byTrackId, displayName, trackId))
            {
                albumFolder = byTrackId;
                return true;
            }

            if (!string.IsNullOrEmpty(displayName) &&
                AlbumFolderByDisplayNameCache.TryGetValue(displayName, out var byDisplayName) &&
                IsValidCachedAlbumFolder(hwaFolder, byDisplayName, displayName, trackId))
            {
                albumFolder = byDisplayName;
                return true;
            }

            return false;
        }

        private static bool IsValidCachedAlbumFolder(string hwaFolder, string albumFolder, string displayName, string trackId)
        {
            if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
                return false;

            if (!albumFolder.StartsWith(hwaFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(trackId))
            {
                foreach (var extension in BmsExtensions)
                {
                    string pattern = $"{trackId}{extension.Substring(1)}";
                    foreach (var _ in Directory.EnumerateFiles(albumFolder, pattern, SearchOption.TopDirectoryOnly))
                        return true;
                }
                return false;
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                return FolderHasDisplayName(albumFolder, displayName);
            }

            return true;
        }

        private static bool FolderHasDisplayName(string albumFolder, string displayName)
        {
            foreach (var txtFile in Directory.EnumerateFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, Path.GetFileNameWithoutExtension(txtFile));
                    if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title == displayName)
                        return true;
                }
                catch
                {
                    // 캐시 검증 실패는 무효로 취급하고 재탐색한다.
                }
            }

            return false;
        }

        private static void CacheAlbumFolder(string displayName, string trackId, string albumFolder)
        {
            if (string.IsNullOrEmpty(albumFolder))
                return;

            if (!string.IsNullOrEmpty(displayName))
                AlbumFolderByDisplayNameCache[displayName] = albumFolder;

            if (!string.IsNullOrEmpty(trackId))
                AlbumFolderByTrackIdCache[trackId] = albumFolder;
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================


    


    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static MusicLoaderCoroutineRunner _musicLoaderRunner;
        private static int _previewRequestVersion;
        private static string _lastPreviewTrackId;
        private static string _lastPreviewMusicFile;
        private static AudioSource _lastPreviewAudioSource;

        /// <summary>
        /// PlayPreview 메서드 prefix 후킹.
        /// 커스텀 트랙인 경우 원래 preview 재생을 막고 커스텀 음악을 재생합니다.
        /// </summary>
        [HarmonyPatch("PlayPreview")]
        [HarmonyPrefix]
        private static bool PlayPreviewPrefix(ManagerMusicSelect __instance, TrackData t)
        {
            try
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] PlayPreview 호출 감지!");
                
                // TrackData가 null이면 원래 동작 수행
                if (t == null)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] PlayPreview: TrackData가 null입니다.");
                    return true;
                }

                string trackId = t.ID;
                string displayName = t.DisplayName;

                MelonLogger.Msg($"[ManagerMusicSelectHook] PlayPreview: Track ID={trackId}, DisplayName={displayName}");

                // 커스텀 트랙인지 확인
                bool isCustomTrack = CustomTrackHelper.IsCustomTrack(t);
                if (isCustomTrack)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 커스텀 트랙의 원래 preview 재생 차단 - 커스텀 음악 재생 시도");
                    
                    // BGM 뮤트하고 demo/music.ogg 재생
                    PlayCustomMusic(__instance, trackId, displayName);
                    
                    return false; // 원래 메서드 실행 방지
                }

                // 일반 트랙이면 원래 동작 수행
                CustomTrackHelper.ClearSelectedTrack();
                MelonLogger.Msg("[ManagerMusicSelectHook] 일반 트랙 - 원래 preview 재생 허용");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] PlayPreviewPrefix 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
                // 오류 발생 시 원래 동작 수행
                return true;
            }
        }

        /// <summary>
        /// 커스텀 트랙의 demo/music 파일을 재생합니다 (강화된 로직).
        /// demo.ogg가 있으면 우선 재생, 없으면 music.ogg를 재생합니다.
        /// </summary>
        private static void PlayCustomMusic(object managerInstance, string trackId, string displayName)
        {
            AudioSource bgmSource = null;
            float originalVolume = 1f;
            
            try
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] === PlayCustomMusic 시작 ===");
                
                // 강화된 BGM 소스 찾기
                bgmSource = FindBgmSourceForPreviewStrong(managerInstance);
                if (bgmSource == null)
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] BGM 소스를 찾을 수 없습니다. 모든 AudioSource 뮤트 시도");
                    // 모든 AudioSource를 찾아서 뮤트
                    MuteAllAudioSources();
                    bgmSource = UnityEngine.Object.FindObjectOfType<AudioSource>();
                    if (bgmSource == null)
                    {
                        MelonLogger.Error("[ManagerMusicSelectHook] AudioSource를 전혀 찾을 수 없습니다!");
                        return;
                    }
                }

                // 원래 볼륨 저장
                originalVolume = bgmSource.volume;
                MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견: volume={originalVolume}, isPlaying={bgmSource.isPlaying}, clip={bgmSource.clip?.name ?? "null"}");

                // 앨범 폴더 찾기
                string albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);
                if (!Directory.Exists(albumFolder))
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 앨범 폴더가 존재하지 않습니다: {albumFolder}");
                    bgmSource.volume = originalVolume;
                    return;
                }
                MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 확인: {Path.GetFileName(albumFolder)}");

                // demo/music 파일 찾기 (demo 우선)
                string musicFile = FindMusicFile(albumFolder);
                if (musicFile == null)
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] 음악 파일을 찾을 수 없습니다. 원래 BGM 볼륨 복원");
                    bgmSource.volume = originalVolume;
                    return;
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 음악 파일 발견: {Path.GetFileName(musicFile)} ({new FileInfo(musicFile).Length / 1024}KB)");

                // 동일한 트랙/파일이 이미 재생 중이면 불필요한 재로드를 건너뜀
                if (bgmSource == _lastPreviewAudioSource &&
                    bgmSource.isPlaying &&
                    string.Equals(_lastPreviewTrackId, trackId, StringComparison.Ordinal) &&
                    string.Equals(_lastPreviewMusicFile, musicFile, StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 동일한 preview 요청 감지 - 재로드 생략");
                    return;
                }

                // 모든 재생 중인 AudioSource 중지 및 뮤트
                StopAndMuteAllAudioSources(bgmSource);
                MelonLogger.Msg("[ManagerMusicSelectHook] 모든 BGM 뮤트 완료");

                // 코루틴으로 비동기 로드
                var runner = GetOrCreateMusicLoaderRunner();
                int requestVersion = ++_previewRequestVersion;
                _lastPreviewTrackId = trackId;
                _lastPreviewMusicFile = musicFile;
                _lastPreviewAudioSource = bgmSource;
                runner.StartCoroutine(LoadAndPlayMusicCoroutine(bgmSource, musicFile, originalVolume, requestVersion));
                
                MelonLogger.Msg("[ManagerMusicSelectHook] === PlayCustomMusic 코루틴 시작 ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] PlayCustomMusic 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                // 예외 발생 시 원래 볼륨 복원
                if (bgmSource != null)
                {
                    bgmSource.volume = originalVolume;
                }
            }
        }

        private static MusicLoaderCoroutineRunner GetOrCreateMusicLoaderRunner()
        {
            if (_musicLoaderRunner != null)
                return _musicLoaderRunner;

            var coroutineRunner = new GameObject("ManagerMusicSelectHook_MusicLoader");
            UnityEngine.Object.DontDestroyOnLoad(coroutineRunner);
            _musicLoaderRunner = coroutineRunner.AddComponent<MusicLoaderCoroutineRunner>();
            return _musicLoaderRunner;
        }

        // 코루틴 실행용 MonoBehaviour
        private class MusicLoaderCoroutineRunner : MonoBehaviour { }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static IEnumerator LoadAndPlayMusicCoroutine(AudioSource audioSource, string musicFile, float originalVolume, int requestVersion)
        {
            var fileUrl = "file://" + musicFile.Replace("\\", "/");
            var audioType = MapMusicFileExtensionToAudioType(Path.GetExtension(musicFile));
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, audioType);
            yield return www.SendWebRequest();

            AudioClip loadedClip = null;
            bool loadOk = false;
            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 로드 실패: {www.error}");
                }
                else if (TryGetAudioClipFromMusicDownload(www, out loadedClip))
                {
                    loadOk = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 처리 중 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            if (!loadOk)
            {
                www?.Dispose();
                audioSource.volume = originalVolume;
                yield break;
            }

            if (requestVersion != _previewRequestVersion)
            {
                www?.Dispose();
                yield break;
            }

            ApplyClipAndPlayPreviewMusic(audioSource, loadedClip, musicFile);
            www?.Dispose();
        }

        private static AudioType MapMusicFileExtensionToAudioType(string extension)
        {
            if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                return AudioType.MPEG;
            if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;
            return AudioType.OGGVORBIS;
        }

        private static bool TryGetAudioClipFromMusicDownload(UnityWebRequest www, out AudioClip audioClip)
        {
            audioClip = null;
            if (!(www.downloadHandler is DownloadHandlerAudioClip handler))
                return false;
            audioClip = handler.audioClip;
            return audioClip != null;
        }

        private static void ApplyClipAndPlayPreviewMusic(AudioSource audioSource, AudioClip audioClip, string musicFile)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            audioSource.clip = audioClip;
            audioSource.loop = true;
            audioSource.volume = 1f;
            audioSource.Play();

            MelonLogger.Msg($"[ManagerMusicSelectHook] 음악 재생 시작: {Path.GetFileName(musicFile)}");
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static readonly string[] PreviewAudioMemberNames =
        {
            "bgmSource", "bgm", "audioSource", "audio", "previewSource", "previewAudio"
        };

        private static readonly Dictionary<Type, MemberInfo> PreviewAudioMemberCache = new Dictionary<Type, MemberInfo>();

        private static AudioSource FindBgmSourceForPreviewStrong(object managerInstance)
        {
            Type managerType = managerInstance.GetType();

            if (PreviewAudioMemberCache.TryGetValue(managerType, out var cachedMember))
            {
                var cachedValue = ReadMemberValue(cachedMember, managerInstance);
                if (cachedValue is AudioSource cachedAudioSource && cachedAudioSource != null)
                {
                    return cachedAudioSource;
                }
            }

            foreach (var name in PreviewAudioMemberNames)
            {
                FieldInfo field = managerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(managerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        PreviewAudioMemberCache[managerType] = field;
                        MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견 (필드): {name}");
                        return audioSource;
                    }
                }

                PropertyInfo prop = managerType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    var value = prop.GetValue(managerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        PreviewAudioMemberCache[managerType] = prop;
                        MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견 (프로퍼티): {name}");
                        return audioSource;
                    }
                }
            }

            return FindFallbackPreviewAudioSource();
        }

        private static object ReadMemberValue(MemberInfo memberInfo, object instance)
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                return fieldInfo.GetValue(instance);
            }

            if (memberInfo is PropertyInfo propertyInfo && propertyInfo.CanRead)
            {
                return propertyInfo.GetValue(instance);
            }

            return null;
        }

        private static AudioSource FindFallbackPreviewAudioSource()
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            MelonLogger.Msg($"[ManagerMusicSelectHook] 전체 AudioSource 개수: {allAudioSources.Length}");

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && (audioSource.isPlaying || audioSource.clip != null))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] 재생 중인 AudioSource 발견: {audioSource.name}, clip={audioSource.clip?.name ?? "null"}");
                    return audioSource;
                }
            }

            if (allAudioSources.Length > 0 && allAudioSources[0] != null)
            {
                MelonLogger.Msg($"[ManagerMusicSelectHook] 첫 번째 AudioSource 사용: {allAudioSources[0].name}");
                return allAudioSources[0];
            }

            return null;
        }

        private static void StopAndMuteAllAudioSources(AudioSource primarySource)
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            int mutedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && audioSource != primarySource)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                        mutedCount++;
                    }

                    audioSource.volume = 0f;
                }
            }

            if (primarySource != null)
            {
                if (primarySource.isPlaying)
                {
                    primarySource.Stop();
                }

                primarySource.volume = 0f;
            }

            MelonLogger.Msg($"[ManagerMusicSelectHook] {mutedCount + (primarySource != null ? 1 : 0)}개의 AudioSource 뮤트 완료");
        }

        private static void MuteAllAudioSources()
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            int mutedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }

                    audioSource.volume = 0f;
                    mutedCount++;
                }
            }

            MelonLogger.Msg($"[ManagerMusicSelectHook] {mutedCount}개의 AudioSource 뮤트 완료 (전체 뮤트)");
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static string FindMusicFile(string albumFolder)
        {
            if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
            {
                return null;
            }

            var byName = TryFindMusicFileByKnownNames(albumFolder);
            if (byName != null)
            {
                return byName;
            }

            return TryFindMusicFileByAudioPrefixScan(albumFolder);
        }

        private static string TryFindMusicFileByKnownNames(string albumFolder)
        {
            string[] demoPatterns = { "demo.ogg", "demo.mp3", "demo.wav", "preview.ogg", "preview.mp3", "preview.wav" };
            foreach (var pattern in demoPatterns)
            {
                string file = Path.Combine(albumFolder, pattern);
                if (File.Exists(file))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] demo 파일 발견: {Path.GetFileName(file)}");
                    return file;
                }
            }

            string[] musicPatterns = { "music.ogg", "music.mp3", "music.wav" };
            foreach (var pattern in musicPatterns)
            {
                string file = Path.Combine(albumFolder, pattern);
                if (File.Exists(file))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] music 파일 발견: {Path.GetFileName(file)}");
                    return file;
                }
            }

            return null;
        }

        private static string TryFindMusicFileByAudioPrefixScan(string albumFolder)
        {
            try
            {
                var audioExtensions = new[] { "*.ogg", "*.mp3", "*.wav" };
                var demoOrPreview = TryFindFirstAudioMatchingPrefix(albumFolder, audioExtensions, new[] { "demo", "preview" }, "대체 demo");
                if (demoOrPreview != null)
                {
                    return demoOrPreview;
                }

                return TryFindFirstAudioMatchingPrefix(albumFolder, audioExtensions, new[] { "music" }, "대체 music");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 검색 중 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
                return null;
            }
        }

        private static string TryFindFirstAudioMatchingPrefix(string albumFolder, string[] audioExtensions, string[] namePrefixes, string logKind)
        {
            foreach (var ext in audioExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(albumFolder, ext, SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    foreach (var prefix in namePrefixes)
                    {
                        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            MelonLogger.Msg($"[ManagerMusicSelectHook] {logKind} 파일 발견: {Path.GetFileName(file)}");
                            return file;
                        }
                    }
                }
            }

            return null;
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        /// <summary>
        /// 커스텀 썸네일을 로드하고 적용합니다.
        /// </summary>
        private static void LoadCustomThumbnail(string trackId, string albumFolder = null)
        {
            try
            {
                if (string.IsNullOrEmpty(albumFolder))
                {
                    string gamePath = Path.GetDirectoryName(Application.dataPath);
                    albumFolder = Path.Combine(gamePath, "hwa");
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 썸네일 검색 시작: Track ID={trackId}, 앨범 폴더={Path.GetFileName(albumFolder)}");

                // ThumbnailLoader 사용 (앨범 폴더 우선)
                Sprite sprite = ThumbnailLoader.LoadThumbnail(trackId, albumFolder);
                
                if (sprite == null)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 썸네일을 찾을 수 없습니다.");
                    return;
                }
                
                MelonLogger.Msg($"[ManagerMusicSelectHook] 썸네일 로드 성공: {sprite.texture.width}x{sprite.texture.height}");

                // 풀사이즈 자켓 이미지 찾기
                var targetImages = FindFullsizeJacketImages();
                
                // 썸네일 적용
                if (targetImages.Count > 0)
                {
                    ApplyThumbnailToImages(targetImages, sprite);
                    MelonLogger.Msg($"[ManagerMusicSelectHook] {targetImages.Count}개의 풀사이즈 자켓 이미지에 썸네일 적용 완료");
                }
                else
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] 풀사이즈 자켓 이미지를 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 썸네일 로드 실패: {ex.Message}");
            }
        }

        private static List<Image> FindFullsizeJacketImages()
        {
            var targetImages = new List<Image>();
            
            try
            {
                Image[] allImages = UnityEngine.Object.FindObjectsOfType<Image>(true);
                
                foreach (var image in allImages)
                {
                    try
                    {
                        if (image == null || !image.gameObject.activeInHierarchy || !image.enabled)
                            continue;
                        
                        // "Jacket Image"라는 이름을 가진 이미지 중에서 경로에 "Fullsize Jacket"가 포함된 경우만 선택
                        if (image.name.Equals("Jacket Image", StringComparison.OrdinalIgnoreCase))
                        {
                            string gameObjectPath = GetGameObjectPath(image.gameObject);
                            if (gameObjectPath.ToLower().Contains("fullsize jacket"))
                            {
                                targetImages.Add(image);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 개별 이미지 처리 중 오류는 무시하고 계속 진행
                        MelonLogger.Warning($"[ManagerMusicSelectHook] 이미지 처리 중 오류 (무시): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 이미지 검색 중 오류: {ex.Message}");
            }

            return targetImages;
        }

        private static void ApplyThumbnailToImages(List<Image> targetImages, Sprite sprite)
        {
            foreach (var targetImage in targetImages)
            {
                try
                {
                    if (targetImage != null)
                    {
                        targetImage.sprite = sprite;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 썸네일 적용 중 오류 (무시): {ex.Message}");
                }
            }
        }
    
    }
}
