using System;
using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Hooks.Manager;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
        private static string GetCurrentTrackDisplayName()
        {
            try
            {
                if (!ManagerMusicSelectBridge.TryGetCurrentSelectedTrackData(out var trackData))
                    return null;
                return ReflectionHelper.GetFirstMemberValueSafe(trackData, ReflectionMemberNames.TrackData.DisplayName) as string;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextHook] DisplayName 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        private static string FindAlbumFolderByDisplayName(string displayName, string trackId)
        {
            return ManagerMusicSelectHook.ResolveAlbumFolderForTrack(displayName, trackId);
        }

    }
}
