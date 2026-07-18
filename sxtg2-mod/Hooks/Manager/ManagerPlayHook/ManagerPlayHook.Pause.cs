using System;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerPlayHook
    {
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
    }
}
