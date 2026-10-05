using System;
using UnityEngine;

namespace sxtg2.Models
{
    /// <summary>
    /// 게임의 TrackData 흐름을 그대로 이용하면서 로컬 앨범 정보를 함께 전달합니다.
    /// </summary>
    public sealed class CustomTrackData : TrackData
    {
        /// <summary>커스텀 곡 ID의 접두사. 원본 곡 ID와 섞이지 않고, 서버/기록 쪽 보호 로직이 이 접두사로 커스텀 곡을 알아본다.</summary>
        public const string IdPrefix = "CUSTOM_";

        public static bool IsCustomId(string id)
        {
            return id != null && id.StartsWith(IdPrefix, StringComparison.Ordinal);
        }

        public string AlbumFolder { get; }
        public string BmsPath { get; }
        public TrackData ResourceDonor { get; }
        public Sprite CustomJacket { get; set; }

        /// <summary>
        /// 자켓을 이미 찾아봤는지(못 찾았어도 true). 자켓 파일이 없을 때 게임이 자켓을 요청할 때마다
        /// 파일 9개를 다시 확인하고 경고를 남기지 않게 한다.
        /// </summary>
        public bool JacketSearched { get; set; }

        public CustomTrackData(TrackData resourceDonor, string albumFolder, string bmsPath)
        {
            ResourceDonor = resourceDonor;
            AlbumFolder = albumFolder;
            BmsPath = bmsPath;
        }
    }
}
