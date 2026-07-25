using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using GameSetting;
using RhythmGame;
using RhythmGame.Play;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;
using sxtg2.Helpers.UI;
using sxtg2.Hooks.Audio;
using sxtg2.Loaders;
using sxtg2.Models;
using sxtg2.Processors;

namespace sxtg2.Hooks
{
    [HarmonyPatch(typeof(ManagerPlay))]
    public static class ManagerPlayHook
    {
        [HarmonyPatch("FetchBMSToModules")]
        [HarmonyPrefix]
        private static void FetchBMSToModulesPrefix(
            ManagerPlay __instance,
            SXGTData _bms,
            TrackData ___playTrack,
            VideoPlayer ___bgaPlayer,
            GameObject ___defaultBGAcanvas)
        {
            if (!(___playTrack is CustomTrackData customTrack))
                return;

            try
            {
                var chart = BmsParser.ParseBmsFileWithStatistics(customTrack.BmsPath);
                if (chart?.Notes == null || chart.Notes.Count == 0)
                {
                    MelonLogger.Warning(
                        $"[ManagerPlayHook] 차트를 읽지 못해 원본 패턴을 유지합니다: {customTrack.BmsPath}");
                }
                else
                {
                    CustomChartInjector.SetParsedChart(chart);
                    CustomChartInjector.InjectBmsNotesToLaneData(_bms);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerPlayHook] 커스텀 차트 주입 실패: {ex}");
            }

            try
            {
                BGMPlayerHook.ResetReplacementFlag();
                BGMPlayerHook.ReplacePlaySceneBGM(__instance.bgm, customTrack.AlbumFolder);

                BGAPlayerHook.ResetReplacementFlag();
                if (UserAccountModule.Instance.userData.bgaMode == BGAMode.ON &&
                    BGAPlayerHook.ReplacePlaySceneBGA(___bgaPlayer, customTrack.AlbumFolder))
                {
                    ___bgaPlayer.gameObject.SetActive(true);
                    ___defaultBGAcanvas.SetActive(false);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerPlayHook] 커스텀 미디어 교체 실패: {ex}");
            }
        }

        [HarmonyPatch("CheckBGMStart")]
        [HarmonyPrefix]
        private static bool CheckBGMStartPrefix(TrackData ___playTrack)
        {
            return !(___playTrack is CustomTrackData) || !BGMPlayerHook.IsLoading();
        }
    }

    [HarmonyPatch(typeof(NoteGenerator))]
    public static class NoteSpriteHook
    {
        private static bool _isInitialized = false;
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> ShortNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("shortNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> TailNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("tailNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldTexture =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("holdTexture");

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            CustomNoteSpriteLoader.Initialize();
            MelonLogger.Msg("[NoteSpriteHook] Initialize() - 자동 HarmonyPatch 적용 상태");
            _isInitialized = true;
        }

        [HarmonyPatch("Generate")]
        [HarmonyPostfix]
        private static void GeneratePostfix(RG_NoteObject __result)
        {
            try
            {
                if (__result == null)
                    return;

                var noteObject = __result.gameObject;
                if (noteObject == null)
                    return;

                ApplyCustomSprites(__result, noteObject);
                NoteRendererRecovery.RecoverNoteRenderer(noteObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteSpriteHook] Generate 후처리 오류: {ex.Message}");
            }
        }

        private static void ApplyCustomSprites(RG_NoteObject noteInstance, GameObject noteObject)
        {
            string noteType = CustomNoteSpriteLoader.ExtractNoteType(noteObject.name);
            ModLog.Verbose($"[NoteSpriteHook] 노트 생성: name={noteObject.name}, 추출된 타입={(string.IsNullOrEmpty(noteType) ? "(없음)" : noteType)}");

            ApplyToTarget(ShortNote(noteInstance), CustomNoteSpriteLoader.GetCustomSpriteForNote(noteObject.name));
            ApplyToTarget(TailNote(noteInstance), CustomNoteSpriteLoader.GetTailNoteSprite(noteType));
            ApplyToTarget(HoldTexture(noteInstance), CustomNoteSpriteLoader.GetHoldTextureSprite(noteType));
        }

