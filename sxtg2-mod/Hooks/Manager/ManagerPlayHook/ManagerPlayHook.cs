using HarmonyLib;
using MelonLoader;
using System.Reflection;
using System;
using sxtg2.Features;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerPlayHook
    {
        private static bool _isInitialized = false;
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        public static void Initialize()
        {
            MelonLogger.Msg("[ManagerPlayHook] Initialize() 호출됨");

            if (_isInitialized)
            {
                MelonLogger.Msg("[ManagerPlayHook] 이미 초기화됨, 리턴");
                return;
            }

            try
            {
                MelonLogger.Msg("[ManagerPlayHook] 초기화 시작...");

                var harmony = new HarmonyLib.Harmony("sxtg2.ManagerPlayHook");

                // ManagerPlay 관련 후킹
                var managerPlayType = TypeFinderHelper.FindType("ManagerPlay");
                if (managerPlayType != null)
                {
                    // set_bms 메서드 후킹
                    var setBmsMethod = managerPlayType.GetMethod("set_bms", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (setBmsMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ManagerPlayHook).GetMethod(nameof(SetBmsPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                        harmony.Patch(setBmsMethod, postfix: postfix);
                        MelonLogger.Msg("[ManagerPlayHook] set_bms 메서드 후킹 완료");
                    }

                    // FetchBMSToModules 메서드 후킹
                    var fetchBMSToModulesMethod = managerPlayType.GetMethod("FetchBMSToModules", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fetchBMSToModulesMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ManagerPlayHook).GetMethod(nameof(FetchBMSToModulesPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                        harmony.Patch(fetchBMSToModulesMethod, postfix: postfix);
                        MelonLogger.Msg("[ManagerPlayHook] FetchBMSToModules 메서드 후킹 완료");
                    }

                    // GetPatternFromDir 메서드 후킹
                    var getPatternFromDirMethod = managerPlayType.GetMethod("GetPatternFromDir", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                    if (getPatternFromDirMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ManagerPlayHook).GetMethod(nameof(GetPatternFromDirPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                        harmony.Patch(getPatternFromDirMethod, postfix: postfix);
                        MelonLogger.Msg("[ManagerPlayHook] GetPatternFromDir 메서드 후킹 완료");
                    }

                    // 디컴파일 원본의 실제 일시정지 진입점 후킹
                    var pauseGameMethod = managerPlayType.GetMethod("PauseGame", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pauseGameMethod != null)
                    {
                        var postfix = new HarmonyMethod(typeof(ManagerPlayHook).GetMethod(nameof(PauseGamePostfix), BindingFlags.NonPublic | BindingFlags.Static));
                        harmony.Patch(pauseGameMethod, postfix: postfix);
                        MelonLogger.Msg("[ManagerPlayHook] PauseGame 메서드 후킹 완료 (일시정지 자켓 교체)");
                    }

                    MelonLogger.Msg("[ManagerPlayHook] 초기화 완료");
                }
                else
                {
                    MelonLogger.Warning("[ManagerPlayHook] ManagerPlay 타입을 찾을 수 없습니다.");
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerPlayHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
