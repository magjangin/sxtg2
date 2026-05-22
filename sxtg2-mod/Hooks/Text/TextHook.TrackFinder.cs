using System;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
        /// <summary>
        /// 현재 선택된 Track ID를 찾습니다.
        /// </summary>
        private static string FindCurrentTrackId()
        {
            try
            {
                if (!ManagerMusicSelectBridge.TryGetCurrentSelectedTrackData(out var trackData))
                {
                    MelonLogger.Warning("[TextHook] currentSelectedTrack을 찾을 수 없습니다.");
                    return null;
                }

                ManagerMusicSelectBridge.ReadTrackIdentity(trackData, out var trackId, out _);
                if (string.IsNullOrEmpty(trackId))
                    MelonLogger.Warning("[TextHook] TrackData에서 ID를 읽을 수 없습니다.");
                return trackId;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] Track ID 찾기 중 오류: {ex.Message}");
                return null;
            }
        }
    }
}
