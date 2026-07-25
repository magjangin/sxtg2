using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using RhythmGame.MusicSelect;
using UnityEngine;
using UnityEngine.Networking;
using sxtg2.Hooks.Audio;
using sxtg2.Loaders;
using sxtg2.Models;

namespace sxtg2.Features
{
    public static class TrackDataAnalyzer
    {
        private const string LogPrefix = "[TrackDataAnalyzer]";
        private static readonly string[] BmsPatterns = { "*.bms", "*.bme", "*.bml" };

        public static void InjectCustomTracks(List<TrackData> trackDatas)
        {
            if (trackDatas == null || trackDatas.Count == 0)
            {
                MelonLogger.Warning($"{LogPrefix} 복제할 원본 TrackData가 없습니다.");
                return;
            }

            if (trackDatas.Any(track => track is CustomTrackData))
            {
                return;
            }

            string gamePath = Path.GetDirectoryName(Application.dataPath);
            string hwaFolder = Path.Combine(gamePath, "hwa");
            if (!Directory.Exists(hwaFolder))
            {
                return;
            }

            TrackData donor = trackDatas[0];
            int addedCount = 0;

            foreach (string albumFolder in EnumerateAlbumFolders(hwaFolder))
            {
                string bmsPath = FindFirstBms(albumFolder);
                if (string.IsNullOrEmpty(bmsPath))
                {
                    continue;
                }

                try
                {
                    var trackInfo = TrackInfoParser.ParseTrackInfo(
                        albumFolder,
                        Path.GetFileNameWithoutExtension(bmsPath));

                    trackDatas.Add(CreateCustomTrack(donor, trackInfo, albumFolder, bmsPath));
                    addedCount++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning(
                        $"{LogPrefix} 앨범 추가 실패 ({Path.GetFileName(albumFolder)}): {ex.Message}");
                }
            }

            MelonLogger.Msg($"{LogPrefix} 커스텀 트랙 {addedCount}개 추가 완료");
        }

        private static IEnumerable<string> EnumerateAlbumFolders(string hwaFolder)
        {
            var folders = new List<string>();
            if (FindFirstBms(hwaFolder) != null)
                folders.Add(hwaFolder);

            folders.AddRange(Directory
                .GetDirectories(hwaFolder)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
            return folders;
        }

        private static string FindFirstBms(string folder)
        {
            foreach (string pattern in BmsPatterns)
            {
                string match = Directory.EnumerateFiles(
                    folder,
                    pattern,
                    SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (match != null)
                    return match;
            }

            return null;
        }

        private static CustomTrackData CreateCustomTrack(
            TrackData donor,
            TrackInfoParser.TrackInfo trackInfo,
            string albumFolder,
            string bmsPath)
        {
            string title = !string.IsNullOrEmpty(trackInfo.Title)
                ? trackInfo.Title
                : "커스텀 차트";
            string composer = !string.IsNullOrEmpty(trackInfo.Artist)
                ? trackInfo.Artist
                : donor.Composer;

            return new CustomTrackData(donor, albumFolder, bmsPath)
            {
                ID = BuildStableCustomId(bmsPath),
                DisplayName = title,
                AlphabetName = title,
                Composer = composer,
                ComposerEN = composer,
                visibilityOnList = true,
                BGAOffset = 0f,
                Level = BuildLevelArray(trackInfo.Difficulties, donor.Level),
                Level_LITE = BuildLevelArray(trackInfo.Difficulties, donor.Level_LITE),
                artistInfoDic = donor.artistInfoDic != null
                    ? new Dictionary<string, string>(donor.artistInfoDic)
                    : new Dictionary<string, string>()
            };
        }

        private static string BuildStableCustomId(string path)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;

            string albumName = Path.GetFileName(Path.GetDirectoryName(path));
            string stablePath = albumName + "/" + Path.GetFileName(path);
            foreach (char character in stablePath.ToUpperInvariant())
            {
                hash ^= character;
                hash *= prime;
            }

            return $"CUSTOM_{hash:X16}";
        }

        private static string[] BuildLevelArray(List<int> difficulties, string[] fallback)
        {
            var levels = new string[4];
            bool hasCustomLevels = difficulties != null && difficulties.Count > 0;

            for (int i = 0; i < levels.Length; i++)
            {
                if (hasCustomLevels)
                {
                    int value = difficulties[Math.Min(i, difficulties.Count - 1)];
                    levels[i] = value.ToString("D2");
                }
                else if (fallback != null && i < fallback.Length && !string.IsNullOrEmpty(fallback[i]))
                {
                    levels[i] = fallback[i];
                }
                else
                {
                    levels[i] = "00";
                }
            }

            return levels;
        }
    }

