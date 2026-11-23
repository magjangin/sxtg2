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
        /// <summary>자켓 파일을 이미 찾아봤는지(못 찾았어도 true). 매번 디스크를 뒤지지 않게 한다.</summary>
        public bool JacketLookupDone { get; set; }

        public CustomTrackData(TrackData resourceDonor, string albumFolder, string bmsPath)
        {
            ResourceDonor = resourceDonor;
            AlbumFolder = albumFolder;
            BmsPath = bmsPath;
        }
    }

    /// <summary>
    /// 지금(또는 방금 끝난) 플레이가 커스텀 곡인지. 결과 화면의 서버 전송·하이스코어 저장 차단이 이 값을 본다.
    /// ManagerPlay.FetchBMSToModules(곡마다 호출)에서 정하고, 곡 선택 화면에 들어오면 끈다.
    /// 커스텀 곡 ID(CUSTOM_…)는 공식 서버가 모르는 곡이라 BlockSave를 꺼도 전송하지 않는다.
    /// </summary>
    public static class CustomPlayState
    {
        public static bool IsCustomTrackPlay { get; set; }
    }
}
