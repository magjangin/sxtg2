namespace sxtg2.Helpers
{
    internal static class ReflectionMemberNames
    {
        internal static class FilePatterns
        {
            internal static readonly string[] BmsExtensions = { "*.bms", "*.bme", "*.bml" };
            internal const string Txt = "*.txt";
        }

        internal static class Paths
        {
            internal const string HwaFolder = "hwa";
        }

        internal static class TextValues
        {
            internal const string DefaultCustomChartTitle = "커스텀 차트";
        }

        internal static class Types
        {
            internal const string ManagerMusicSelect = "ManagerMusicSelect";
            internal const string ManagerMusicSelectHook = "ManagerMusicSelectHook";
            internal const string TrackLoader = "TrackLoader";
            internal const string SXGTData = "SXGTData";
            internal const string TrackManager = "TrackManager";
            internal const string TrackData = "TrackData";
            internal const string ManagerPlay = "ManagerPlay";
        }

        internal static class GameSingleton
        {
            internal const string InstanceMember = "Instance";
            internal const string FindMethod = "Find";
        }

        internal static class TrackManagerMembers
        {
            internal const string TrackList = "TrackList";
        }

        internal static class SXGTDataMembers
        {
            internal const string LaneData = "laneData";
            internal const string ItemIndexer = "Item";
            internal const string TotalNotes = "totalNotes";
            internal const string TotalNoteWithTicks = "totalNoteWithTicks";
        }

        internal static class HoldNoteMembers
        {
            internal const string TickLength = "tickLength";
        }

        internal static class TrackData
        {
            internal static readonly string[] Id = { "ID", "id", "_id" };
            internal static readonly string[] DisplayName = { "DisplayName", "displayName", "Title" };
        }

        internal static class ManagerMusicSelect
        {
            internal const string Instance = GameSingleton.InstanceMember;
            internal const string CurrentSelectedTrack = "currentSelectedTrack";
        }

        internal static class ManagerMusicSelectHook
        {
            internal const string FindAlbumFolderByDisplayName = "FindAlbumFolderByDisplayName";
        }
    }
}
