using System;
using System.Collections;
using System.IO;
using HarmonyLib;
using MelonLoader;
using RhythmGame.MusicSelect;
using UnityEngine;
using UnityEngine.Networking;
using sxtg2.Features;
using sxtg2.Hooks.Audio;
using sxtg2.Models;

namespace sxtg2.Hooks.Manager
{
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
