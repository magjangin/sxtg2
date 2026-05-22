using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
        internal static void FixMaxScoreField(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                var maxScoreField = sxgtDataType.GetField(
                    "maxScore",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (maxScoreField != null)
                {
                    maxScoreField.SetValue(sxgtDataInstance, -1f);
                }

                var sxgtReaderType = Helpers.TypeFinderHelper.FindType("SXGTReader");
                if (sxgtReaderType != null)
                {
                    var maxScoreFieldStatic = sxgtReaderType.GetField(
                        "MaxScore",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (maxScoreFieldStatic != null)
                    {
                        maxScoreFieldStatic.SetValue(null, -1f);
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] 스코어 제한 해제 실패: {ex.Message}");
            }
        }
    }
}
