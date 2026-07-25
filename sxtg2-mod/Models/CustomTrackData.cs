using UnityEngine;

namespace sxtg2.Models
{
    /// <summary>
    /// 게임의 TrackData 흐름을 그대로 이용하면서 로컬 앨범 정보를 함께 전달합니다.
    /// </summary>
    public sealed class CustomTrackData : TrackData
    {
        public string AlbumFolder { get; }
        public string BmsPath { get; }
        public TrackData ResourceDonor { get; }
        public Sprite CustomJacket { get; set; }

        public CustomTrackData(TrackData resourceDonor, string albumFolder, string bmsPath)
        {
            ResourceDonor = resourceDonor;
            AlbumFolder = albumFolder;
            BmsPath = bmsPath;
        }
    }
}
