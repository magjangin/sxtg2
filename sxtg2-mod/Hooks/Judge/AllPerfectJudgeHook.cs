using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Judge
{
    /// <summary>
    /// 판정 인수를 BLUESTAR(0)로 강제 변환하는 올 퍼펙트 판정 조작 모듈
    /// </summary>
    [HarmonyPatch]
    public static class AllPerfectJudgeHook
    {
        private static Type _eJudgesType;
        private static object _bluestarValue;

        private static void InitializeEJudges()
        {
            if (_eJudgesType != null) return;
            try
            {
                _eJudgesType = AccessTools.TypeByName("EJudges");
                if (_eJudgesType != null)
                {
                    _bluestarValue = Enum.ToObject(_eJudgesType, 0); // BLUESTAR = 0
                }
            }
            catch { }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            InitializeEJudges();
            if (_eJudgesType == null)
                yield break;

            string[] targetTypes = {
                "RhythmGame.Play.RG_PS_Judgement",
                "JudgeCounter",
                "JudgeTextViewer",
                "RedStarCounter",
                "FastSlowMeter"
            };

            string[] targetMethods = {
                "JudgeAction",
                "JudgeDivergence",
                "TryJudgeShortNote",
                "AddJudge",
                "OnGetJudge"
            };

            foreach (var typeName in targetTypes)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                foreach (var methodName in targetMethods)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name != methodName) continue;
                        var paramsInfo = m.GetParameters();
                        if (paramsInfo.Length >= 1 && paramsInfo[0].ParameterType == _eJudgesType)
                        {
                            yield return m;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(ref object __0)
        {
            if (!ModLog.EnableAllPerfect) return true;

            InitializeEJudges();
            if (_eJudgesType != null && _bluestarValue != null && __0 != null && __0.GetType() == _eJudgesType)
            {
                __0 = _bluestarValue;
            }

            return true;
        }
    }
}