        private static void ApplyToTarget(RectTransform target, Sprite sprite)
        {
            if (sprite == null || target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = sprite;
            }
        }
    }

    [HarmonyPatch]
    public static class AutoPlayHook
    {
        public static float CurrentTimeSeconds = -1f;
        public static bool IsPlayScene = false;

        private static Action<RG_PS_Judgement, float, int> _autoPlayJudge;
        private static FieldInfo _noteJudgeCursorField;
        private static FieldInfo _numLanesField;

        [HarmonyPatch(typeof(ManagerPlay), "CheckGameFinished", new[] { typeof(float) })]
        [HarmonyPrefix]
        private static void CheckGameFinished_Prefix(float __0)
        {
            CurrentTimeSeconds = __0;
        }

        [HarmonyPatch(typeof(RG_PS_Judgement), "Update")]
        [HarmonyPostfix]
        private static void RG_PS_Judgement_Update_Postfix(RG_PS_Judgement __instance)
        {
            if (!ModLog.EnableAutoPlay || !IsPlayScene)
                return;

            float curTime = CurrentTimeSeconds;
            if (curTime < 0f)
                return;

            try
            {
                EnsureCaches();
                if (_autoPlayJudge == null)
                    return;

                int laneCount = GetLaneCount(__instance);
                for (int lane = 0; lane < laneCount; lane++)
                {
                    _autoPlayJudge(__instance, curTime, lane);
                }
            }
            catch
            {
                // 플레이 중 프레임 예외 방지
            }
        }

        private static void EnsureCaches()
        {
            if (_autoPlayJudge == null)
            {
                var m = AccessTools.Method(typeof(RG_PS_Judgement), "AutoPlayJudge", new[] { typeof(float), typeof(int) });
                if (m != null)
                    _autoPlayJudge = AccessTools.MethodDelegate<Action<RG_PS_Judgement, float, int>>(m);
            }

            if (_noteJudgeCursorField == null)
                _noteJudgeCursorField = AccessTools.Field(typeof(RG_PS_Judgement), "noteJudgeCursor");
            if (_numLanesField == null)
                _numLanesField = AccessTools.Field(typeof(RG_PS_Judgement), "numLanes");
        }

        private static int GetLaneCount(RG_PS_Judgement instance)
        {
            try
            {
                if (_noteJudgeCursorField != null)
                {
                    if (_noteJudgeCursorField.GetValue(instance) is IList list && list.Count > 0)
                        return list.Count;
                }

                if (_numLanesField != null)
                {
                    int n = (int)_numLanesField.GetValue(instance);
                    if (n > 0 && n <= 10) return n;
                }
            }
            catch { }

            return 10;
        }
    }

    [HarmonyPatch]
    public static class AllPerfectJudgeHook
    {
        private static Type _eJudgesType;
        private static object _bluestarValue;

