using System;
using MelonLoader;
using sxtg2.Helpers;
using UnityEngine;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerPlayHook
    {
        private static void PauseMethodPostfix()
        {
            try
            {
                MelonLogger.Msg("[ManagerPlayHook] 일시정지 메서드 호출됨 (ESC 또는 일시정지 메뉴)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerPlayHook] 일시정지 메서드 후킹 오류: {ex.Message}");
            }
        }

        private static void UpdatePrefix(object __instance)
        {
            try
            {
                // ESC 키 체크
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    MelonLogger.Msg("[ManagerPlayHook] ESC 키 입력 감지");
                    PauseMethodHelper.CallPauseMenu();
                }
            }
            catch (Exception ex)
            {
                ModLog.Exception("ManagerPlayHook.UpdatePrefix", ex);
            }
        }
    }
}
