using System;
using HarmonyLib;
using MelonLoader;
using RhythmGame.MusicSelect;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Hooks;

namespace sxtg2.Features
{
    // 시이(Shii) Live2D 조사용 로깅 훅 모음. 게임 동작은 바꾸지 않는다.
    //
    // 전부 LogLevel=2(상세)일 때만 동작한다. 예전에는 로그 레벨과 무관하게 곡 선택 확인창을 열 때마다 캐릭터 계층 수백 줄을,
    // 결과 화면에 들어갈 때마다 FindObjectsOfType<GameObject>() 전체 스캔과 수백 줄을 남겨서 모드 로그의 90% 가까이를 차지했다.
    // 핵심 훅(ManagerMusicSelectHook)과 한 클래스에 섞여 있던 것도 따로 뺐다: 진단용 필드/메서드 이름이 게임 업데이트로 바뀌어도
    // 커스텀 곡 등록과 미리듣기가 같이 깨지지 않는다.

    [HarmonyPatch(typeof(ManagerMusicSelect))]
    public static class MusicSelectDiagnosticsHook
    {
        private const int MaxHierarchyDepth = 6;

        private static readonly AccessTools.FieldRef<ManagerMusicSelect, ConfirmWindow> ConfirmWindowField =
            SafeAccess.FieldRef<ManagerMusicSelect, ConfirmWindow>("confirmWindow");

        [HarmonyPatch("OpenConfirmWindow")]
        [HarmonyPostfix]
        private static void OpenConfirmWindowPostfix(ManagerMusicSelect __instance, bool willFetchKey)
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                TrackData track = __instance.trackDatas[__instance.TrackCursor];
                ModLog.Msg(
                    $"[MusicSelectDiagnostics] OpenConfirmWindow 호출: track={track?.DisplayName}, " +
                    $"level={__instance.LevelCursor}, style={__instance.playStyle}, willFetchKey={willFetchKey}");

                LogCharacterLayer(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[MusicSelectDiagnostics] OpenConfirmWindow 로깅 실패: {ex.Message}");
            }
        }

        private static void LogCharacterLayer(ManagerMusicSelect instance)
        {
            ConfirmWindow confirmWindow = SafeAccess.Get(ConfirmWindowField, instance);
            GameObject layer = confirmWindow?.characterLayer;
            if (layer == null)
            {
                ModLog.Msg("[MusicSelectDiagnostics] characterLayer가 비어있습니다.");
                return;
            }

            ModLog.Msg($"[MusicSelectDiagnostics] characterLayer 오브젝트: {layer.name} (active={layer.activeSelf})");
            LogHierarchy(layer.transform, 1);

            OperatorCharacter[] operators = layer.GetComponentsInChildren<OperatorCharacter>(includeInactive: true);
            if (operators.Length == 0)
            {
                ModLog.Msg("[MusicSelectDiagnostics]   -> OperatorCharacter 컴포넌트를 찾지 못했습니다.");
            }

            foreach (OperatorCharacter op in operators)
            {
                ModLog.Msg(
                    $"[MusicSelectDiagnostics]   -> Operator 발견: name={op.OperatorName} " +
                    $"(type={op.GetType().Name}, object={op.gameObject.name}, active={op.gameObject.activeInHierarchy})");
            }
        }

        private static void LogHierarchy(Transform t, int depth)
        {
            if (t == null || depth > MaxHierarchyDepth)
                return;

            foreach (Transform child in t)
            {
                ModLog.Msg(
                    $"[MusicSelectDiagnostics] {new string(' ', depth * 2)}- {child.name} (active={child.gameObject.activeSelf})");
                LogHierarchy(child, depth + 1);
            }
        }