        private static void InitializeEJudges()
        {
            if (_eJudgesType != null) return;
            try
            {
                _eJudgesType = AccessTools.TypeByName("EJudges");
                if (_eJudgesType != null)
                {
                    _bluestarValue = Enum.ToObject(_eJudgesType, 0);
                }
            }
            catch { }
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            InitializeEJudges();
            if (_eJudgesType == null)
                yield break;

            string[] targetTypes = {
                "RhythmGame.Play.RG_PS_Judgement",
                "JudgeCounter",
                "JudgeTextViewer",
                "RedStarCounter",
                "FastSlowMeter"
            };

            string[] targetMethods = {
                "JudgeAction",
                "JudgeDivergence",
                "TryJudgeShortNote",
                "AddJudge",
                "OnGetJudge"
            };

            foreach (var typeName in targetTypes)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                foreach (var methodName in targetMethods)
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var m in methods)
                    {
                        if (m.Name != methodName) continue;
                        var paramsInfo = m.GetParameters();
                        if (paramsInfo.Length >= 1 && paramsInfo[0].ParameterType == _eJudgesType)
                        {
                            yield return m;
                        }
                    }
                }
            }
        }

        [HarmonyPrefix]
        private static bool Prefix(ref object __0)
        {
            if (!ModLog.EnableAllPerfect) return true;

            InitializeEJudges();
            if (_eJudgesType != null && _bluestarValue != null && __0 != null && __0.GetType() == _eJudgesType)
            {
                __0 = _bluestarValue;
            }

            return true;
        }
    }

    [HarmonyPatch]
    public static class ResultSaveBlockHook
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("RhythmGame.Result.ManagerResult");
            if (type == null) yield break;

            var m1 = AccessTools.Method(type, "PostRequestPlayResult");
            if (m1 != null) yield return m1;

            var m2 = AccessTools.Method(type, "ComparePlayResultHighScore");
            if (m2 != null) yield return m2;
        }

        [HarmonyPrefix]
        private static bool Prefix(MethodBase __originalMethod)
        {
            bool shouldBlock = ModLog.BlockSaveBestRanking || ModLog.EnableAutoPlay || ModLog.EnableAllPerfect;

            if (shouldBlock)
            {
                ModLog.Msg($"[차단] 하이스코어 및 랭킹 저장 차단: {__originalMethod?.DeclaringType?.Name}.{__originalMethod?.Name}");
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(TrackData))]
    public static class TrackDataMediaHook
    {
        [HarmonyPatch(nameof(TrackData.GetJacketSprite), new Type[] { })]
        [HarmonyPrefix]
        private static bool GetJacketSpritePrefix(TrackData __instance, ref Sprite __result)
        {
            return TryUseCustomJacket(__instance, ref __result);
        }

        [HarmonyPatch(nameof(TrackData.GetThumbSprite))]
        [HarmonyPrefix]
        private static bool GetThumbSpritePrefix(TrackData __instance, ref Sprite __result)
        {
            return TryUseCustomJacket(__instance, ref __result);
        }

        [HarmonyPatch(nameof(TrackData.GetAudioClip), new Type[] { })]
        [HarmonyPrefix]
        private static bool GetAudioClipPrefix(TrackData __instance, ref AudioClip __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetAudioClip();
            return false;
        }

        [HarmonyPatch(nameof(TrackData.GetLoadingAnimation))]
        [HarmonyPrefix]
        private static bool GetLoadingAnimationPrefix(TrackData __instance, ref VideoClip __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetLoadingAnimation();
            return false;
        }

        [HarmonyPatch(nameof(TrackData.GetSixtarPatternDirectory), new[] { typeof(ELevels), typeof(EPlayStyle), typeof(bool) })]
        [HarmonyPrefix]
        private static bool GetPatternDirectoryPrefix(
            TrackData __instance,
            ELevels lv,
            EPlayStyle ps,
            bool willUseSXGT,
            ref string __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetSixtarPatternDirectory(lv, ps, willUseSXGT);
            return false;
        }

        [HarmonyPatch(nameof(TrackData.GetSixtarPatternDirectory), new[] { typeof(string), typeof(bool) })]
        [HarmonyPrefix]
        private static bool GetPatternDirectoryByLevelPrefix(
            TrackData __instance,
            string lv,
            bool willUseSXGT,
            ref string __result)
        {
            if (!(__instance is CustomTrackData customTrack))
                return true;

            __result = customTrack.ResourceDonor.GetSixtarPatternDirectory(lv, willUseSXGT);
            return false;
        }

        private static bool TryUseCustomJacket(TrackData track, ref Sprite result)
        {
            if (!(track is CustomTrackData customTrack))
                return true;

            if (customTrack.CustomJacket == null)
            {
                customTrack.CustomJacket = ThumbnailLoader.LoadThumbnail(
                    customTrack.ID,
                    customTrack.AlbumFolder);
            }

            if (customTrack.CustomJacket == null)
            {
                MelonLogger.Warning(
                    $"[TrackDataMediaHook] 커스텀 자켓을 찾지 못해 기본 자켓을 사용합니다: {customTrack.DisplayName}");
                return true;
            }

            result = customTrack.CustomJacket;
            return false;
        }
    }
}
