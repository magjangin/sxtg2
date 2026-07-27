using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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

    /// <summary>
    /// 노트가 눈송이처럼 좌우로 흔들리며 내려오게 하는 순수 시각 효과.
    /// 판정은 노트의 화면 위치가 아니라 시간(Note.timing)만 보므로 정확도에는 영향이 없다.
    /// </summary>
    [HarmonyPatch(typeof(RG_NoteObject))]
    public static class NoteSwayHook
    {
        private sealed class SwayState
        {
            public RectTransform Root;
            public float BaseX;
            public float Phase;
        }

        private static readonly Dictionary<int, SwayState> States = new Dictionary<int, SwayState>();

        public static void Reset()
        {
            States.Clear();
        }

        /// <summary>
        /// CalculatePosition은 자식(shortNote/holdMask/tailNote)의 y만 세팅하고 루트는 건드리지 않는다.
        /// 그래서 루트를 통째로 밀면 헤드·몸통·꼬리가 한 덩어리로 움직이고, 홀드 몸통과
        /// holdTexture의 상대 위치도 그대로 유지된다(마스크만 옮기면 무늬가 반대로 밀려 보인다).
        /// </summary>
        [HarmonyPatch("CalculatePosition")]
        [HarmonyPostfix]
        private static void CalculatePositionPostfix(RG_NoteObject __instance, float __0)
        {
            if (!SaveCustomKeyConfig.EnableNoteSway)
                return;

            try
            {
                var state = GetOrCreateState(__instance);
                if (state == null || state.Root == null)
                    return;

                float amplitude = SaveCustomKeyConfig.NoteSwayAmplitude;
                if (SaveCustomKeyConfig.NoteSwayDamping)
                {
                    // 판정선에 가까워질수록 진폭을 0으로 수렴시켜 칠 때는 제자리에 있게 한다.
                    float remaining = __instance.Timing - __0;
                    amplitude *= Mathf.Clamp01(remaining / SaveCustomKeyConfig.NoteSwayDampingTime);
                }

                if (amplitude <= 0f)
                    return;

                float angle = __0 * SaveCustomKeyConfig.NoteSwaySpeed * 2f * Mathf.PI + state.Phase;
                float offset = amplitude * Mathf.Sin(angle);

                var pos = state.Root.anchoredPosition;
                state.Root.anchoredPosition = new Vector2(state.BaseX + offset, pos.y);
            }
            catch
            {
                // 플레이 중 프레임 예외 방지
            }
        }

        private static SwayState GetOrCreateState(RG_NoteObject note)
        {
            int id = note.GetInstanceID();
            if (States.TryGetValue(id, out var state))
                return state;

            var root = note.GetComponent<RectTransform>();
            if (root == null)
                return null;

            state = new SwayState
            {
                Root = root,
                BaseX = root.anchoredPosition.x,
                // Timing을 시드로 삼아 노트마다 위상을 흩뿌린다. 결정론적이라 리트라이해도 궤적이 같다.
                Phase = Mathf.Repeat(note.Timing * 12.9898f, 2f * Mathf.PI)
            };
            States[id] = state;
            return state;
        }
    }

    /// <summary>
    /// [챌린지] 노트마다 낙하 속도 배율을 다르게 준다.
    /// 원본은 모든 노트가 같은 noteSpeed를 쓰므로 순서가 절대 뒤집히지 않지만, 배율을 노트별로
    /// 흩뿌리면 노트끼리 서로 추월한다. 읽기 난이도를 올리는 것이 목적인 기능이다.
    /// 판정은 Note.timing만 보므로 정확도 자체에는 영향이 없다.
    /// </summary>
    [HarmonyPatch]
    public static class NoteSpeedChaosHook
    {
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> ShortNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("shortNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldMask =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("holdMask");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldTexture =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("holdTexture");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> TailNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("tailNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, float> HoldTextureYOffset =
            AccessTools.FieldRefAccess<RG_NoteObject, float>("holdTextureYOffset");

        private static readonly Dictionary<int, float> Multipliers = new Dictionary<int, float>();

        public static void Reset()
        {
            Multipliers.Clear();
        }

        /// <summary>
        /// 원본이 계산해 둔 y를 배율만큼 늘린다. 홀드는 헤드·길이에 같은 배율을 걸어야
        /// 몸통이 늘어난 만큼 꼬리도 따라간다.
        /// </summary>
        [HarmonyPatch(typeof(RG_NoteObject), "CalculatePosition")]
        [HarmonyPostfix]
        private static void CalculatePositionPostfix(RG_NoteObject __instance)
        {
            if (!SaveCustomKeyConfig.EnableNoteSpeedChaos)
                return;

            try
            {
                float multiplier = GetMultiplier(__instance);
                if (Mathf.Approximately(multiplier, 1f))
                    return;

                var head = ShortNote(__instance);
                if (head == null)
                    return;

                float headY = head.anchoredPosition.y * multiplier;
                head.anchoredPosition = new Vector2(head.anchoredPosition.x, headY);

                if (__instance.Duration == 0f)
                    return;

                var mask = HoldMask(__instance);
                if (mask == null)
                    return;

                // 원본이 방금 세팅한 sizeDelta.y가 (꼬리y - 헤드y)라서 길이만 배율을 걸면 된다.
                float length = mask.sizeDelta.y * multiplier;
                mask.sizeDelta = new Vector2(mask.sizeDelta.x, length);
                mask.anchoredPosition = new Vector2(mask.anchoredPosition.x, headY + length / 2f);

                var texture = HoldTexture(__instance);
                if (texture != null)
                {
                    // 마스크를 옮겼으면 무늬가 반대로 밀리지 않도록 원본과 같은 상쇄를 다시 건다.
                    texture.anchoredPosition = new Vector2(
                        texture.anchoredPosition.x,
                        mask.anchoredPosition.y * -1f + HoldTextureYOffset(__instance));
                }

                var tail = TailNote(__instance);
                if (tail != null)
                {
                    tail.anchoredPosition = new Vector2(tail.anchoredPosition.x, headY + length);
                }
            }
            catch
            {
                // 플레이 중 프레임 예외 방지
            }
        }

        /// <summary>
        /// 느린 노트는 생성 시점에 이미 화면 안쪽에 있어야 해서 그대로 두면 허공에서 튀어나온다.
        /// NoteGenerator는 속도와 무관하게 고정 3초 전에 노트를 만들므로, 가장 느린 배율만큼
        /// 선행 생성 시간을 늘려준다.
        /// </summary>
        [HarmonyPatch(typeof(NoteGenerator), "Start")]
        [HarmonyPostfix]
        private static void NoteGeneratorStartPostfix(NoteGenerator __instance)
        {
            if (!SaveCustomKeyConfig.EnableNoteSpeedChaos)
                return;

            float slowest = SaveCustomKeyConfig.NoteSpeedChaosMin;
            if (slowest >= 1f)
                return;

            try
            {
                var field = AccessTools.Field(typeof(NoteGenerator), "notePreGenerateTime");
                if (field == null)
                    return;

                float current = (float)field.GetValue(__instance);
                float extended = current / slowest;
                field.SetValue(__instance, extended);
                ModLog.Msg($"[NoteSpeedChaos] 노트 선행 생성 시간을 {current:0.##}초 → {extended:0.##}초로 늘렸습니다 (최저 배율 {slowest:0.##}).");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteSpeedChaos] 선행 생성 시간 조정 실패: {ex.Message}");
            }
        }

        private static float GetMultiplier(RG_NoteObject note)
        {
            int id = note.GetInstanceID();
            if (Multipliers.TryGetValue(id, out float cached))
                return cached;

            // 레인별 모드에서는 같은 레인(같은 NoteGroup 부모)의 노트가 같은 배율을 받는다.
            float seed;
            if (SaveCustomKeyConfig.NoteSpeedChaosPerLane)
                seed = note.transform.parent != null ? note.transform.parent.GetInstanceID() % 1000 : 0f;
            else
                seed = note.Timing;

            float random = Mathf.Abs(Mathf.Sin(seed * 12.9898f) * 43758.5453f);
            random -= Mathf.Floor(random);

            float multiplier = Mathf.Lerp(
                SaveCustomKeyConfig.NoteSpeedChaosMin,
                SaveCustomKeyConfig.NoteSpeedChaosMax,
                random);

            Multipliers[id] = multiplier;
            return multiplier;
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

    /// <summary>
    /// RG_PS_Judgement의 점수 계산에 리터럴로 박혀 있는 만점 상수(1000000f)를
    /// SaveCustomKey/config.txt의 MaxScore 값으로 바꾼다.
    /// 필드(SXGTData.maxScore)가 아니라 메서드 IL에 직접 박힌 값이라 Transpiler로만 교체 가능.
    /// </summary>
    [HarmonyPatch]
    public static class JudgeScoreMaxHook
    {
        /// <summary>Transpiler가 원본 IL에서 찾아 교체할 상수.</summary>
        private const float OriginalMaxScore = 1000000f;

        /// <summary>교체된 IL이 매 프레임 호출한다. 상수를 직접 굽지 않아 설정 로드 순서에 영향받지 않는다.</summary>
        public static float GetMaxScore()
        {
            return SaveCustomKeyConfig.MaxScore;
        }

        private static bool Prepare()
        {
            SaveCustomKeyConfig.EnsureInitialized();

            if (!SaveCustomKeyConfig.IsMaxScoreCustom)
                return false;

            MelonLogger.Msg($"[JudgeScoreMax] 점수 상한을 {SaveCustomKeyConfig.MaxScore:0.###}(으)로 교체합니다 (원본 {OriginalMaxScore:0.###}).");
            return true;
        }

        private static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("RhythmGame.Play.RG_PS_Judgement");
            if (type == null)
            {
                MelonLogger.Warning("[JudgeScoreMax] RG_PS_Judgement 타입을 찾지 못해 점수 상한을 적용하지 못했습니다.");
                yield break;
            }

            var update = AccessTools.Method(type, "Update");
            if (update != null) yield return update;

            var calculate = AccessTools.Method(type, "CalculateJudgeScore", new[] { typeof(float) });
            if (calculate != null) yield return calculate;
        }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            MethodBase __originalMethod)
        {
            var codes = new List<CodeInstruction>(instructions);
            var getter = AccessTools.Method(typeof(JudgeScoreMaxHook), nameof(GetMaxScore));
            int replaced = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                if (code.opcode != OpCodes.Ldc_R4 || !(code.operand is float value))
                    continue;

                if (Math.Abs(value - OriginalMaxScore) > 0.001f)
                    continue;

                // opcode/operand만 갈아끼워 라벨과 예외 블록을 그대로 보존한다.
                code.opcode = OpCodes.Call;
                code.operand = getter;
                replaced++;
            }

            if (replaced == 0)
            {
                MelonLogger.Warning($"[JudgeScoreMax] {__originalMethod?.Name}에서 만점 상수({OriginalMaxScore:0.###})를 찾지 못했습니다. 게임 업데이트로 코드가 바뀌었을 수 있습니다.");
            }
            else
            {
                ModLog.Msg($"[JudgeScoreMax] {__originalMethod?.Name}: 만점 상수 {replaced}곳 교체 완료");
            }

            return codes;
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
