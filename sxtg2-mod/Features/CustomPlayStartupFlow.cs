using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers.Track;
using sxtg2.Hooks.Audio;
using sxtg2.Hooks.SXGT;
using sxtg2.Hooks.Text;

namespace sxtg2.Features
{
    public static class CustomPlayStartupFlow
    {
        public static void Run(object managerPlayInstance, string sourceMethodName)
        {
            if (!string.IsNullOrEmpty(sourceMethodName))
            {
                MelonLogger.Msg($"[CustomPlayStartupFlow] {sourceMethodName} 호출됨");
            }

            if (!CustomPlayContext.TryResolveCurrentCustomTrack(out var customContext))
            {
                ClearCustomPlayState();
                return;
            }

            StartCustomPlay(managerPlayInstance, customContext);
        }

        private static void ClearCustomPlayState()
        {
            CustomTrackHelper.ClearSelectedTrack();
            SXGTDataHook.ResetPendingInstance();
            MelonLogger.Msg("[CustomPlayStartupFlow] 일반 트랙 - 커스텀 차트 기능 건너뜀");
        }

        private static void StartCustomPlay(object managerPlayInstance, CustomPlayContext customContext)
        {
            CustomTrackHelper.SetSelectedTrack(customContext.TrackId, customContext.DisplayName, customContext.AlbumFolder);
            LoadBmsForCurrentTrack(customContext);

            ResetMediaReplacementState();
            CaptureManagerPlayBgm(managerPlayInstance);
            ReplaceMedia(customContext.AlbumFolder);

            HighscoreMeterHook.ApplyForCustomChart();
            SXGTDataHook.ProcessPendingNoteRemovalAndInjection();
        }

        private static void LoadBmsForCurrentTrack(CustomPlayContext customContext)
        {
            try
            {
                if (customContext == null)
                {
                    return;
                }

                MelonLogger.Msg($"[CustomPlayStartupFlow] 커스텀 트랙 감지: ID={customContext.TrackId}, DisplayName={customContext.DisplayName}");
                TextHook.LoadAndInjectBmsForTrack(customContext.TrackId, customContext.DisplayName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomPlayStartupFlow] BMS 파일 로드 실패: {ex.Message}");
            }
        }

        private static void ResetMediaReplacementState()
        {
            BGAPlayerHook.ResetReplacementFlag();
            BGMPlayerHook.ResetReplacementFlag();
        }

        private static void CaptureManagerPlayBgm(object managerPlayInstance)
        {
            if (managerPlayInstance == null)
            {
                return;
            }

            var managerPlayType = managerPlayInstance.GetType();
            var bgmField = managerPlayType.GetField("bgm", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (bgmField == null)
            {
                return;
            }

            var bgmValue = bgmField.GetValue(managerPlayInstance);
            if (bgmValue is AudioSource bgmAudioSource)
            {
                BGMPlayerHook.SetManagerPlayBGM(bgmAudioSource);
            }
        }

        private static void ReplaceMedia(string albumFolder)
        {
            BGAPlayerHook.ReplacePlaySceneBGA(albumFolder);
            BGMPlayerHook.ReplacePlaySceneBGM(albumFolder);
        }
    }
}
