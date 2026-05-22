using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Hooks.Audio
{
    public static partial class HighscoreMeterHook
    {
        /// <summary>
        /// SXGTReader의 MaxScore 필드를 수정합니다.
        /// </summary>
        public static void FixSXGTReaderMaxScore()
        {
            try
            {
                var sxgtReaderType = Helpers.TypeFinderHelper.FindType("SXGTReader");
                if (sxgtReaderType != null)
                {
                    var maxScoreField = sxgtReaderType.GetField("MaxScore", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (maxScoreField != null)
                    {
                        maxScoreField.SetValue(null, -1f);
                        MelonLogger.Msg("[HighscoreMeterHook] SXGTReader.MaxScore 수정 완료");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] SXGTReader.MaxScore 수정 실패: {ex.Message}");
            }
        }
    }
}



















