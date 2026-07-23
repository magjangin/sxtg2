using HarmonyLib;
using MelonLoader;
using System.Reflection;
using System;
using RhythmGame;
using sxtg2.Features;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Manager
{
    [HarmonyPatch(typeof(ManagerPlay))]
    public static partial class ManagerPlayHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            MelonLogger.Msg("[ManagerPlayHook] Initialize() - 자동 HarmonyPatch 적용 상태");
            _isInitialized = true;
        }

        [HarmonyPatch("PauseGame")]
        [HarmonyPostfix]
        private static void PauseGamePostfix()
        {
            try
            {
                PauseMethodHelper.ApplyCustomPauseJacket();
            }
            catch (Exception ex)
            {
                ModLog.Exception("ManagerPlayHook.PauseGamePostfix", ex);
            }
        }

        private static void OnPlaySceneStart(object __instance = null, string methodName = null)
        {
            try
            {
                CustomPlayStartupFlow.Run(__instance, methodName);
            }
            catch (Exception ex)
            {
                var errorContext = string.IsNullOrEmpty(methodName) ? "플레이 씬 시작" : methodName;
                MelonLogger.Warning($"[ManagerPlayHook] {errorContext} 후킹 오류: {ex.Message}");
            }
        }

        [HarmonyPatch("set_bms")]
        [HarmonyPostfix]
        private static void SetBmsPostfix(ManagerPlay __instance)
        {
            OnPlaySceneStart(__instance, "set_bms");
        }

        [HarmonyPatch("FetchBMSToModules")]
        [HarmonyPostfix]
        private static void FetchBMSToModulesPostfix(ManagerPlay __instance)
        {
            OnPlaySceneStart(__instance, "FetchBMSToModules");
        }

        [HarmonyPatch("GetPatternFromDir")]
        [HarmonyPostfix]
        private static void GetPatternFromDirPostfix(ManagerPlay __instance)
        {
            OnPlaySceneStart(__instance, "GetPatternFromDir");
        }
    }
}
