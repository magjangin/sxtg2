using System;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;
using sxtg2.Helpers.Track;
using sxtg2.Models;

namespace sxtg2.Hooks.Track
{
    [HarmonyPatch(typeof(TrackData))]
    public static class TrackDataMediaHook
    {
        [HarmonyPatch(nameof(TrackData.GetJacketSprite), new Type[] { })]
        [HarmonyPrefix]
        private static bool GetJacketSpritePrefix(TrackData __instance, ref Sprite __result)
        {
            return TryUseCustomJacket(__instance, ref __result);
        }

        [HarmonyPatch(nameof(TrackData.GetThumbSprite))]
        [HarmonyPrefix]
        private static bool GetThumbSpritePrefix(TrackData __instance, ref Sprite __result)
        {
            return TryUseCustomJacket(__instance, ref __result);
        }

        [HarmonyPatch(nameof(TrackData.GetAudioClip), new Type[] { })]
        [HarmonyPrefix]
        private static bool GetAudioClipPrefix(TrackData __instance, ref AudioClip __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetAudioClip();
            return false;
        }

        [HarmonyPatch(nameof(TrackData.GetLoadingAnimation))]
        [HarmonyPrefix]
        private static bool GetLoadingAnimationPrefix(
            TrackData __instance,
            ref VideoClip __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetLoadingAnimation();
            return false;
        }

        [HarmonyPatch(
            nameof(TrackData.GetSixtarPatternDirectory),
            new[] { typeof(ELevels), typeof(EPlayStyle), typeof(bool) })]
        [HarmonyPrefix]
        private static bool GetPatternDirectoryPrefix(
            TrackData __instance,
            ELevels lv,
            EPlayStyle ps,
            bool willUseSXGT,
            ref string __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetSixtarPatternDirectory(
                lv,
                ps,
                willUseSXGT);
            return false;
        }

        [HarmonyPatch(
            nameof(TrackData.GetSixtarPatternDirectory),
            new[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        private static bool GetPatternDirectoryByLevelPrefix(
            TrackData __instance,
            string lv,
            bool willUseSXGT,
            ref string __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetSixtarPatternDirectory(
                lv,
                willUseSXGT);
            return false;
        }

        private static bool TryUseCustomJacket(TrackData track, ref Sprite result)
        {
            if (!(track is CustomTrackData customTrack))
                return true;

            if (customTrack.CustomJacket == null)
            {
                customTrack.CustomJacket = ThumbnailLoader.LoadThumbnail(
                    customTrack.ID,
                    customTrack.AlbumFolder);
            }

            if (customTrack.CustomJacket == null)
            {
                MelonLogger.Warning(
                    $"[TrackDataMediaHook] 커스텀 자켓을 찾지 못해 기본 자켓을 사용합니다: {customTrack.DisplayName}");
                return true;
            }

            result = customTrack.CustomJacket;
            return false;
        }
    }
}
