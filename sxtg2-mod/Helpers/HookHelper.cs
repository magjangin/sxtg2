using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace sxtg2.Helpers
{
    public static class HookHelper
    {
        /// <summary>
        /// 메서드를 안전하게 후킹합니다.
        /// </summary>
        public static bool SafePatch(HarmonyLib.Harmony harmony, Type targetType, string methodName, BindingFlags flags, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            try
            {
                if (targetType == null || string.IsNullOrEmpty(methodName))
                    return false;

                var method = targetType.GetMethod(methodName, flags);
                if (method == null)
                {
                    MelonLogger.Warning($"[HookHelper] 메서드를 찾을 수 없습니다: {targetType.Name}.{methodName}");
                    return false;
                }

                harmony.Patch(method, prefix: prefix, postfix: postfix);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HookHelper] 메서드 후킹 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 생성자를 안전하게 후킹합니다.
        /// </summary>
        public static bool SafePatchConstructor(HarmonyLib.Harmony harmony, Type targetType, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
        {
            try
            {
                if (targetType == null)
                    return false;

                var constructors = targetType.GetConstructors();
                if (constructors.Length == 0)
                {
                    MelonLogger.Warning($"[HookHelper] 생성자를 찾을 수 없습니다: {targetType.Name}");
                    return false;
                }

                // 첫 번째 생성자 후킹
                harmony.Patch(constructors[0], prefix: prefix, postfix: postfix);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HookHelper] 생성자 후킹 실패: {ex.Message}");
                return false;
            }
        }
    }
}























