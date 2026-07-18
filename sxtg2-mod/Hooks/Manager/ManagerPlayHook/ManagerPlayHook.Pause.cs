using System;
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
    }
}