        [HarmonyPatch("instantiateOperatorCharacter")]
        [HarmonyPostfix]
        private static void InstantiateOperatorCharacterPostfix(string opCharID, OperatorCharacter __result)
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                ModLog.Msg(
                    $"[MusicSelectDiagnostics] instantiateOperatorCharacter 호출: opCharID={opCharID}, " +
                    $"result={__result?.OperatorName ?? "null"} (object={__result?.gameObject.name ?? "null"})");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[MusicSelectDiagnostics] instantiateOperatorCharacter 로깅 실패: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(OperatorCharacter))]
    public static class OperatorCharacterHook
    {
        [HarmonyPatch("SetUp")]
        [HarmonyPrefix]
        private static void SetUpPrefix(OperatorCharacter __instance)
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                ModLog.Msg(
                    $"[OperatorCharacterHook] SetUp 호출: name={__instance.OperatorName} " +
                    $"(type={__instance.GetType().Name}, object={__instance.gameObject.name})");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[OperatorCharacterHook] SetUp 로깅 실패: {ex.Message}");
            }
        }

        [HarmonyPatch("ShowDialogue")]
        [HarmonyPrefix]
        private static void ShowDialoguePrefix(OperatorCharacter __instance, EOperatorStatus os)
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                ModLog.Msg(
                    $"[OperatorCharacterHook] ShowDialogue 호출: name={__instance.OperatorName}, " +
                    $"status={os} (type={__instance.GetType().Name}, object={__instance.gameObject.name})");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[OperatorCharacterHook] ShowDialogue 로깅 실패: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(RhythmGame.Result.ManagerResult))]
    public static class ManagerResultHook
    {
        private const int MaxHierarchyDepth = 4;

        private static readonly AccessTools.FieldRef<RhythmGame.Result.ManagerResult, Animator> OperatorAnimatorField =
            SafeAccess.FieldRef<RhythmGame.Result.ManagerResult, Animator>("operatorAnimator");

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        private static void StartPostfix(RhythmGame.Result.ManagerResult __instance)
        {
            if (!ModLog.IsVerbose)
                return;

            try
            {
                ModLog.Msg("[ManagerResultHook] ManagerResult.Start Postfix 실행 감지");
                LogResultOperatorLayer(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[ManagerResultHook] 결과 씬 로깅 실패: {ex.Message}");
            }
        }

        private static void LogResultOperatorLayer(RhythmGame.Result.ManagerResult instance)
        {
            if (instance == null)
            {
                ModLog.Msg("[ManagerResultHook] ManagerResult 인스턴스가 null입니다.");
                return;
            }

            ModLog.Msg("[ManagerResultHook] === 결과 씬 오퍼레이터 레이어 스캔 시작 ===");
            LogOperatorAnimator(instance);
            LogOperatorCharacters(instance);
            int matched = LogMatchingObjects();
            ModLog.Msg($"[ManagerResultHook] === 결과 씬 오퍼레이터 레이어 스캔 완료 (매칭 오브젝트 {matched}개) ===");
        }

        private static void LogOperatorAnimator(RhythmGame.Result.ManagerResult instance)
        {
            Animator animator = SafeAccess.Get(OperatorAnimatorField, instance);
            if (animator == null)
            {
                ModLog.Msg("[ManagerResultHook] operatorAnimator 필드가 null입니다.");
                return;
            }

            GameObject animObj = animator.gameObject;
            ModLog.Msg($"[ManagerResultHook] operatorAnimator 오브젝트: {animObj.name} (activeSelf={animObj.activeSelf}, activeInHierarchy={animObj.activeInHierarchy})");
            if (animObj.transform.parent != null)
            {
                ModLog.Msg($"[ManagerResultHook] operatorAnimator 부모: {animObj.transform.parent.name}");
                LogHierarchy(animObj.transform.parent, 1);
            }
            else
            {
                LogHierarchy(animObj.transform, 1);
            }
        }

        private static void LogOperatorCharacters(RhythmGame.Result.ManagerResult instance)
        {
            OperatorCharacter[] operators = instance.GetComponentsInChildren<OperatorCharacter>(includeInactive: true);
            if (operators.Length == 0)
            {
                operators = UnityEngine.Object.FindObjectsOfType<OperatorCharacter>();
            }

            ModLog.Msg($"[ManagerResultHook] 씬 내 OperatorCharacter 수: {operators.Length}");
            foreach (OperatorCharacter op in operators)
            {
                ModLog.Msg(
                    $"[ManagerResultHook]   -> Operator 발견: name={op.OperatorName} " +
                    $"(type={op.GetType().Name}, object={op.gameObject.name}, activeSelf={op.gameObject.activeSelf}, activeInHierarchy={op.gameObject.activeInHierarchy})");
                LogHierarchy(op.transform, 1);
            }
        }

        private static int LogMatchingObjects()
        {
            int matchedCount = 0;
            foreach (GameObject obj in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (obj == null) continue;
                string objName = obj.name;
                if (objName.IndexOf("operator", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objName.IndexOf("shii", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    objName.IndexOf("character", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedCount++;
                    ModLog.Msg($"[ManagerResultHook] 매칭 오브젝트: {obj.name} (activeSelf={obj.activeSelf}, activeInHierarchy={obj.activeInHierarchy})");
                }
            }

            return matchedCount;
        }

        private static void LogHierarchy(Transform t, int depth)
        {
            if (t == null || depth > MaxHierarchyDepth) return;
            foreach (Transform child in t)
            {
                ModLog.Msg(
                    $"[ManagerResultHook] {new string(' ', depth * 2)}- {child.name} (activeSelf={child.gameObject.activeSelf}, activeInHierarchy={child.gameObject.activeInHierarchy})");
                LogHierarchy(child, depth + 1);
            }
        }
    }
}
