using System;
using HarmonyLib;
using MelonLoader;
using GameSetting;
using RhythmGame;
using UnityEngine;
using UnityEngine.Video;
using sxtg2.Hooks.Audio;
using sxtg2.Loaders;
using sxtg2.Models;
using sxtg2.Processors;

namespace sxtg2.Hooks.Manager
{
    [HarmonyPatch(typeof(ManagerPlay))]
    public static class ManagerPlayHook
    {
        [HarmonyPatch("FetchBMSToModules")]
        [HarmonyPrefix]
        private static void FetchBMSToModulesPrefix(
            ManagerPlay __instance,
            SXGTData _bms,
            TrackData ___playTrack,
            VideoPlayer ___bgaPlayer,
            GameObject ___defaultBGAcanvas)
        {
            if (!(___playTrack is CustomTrackData customTrack))
                return;

            try
            {
                var chart = BmsParser.ParseBmsFileWithStatistics(customTrack.BmsPath);
                if (chart?.Notes == null || chart.Notes.Count == 0)
                {
                    MelonLogger.Warning(
                        $"[ManagerPlayHook] 차트를 읽지 못해 원본 패턴을 유지합니다: {customTrack.BmsPath}");
                }
                else
                {
                    CustomChartInjector.SetParsedChart(chart);
                    CustomChartInjector.InjectBmsNotesToLaneData(_bms);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerPlayHook] 커스텀 차트 주입 실패: {ex}");
            }

            try
            {
                BGMPlayerHook.ResetReplacementFlag();
                BGMPlayerHook.ReplacePlaySceneBGM(__instance.bgm, customTrack.AlbumFolder);

                BGAPlayerHook.ResetReplacementFlag();
                if (UserAccountModule.Instance.userData.bgaMode == BGAMode.ON &&
                    BGAPlayerHook.ReplacePlaySceneBGA(___bgaPlayer, customTrack.AlbumFolder))
                {
                    ___bgaPlayer.gameObject.SetActive(true);
                    ___defaultBGAcanvas.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerPlayHook] 커스텀 미디어 교체 실패: {ex}");
            }
        }

        [HarmonyPatch("CheckBGMStart")]
        [HarmonyPrefix]
        private static bool CheckBGMStartPrefix(TrackData ___playTrack)
        {
            return !(___playTrack is CustomTrackData) || !BGMPlayerHook.IsLoading();
        }
    }
}
