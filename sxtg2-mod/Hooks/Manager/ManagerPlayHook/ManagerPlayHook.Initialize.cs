using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerPlayHook
    {
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

                    // 일시정지 관련 메서드 찾기 및 후킹
                    var pauseMethods = new[] { "Pause", "OnPause", "SetPause", "TogglePause", "ShowPauseMenu", "OpenPauseMenu" };
                    foreach (var methodName in pauseMethods)
                    {
                        var pauseMethod = managerPlayType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                        if (pauseMethod != null)
                        {
                            var postfix = new HarmonyMethod(typeof(ManagerPlayHook).GetMethod(nameof(PauseMethodPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                            harmony.Patch(pauseMethod, postfix: postfix);
                            MelonLogger.Msg($"[ManagerPlayHook] {methodName} 메서드 후킹 완료");
                            break;
                        }
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
    }
}
