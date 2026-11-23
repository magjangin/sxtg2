using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace sxtg2
{
    /// <summary>
    /// Harmony 패치를 패치 클래스마다 따로 적용한다.
    /// MelonLoader 기본 동작(HarmonyInstance.PatchAll)은 한 클래스에서 예외가 나면(게임 업데이트로 대상 메서드가
    /// 사라지거나 오버로드가 생긴 경우 등) 그 뒤 클래스를 전부 건너뛴다. 여기서는 실패한 클래스만 빠지고
    /// 나머지 기능은 계속 동작하며, 실패 목록을 시작 로그 한 줄로 남긴다.
    /// 사용하려면 어셈블리에 [assembly: HarmonyDontPatchAll]이 있어야 한다(없으면 모든 훅이 두 번 걸린다).
    /// </summary>
    internal static class SafePatcher
    {
        public static void PatchAll(HarmonyLib.Harmony harmony)
        {
            int succeeded = 0;
            var failed = new List<string>();

            foreach (Type type in AccessTools.GetTypesFromAssembly(typeof(SafePatcher).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), false).Length == 0)
                    continue;

                try
                {
                    var patched = harmony.CreateClassProcessor(type).Patch();
                    if (patched != null && patched.Count > 0)
                        succeeded++;
                }
                catch (Exception ex)
                {
                    failed.Add(type.Name);
                    MelonLogger.Error($"[SafePatcher] {type.FullName} 패치 실패 - 이 기능만 빠지고 나머지는 계속 동작합니다: {Describe(ex)}");
                }
            }

            if (failed.Count == 0)
                MelonLogger.Msg($"[SafePatcher] 패치 클래스 {succeeded}개 적용 완료 (실패 0)");
            else
                MelonLogger.Warning($"[SafePatcher] 패치 클래스 {succeeded}개 적용, 실패 {failed.Count}개: {string.Join(", ", failed.ToArray())}. 게임 업데이트로 대상이 바뀌었는지 확인하세요.");
        }

        private static string Describe(Exception ex)
        {
            // Harmony는 실제 원인을 InnerException 안쪽에 감싸 던지는 경우가 많다.
            while (ex is TargetInvocationException || ex is TypeInitializationException)
            {
                if (ex.InnerException == null)
                    break;
                ex = ex.InnerException;
            }

            return ex.GetType().Name + ": " + ex.Message;
        }
    }
}