    [HarmonyPatch(typeof(ManagerMusicSelect))]
    public static class ManagerMusicSelectHook
    {
        private static int _previewRequestVersion;

        [HarmonyPatch("Awake")]
        [HarmonyPostfix]
        private static void AwakePostfix(ManagerMusicSelect __instance)
        {
            try
            {
                TrackDataAnalyzer.InjectCustomTracks(__instance.trackDatas);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] 커스텀 트랙 주입 실패: {ex}");
            }
        }

        [HarmonyPatch("PlayPreview")]
        [HarmonyPrefix]
        private static bool PlayPreviewPrefix(
            ManagerMusicSelect __instance,
            TrackData t,
            AudioSource ___bgmSource,
            AudioSource ___previewSource,
            ref Coroutine ___previewCoroutine,
            ref Coroutine ___previewStopperCoroutine)
        {
            int requestVersion = ++_previewRequestVersion;
            if (!(t is CustomTrackData customTrack))
                return true;

            try
            {
                StopCoroutine(__instance, ref ___previewCoroutine);
                StopCoroutine(__instance, ref ___previewStopperCoroutine);
                ___previewSource.Stop();
                ___bgmSource.Stop();

                string previewFile = BgmFileResolver.FindPreviewForAlbum(customTrack.AlbumFolder);
                if (string.IsNullOrEmpty(previewFile))
                {
                    RestoreMenuBgm(___bgmSource);
                    MelonLogger.Warning(
                        $"[ManagerMusicSelectHook] 프리뷰 파일을 찾지 못했습니다: {customTrack.DisplayName}");
                    return false;
                }

                ___previewCoroutine = __instance.StartCoroutine(LoadAndPlayPreview(
                    ___previewSource,
                    ___bgmSource,
                    previewFile,
                    requestVersion));
            }
            catch (Exception ex)
            {
                RestoreMenuBgm(___bgmSource);
                MelonLogger.Error($"[ManagerMusicSelectHook] 프리뷰 교체 실패: {ex}");
            }

            return false;
        }

        private static void StopCoroutine(
            ManagerMusicSelect manager,
            ref Coroutine coroutine)
        {
            if (coroutine == null)
                return;

            manager.StopCoroutine(coroutine);
            coroutine = null;
        }

        private static IEnumerator LoadAndPlayPreview(
            AudioSource previewSource,
            AudioSource bgmSource,
            string filePath,
            int requestVersion)
        {
            string url = "file://" + filePath.Replace("\\", "/");
            AudioType audioType = GetAudioType(Path.GetExtension(filePath));
            using (var request = UnityWebRequestMultimedia.GetAudioClip(url, audioType))
            {
                yield return request.SendWebRequest();

                if (requestVersion != _previewRequestVersion)
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning(
                        $"[ManagerMusicSelectHook] 프리뷰 로드 실패: {request.error}");
                    RestoreMenuBgm(bgmSource);
                    yield break;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null)
                {
                    RestoreMenuBgm(bgmSource);
                    yield break;
                }

                previewSource.clip = clip;
                previewSource.volume = Util.GetGameplayVolume();
                previewSource.Play();
                MelonLogger.Msg(
                    $"[ManagerMusicSelectHook] 프리뷰 재생: {Path.GetFileName(filePath)}");

                yield return new WaitForSeconds(clip.length);
            }

            if (requestVersion == _previewRequestVersion)
            {
                previewSource.Stop();
                RestoreMenuBgm(bgmSource);
            }
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

        private static void RestoreMenuBgm(AudioSource bgmSource)
        {
            if (bgmSource == null)
                return;

            bgmSource.volume = Util.GetBGMVolume();
            if (!bgmSource.isPlaying)
                bgmSource.Play();
        }
    }
}
