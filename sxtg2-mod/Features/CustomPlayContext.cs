using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;
using sxtg2.Hooks.Manager;

namespace sxtg2.Features
{
    public sealed class CustomPlayContext
    {
        public string TrackId { get; private set; }
        public string DisplayName { get; private set; }
        public string AlbumFolder { get; private set; }
        public object TrackData { get; private set; }

        private CustomPlayContext(object trackData, string trackId, string displayName, string albumFolder)
        {
            TrackData = trackData;
            TrackId = trackId;
            DisplayName = displayName;
            AlbumFolder = albumFolder;
        }

        public static bool TryResolveCurrentCustomTrack(out CustomPlayContext context)
        {
            context = null;

            if (!ManagerMusicSelectBridge.TryGetCurrentSelectedTrackData(out var trackData))
            {
                MelonLogger.Warning("[CustomPlayContext] currentSelectedTrack을 찾을 수 없습니다.");
                return false;
            }

            ManagerMusicSelectBridge.ReadTrackIdentity(trackData, out var trackId, out var displayName);

            if (!CustomTrackHelper.IsCustomTrack(trackData))
            {
                MelonLogger.Msg($"[CustomPlayContext] 일반 트랙 감지: ID={trackId}, DisplayName={displayName}");
                return false;
            }

            var albumFolder = ManagerMusicSelectHook.ResolveAlbumFolderForTrack(displayName, trackId);
            context = new CustomPlayContext(trackData, trackId, displayName, albumFolder);
            return true;
        }
    }
}
