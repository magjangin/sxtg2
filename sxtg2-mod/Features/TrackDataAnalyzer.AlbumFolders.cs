using System.Collections.Generic;
using sxtg2.Helpers.Track;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        private static List<string> GetAlbumFolders(string hwaFolder)
        {
            return BmsFileResolver.GetAlbumFoldersOrRootWithBms(hwaFolder);
        }
    }
}
