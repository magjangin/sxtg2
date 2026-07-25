using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using RhythmGame;
using RhythmGame.Play;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Play
{
    /// <summary>
    /// 플레이 씬에서 동작하는 오토플레이 패치
    /// </summary>
    [HarmonyPatch]
    public static class AutoPlayHook
    {
        public static float CurrentTimeSeconds = -1f;
        public static bool IsPlayScene = false;

        private static Action<RG_PS_Judgement, float, int> _autoPlayJudge;
        private static FieldInfo _noteJudgeCursorField;
        private static FieldInfo _numLanesField;

        [HarmonyPatch(typeof(ManagerPlay), "CheckGameFinished", new[] { typeof(float) })]
        [HarmonyPrefix]
        private static void CheckGameFinished_Prefix(float __0)
        {
            CurrentTimeSeconds = __0;
        }

        [HarmonyPatch(typeof(RG_PS_Judgement), "Update")]
        [HarmonyPostfix]
        private static void RG_PS_Judgement_Update_Postfix(RG_PS_Judgement __instance)
        {
            if (!ModLog.EnableAutoPlay || !IsPlayScene)
                return;

            float curTime = CurrentTimeSeconds;
            if (curTime < 0f)
                return;

            try
            {
                EnsureCaches();
                if (_autoPlayJudge == null)
                    return;

                int laneCount = GetLaneCount(__instance);
                for (int lane = 0; lane < laneCount; lane++)
                {
                    _autoPlayJudge(__instance, curTime, lane);
                }
            }
            catch
            {
                // 플레이 중 프레임 예외로 무너지는 것 방지
            }
        }

        private static void EnsureCaches()
        {
            if (_autoPlayJudge == null)
            {
                var m = AccessTools.Method(typeof(RG_PS_Judgement), "AutoPlayJudge", new[] { typeof(float), typeof(int) });
                if (m != null)
                    _autoPlayJudge = AccessTools.MethodDelegate<Action<RG_PS_Judgement, float, int>>(m);
            }

            if (_noteJudgeCursorField == null)
                _noteJudgeCursorField = AccessTools.Field(typeof(RG_PS_Judgement), "noteJudgeCursor");
            if (_numLanesField == null)
                _numLanesField = AccessTools.Field(typeof(RG_PS_Judgement), "numLanes");
        }

        private static int GetLaneCount(RG_PS_Judgement instance)
        {
            try
            {
                if (_noteJudgeCursorField != null)
                {
                    if (_noteJudgeCursorField.GetValue(instance) is IList list && list.Count > 0)
                        return list.Count;
                }

                if (_numLanesField != null)
                {
                    int n = (int)_numLanesField.GetValue(instance);
                    if (n > 0 && n <= 10) return n;
                }
            }
            catch { }

            return 10;
        }
    }
}
