using System;
using MelonLoader;
using sxtg2.Features;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerPlayHook
    {
        /// <summary>
        /// 플레이 씬 시작 시 공통 초기화 작업을 수행합니다.
        /// </summary>
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

        private static void SetBmsPostfix(object __instance)
        {
            OnPlaySceneStart(__instance, "set_bms");
        }

        private static void FetchBMSToModulesPostfix()
        {
            OnPlaySceneStart(null, "FetchBMSToModules");
        }

        private static void GetPatternFromDirPostfix()
        {
            OnPlaySceneStart(null, "GetPatternFromDir");
        }
    }
}
