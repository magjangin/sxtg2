using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Result
{
    /// <summary>
    /// 오토플레이 및 판정 조작 활성화 시 베스트 스코어/랭킹 저장을 차단하는 모듈
    /// </summary>
    [HarmonyPatch]
    public static class ResultSaveBlockHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("RhythmGame.Result.ManagerResult");
            if (type == null) yield break;

            var m1 = AccessTools.Method(type, "PostRequestPlayResult");
            if (m1 != null) yield return m1;

            var m2 = AccessTools.Method(type, "ComparePlayResultHighScore");
            if (m2 != null) yield return m2;
        }

        [HarmonyPrefix]
        private static bool Prefix(MethodBase __originalMethod)
        {
            bool shouldBlock = ModLog.BlockSaveBestRanking || ModLog.EnableAutoPlay || ModLog.EnableAllPerfect;

            if (shouldBlock)
            {
                ModLog.Msg($"[차단] 하이스코어 및 랭킹 저장 차단: {__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name}");
                return false;
            }

            return true;
        }
    }
}
