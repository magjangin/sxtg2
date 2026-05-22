using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
        /// <summary>
        /// TextHook 초기화 로직
        /// </summary>
        public static void Initialize()
        {
            MelonLogger.Msg("[TextHook] Initialize() 호출됨");
            
            if (_isInitialized)
            {
                MelonLogger.Warning("[TextHook] 이미 초기화되었습니다.");
                return;
            }

            try
            {
                MelonLogger.Msg("[TextHook] Harmony 인스턴스 생성 중...");
                var harmony = new HarmonyLib.Harmony("sxtg2.TextHook");
                MelonLogger.Msg("[TextHook] Harmony 인스턴스 생성 완료");

                // TextSetterPrefix 메서드 찾기
                var prefixMethod = typeof(TextHook).GetMethod(nameof(TextSetterPrefix), BindingFlags.NonPublic | BindingFlags.Static);
                if (prefixMethod == null)
                {
                    MelonLogger.Error("[TextHook] TextSetterPrefix 메서드를 찾을 수 없습니다!");
                    return;
                }
                MelonLogger.Msg($"[TextHook] TextSetterPrefix 메서드 찾기 성공: {prefixMethod.Name}");

                // UnityEngine.UI.Text.text 속성 후킹
                HookUnityText(harmony, prefixMethod);

                // TMPro.TextMeshProUGUI.text 속성 후킹
                HookTextMeshPro(harmony, prefixMethod);

                _isInitialized = true;
                MelonLogger.Msg("[TextHook] 초기화 완료!");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void HookUnityText(HarmonyLib.Harmony harmony, MethodInfo prefixMethod)
        {
            MelonLogger.Msg("[TextHook] UnityEngine.UI.Text 타입 확인 중...");
            var textType = typeof(UnityEngine.UI.Text);
            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text 타입: {textType.FullName}");
            
            var textProperty = textType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (textProperty == null)
            {
                MelonLogger.Warning("[TextHook] UnityEngine.UI.Text.text 속성을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text 속성 찾기 성공");
            if (textProperty.SetMethod == null)
            {
                MelonLogger.Warning("[TextHook] UnityEngine.UI.Text.text SetMethod가 null입니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text SetMethod 찾기 성공, 패치 시도 중...");
            var prefix = new HarmonyMethod(prefixMethod);
            var patchResult = harmony.Patch(textProperty.SetMethod, prefix: prefix);
            MelonLogger.Msg($"[TextHook] UnityEngine.UI.Text.text 패치 결과: {patchResult}");
            if (patchResult != null)
            {
                MelonLogger.Msg("[TextHook] UnityEngine.UI.Text.text 후킹 완료!");
            }
            else
            {
                MelonLogger.Error("[TextHook] UnityEngine.UI.Text.text 패치 실패!");
            }
        }

        private static void HookTextMeshPro(HarmonyLib.Harmony harmony, MethodInfo prefixMethod)
        {
            MelonLogger.Msg("[TextHook] TMPro.TextMeshProUGUI 타입 찾기 중...");
            var tmproType = Helpers.TypeFinderHelper.FindType("TMPro.TextMeshProUGUI");
            if (tmproType == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI 타입을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI 타입 찾기 성공: {tmproType.FullName}");
            var tmproProperty = tmproType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
            if (tmproProperty == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI.text 속성을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text 속성 찾기 성공");
            if (tmproProperty.SetMethod == null)
            {
                MelonLogger.Warning("[TextHook] TMPro.TextMeshProUGUI.text SetMethod가 null입니다.");
                return;
            }

            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text SetMethod 찾기 성공, 패치 시도 중...");
            var prefix = new HarmonyMethod(prefixMethod);
            var patchResult = harmony.Patch(tmproProperty.SetMethod, prefix: prefix);
            MelonLogger.Msg($"[TextHook] TMPro.TextMeshProUGUI.text 패치 결과: {patchResult}");
            if (patchResult != null)
            {
                MelonLogger.Msg("[TextHook] TMPro.TextMeshProUGUI.text 후킹 완료!");
            }
            else
            {
                MelonLogger.Error("[TextHook] TMPro.TextMeshProUGUI.text 패치 실패!");
            }
        }
    }
}

















