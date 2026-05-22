using MelonLoader;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Audio
{
    public static partial class HighscoreMeterHook
    {
        public static void ApplyForCustomChart()
        {
            if (!CustomTrackHelper.IsCustomPlayActive())
            {
                MelonLogger.Msg("[HighscoreMeterHook] 일반 트랙 - 스코어/클리어 사운드 보정 건너뜀");
                return;
            }

            FixSXGTReaderMaxScore();
            MelonLogger.Msg("[HighscoreMeterHook] 커스텀 차트용 스코어 보정 적용 완료");
        }
    }
}
