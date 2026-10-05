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
using sxtg2.Hooks.Audio;
using sxtg2.Loaders;
using sxtg2.Models;
using sxtg2.Processors;

namespace sxtg2.Hooks
{
    /// <summary>
    /// 게임의 private 필드 접근자를 안전하게 만든다. `static readonly FieldRefAccess(...)`를 필드 초기화로 바로 쓰면
    /// 게임 업데이트로 필드 이름이 바뀔 때 TypeInitializationException이 나서 그 클래스를 처음 건드리는 코드(씬 전환 처리 등)까지
    /// 같이 죽는다. 여기서는 실패하면 경고만 남기고 null을 돌려주므로, 사용하는 쪽이 null이면 그 기능만 건너뛰면 된다.
    /// </summary>
    internal static class SafeAccess
    {
        public static AccessTools.FieldRef<T, F> FieldRef<T, F>(string fieldName)
        {
            try
            {
                return AccessTools.FieldRefAccess<T, F>(fieldName);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SafeAccess] {typeof(T).Name}.{fieldName} 필드를 찾지 못해 관련 기능을 건너뜁니다 (게임 업데이트로 바뀌었을 수 있음): {ex.Message}");
                return null;
            }
        }

        /// <summary>접근자가 null(필드를 못 찾음)이면 기본값을, 아니면 필드 값을 돌려준다.</summary>
        public static F Get<T, F>(AccessTools.FieldRef<T, F> accessor, T instance)
        {
            return accessor == null ? default(F) : accessor(instance);
        }
    }

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
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("shortNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> TailNote =
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("tailNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldTexture =
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("holdTexture");

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

                // 커스텀 스프라이트를 쓰는 이미지에만 손댄다. 예전에는 스킨이 없는 모든 노트에도 SetNativeSize를 불러서
                // 게임의 노트 크기 옵션(noteSize)을 무시했다.
                ApplyCustomSprites(__result, noteObject);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteSpriteHook] Generate 후처리 오류: {ex.Message}");
            }
        }

        private static void ApplyCustomSprites(RG_NoteObject noteInstance, GameObject noteObject)
        {
            // 커스텀 스프라이트가 하나도 없으면(스킨 폴더가 비어 있으면) 노트마다 할 일이 없다.
            if (!CustomNoteSpriteLoader.HasAnySprite)
                return;

            string noteType = CustomNoteSpriteLoader.ExtractNoteType(noteObject.name);
            if (ModLog.IsVerbose)
                ModLog.Verbose($"[NoteSpriteHook] 노트 생성: name={noteObject.name}, 추출된 타입={(string.IsNullOrEmpty(noteType) ? "(없음)" : noteType)}");

            // 원본 RG_NoteObject.SetSize와 같은 규칙: 헤드/꼬리는 가로세로, 홀드 몸통 무늬는 가로만 노트 크기를 곱한다.
            ApplyToTarget(SafeAccess.Get(ShortNote, noteInstance), CustomNoteSpriteLoader.GetCustomSpriteForNote(noteObject.name), scaleHeight: true);
            ApplyToTarget(SafeAccess.Get(TailNote, noteInstance), CustomNoteSpriteLoader.GetTailNoteSprite(noteType), scaleHeight: true);
            ApplyToTarget(SafeAccess.Get(HoldTexture, noteInstance), CustomNoteSpriteLoader.GetHoldTextureSprite(noteType), scaleHeight: false);
        }

        private static void ApplyToTarget(RectTransform target, Sprite sprite, bool scaleHeight)
        {
            if (sprite == null || target == null)
                return;

            var image = target.GetComponent<Image>();
            if (image == null)
                return;

            image.sprite = sprite;

            // 스프라이트 원본 크기로 맞춘 뒤, 원본이 SetTiming에서 곱해 둔 노트 크기 옵션(noteSize/100)을 다시 곱한다.
            image.SetNativeSize();
            float scale = GetNoteSizeScale();
            Vector2 size = target.sizeDelta;
            target.sizeDelta = new Vector2(size.x * scale, scaleHeight ? size.y * scale : size.y);
            image.SetAllDirty();
        }

        private static float GetNoteSizeScale()
        {
            try
            {
                var userData = UserAccountModule.Instance?.userData;
                return userData != null ? userData.noteSize / 100f : 1f;
            }
            catch
            {
                return 1f;
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
                {
                    // 진폭이 0으로 수렴한 뒤에는 기준 위치로 되돌려 마지막 프레임의 잔여 오프셋이 남지 않게 한다.
                    var current = state.Root.anchoredPosition;
                    if (current.x != state.BaseX)
                        state.Root.anchoredPosition = new Vector2(state.BaseX, current.y);
                    return;
                }

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
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("shortNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldMask =
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("holdMask");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> HoldTexture =
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("holdTexture");
        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> TailNote =
            SafeAccess.FieldRef<RG_NoteObject, RectTransform>("tailNote");
        private static readonly AccessTools.FieldRef<RG_NoteObject, float> HoldTextureYOffset =
            SafeAccess.FieldRef<RG_NoteObject, float>("holdTextureYOffset");

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

                var head = SafeAccess.Get(ShortNote, __instance);
                if (head == null)
                    return;

                float headY = head.anchoredPosition.y * multiplier;
                head.anchoredPosition = new Vector2(head.anchoredPosition.x, headY);

                if (__instance.Duration == 0f)
                    return;

                var mask = SafeAccess.Get(HoldMask, __instance);
                if (mask == null)
                    return;

                // 원본이 방금 세팅한 sizeDelta.y가 (꼬리y - 헤드y)라서 길이만 배율을 걸면 된다.
                float length = mask.sizeDelta.y * multiplier;
                mask.sizeDelta = new Vector2(mask.sizeDelta.x, length);
                mask.anchoredPosition = new Vector2(mask.anchoredPosition.x, headY + length / 2f);

                var texture = SafeAccess.Get(HoldTexture, __instance);
                if (texture != null && HoldTextureYOffset != null)
                {
                    // 마스크를 옮겼으면 무늬가 반대로 밀리지 않도록 원본과 같은 상쇄를 다시 건다.
                    texture.anchoredPosition = new Vector2(
                        texture.anchoredPosition.x,
                        mask.anchoredPosition.y * -1f + HoldTextureYOffset(__instance));
                }

                var tail = SafeAccess.Get(TailNote, __instance);
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

    /// <summary>
    /// 오토플레이: 게임에 원래 있는 ManagerPlay.autoPlay 플래그를 플레이 시작 때 켠다.
    /// 예전에는 private AutoPlayJudge를 RG_PS_Judgement.Update Postfix에서 직접 불렀는데, 그러면 게임이 autoPlay 플래그로
    /// 하는 처리(홀드가 끝날 때 OnLaneKeyUp, 홀드 틱 판정, 키 입력 무시)가 빠져 홀드 뒤에 레인이 눌린 채로 남았고,
    /// 시간 캐시(CurrentTimeSeconds)도 리트라이 때 이전 판 값이 남았다. 플래그를 쓰면 게임 원래 경로 그대로 동작한다.
    /// </summary>
    [HarmonyPatch]
    public static class AutoPlayHook
    {
        [HarmonyPatch(typeof(ManagerPlay), nameof(ManagerPlay.InitializePlayScene))]
        [HarmonyPostfix]
        private static void InitializePlaySceneAutoPlay(ManagerPlay __instance)
        {
            if (!ModLog.EnableAutoPlay)
                return;

            __instance.autoPlay = true;
            ModLog.Msg("[AutoPlay] 게임의 autoPlay 플래그를 켰습니다.");
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
            var targets = new List<MethodBase>();
            if (_eJudgesType == null)
            {
                MelonLogger.Warning("[AllPerfect] EJudges 타입을 찾지 못해 올퍼펙트가 적용되지 않습니다.");
                return targets;
            }

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

            // 상속된 베이스 메서드(PlayWidget.OnGetJudge 등)는 여러 파생 타입에서 같은 메서드로 잡히므로 한 번만 패치한다.
            // MethodInfo는 가져온 타입(ReflectedType)이 다르면 같은 메서드도 서로 다른 객체라, 선언 타입+시그니처 문자열로 구분한다.
            var seen = new HashSet<string>();
            foreach (var typeName in targetTypes)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var methodName in targetMethods)
                {
                    foreach (var m in methods)
                    {
                        if (m.Name != methodName) continue;
                        var paramsInfo = m.GetParameters();
                        if (paramsInfo.Length >= 1 && paramsInfo[0].ParameterType == _eJudgesType &&
                            seen.Add(m.DeclaringType.FullName + "::" + m))
                        {
                            targets.Add(m);
                        }
                    }
                }
            }

            if (targets.Count == 0)
                MelonLogger.Warning("[AllPerfect] 패치할 판정 메서드를 하나도 찾지 못해 올퍼펙트가 적용되지 않습니다 (게임 업데이트로 바뀌었을 수 있음).");
            else
                MelonLogger.Msg($"[AllPerfect] 판정 메서드 {targets.Count}개를 패치합니다.");

            return targets;
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

        /// <summary>
        /// 항상 패치한다. 예전에는 시작 시점에 MaxScore가 기본값이면 패치를 건너뛰었는데,
        /// 그러면 설정을 재로드해 MaxScore를 바꿔도 훅 자체가 없어서 영영 반영되지 않는다.
        /// GetMaxScore()가 실시간으로 읽으므로 기본값일 때 동작은 원본과 완전히 동일하다.
        /// </summary>
        private static bool _prepareLogged;

        private static bool Prepare()
        {
            SaveCustomKeyConfig.EnsureInitialized();

            // Harmony가 대상 메서드마다 Prepare를 부르므로 같은 줄이 세 번 찍혔다. 한 번만 남긴다.
            if (!_prepareLogged)
            {
                _prepareLogged = true;
                MelonLogger.Msg(
                    $"[JudgeScoreMax] 점수 상한 훅 적용 (현재 {SaveCustomKeyConfig.MaxScore:0.###}" +
                    $"{(SaveCustomKeyConfig.IsMaxScoreCustom ? " - 커스텀" : " - 기본값, 원본과 동일 동작")}, " +
                    $"원본 상수 {OriginalMaxScore:0.###}).");
            }

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
            if (!(__instance is CustomTrackData customTrack) || customTrack.ResourceDonor == null)
                return true;

            __result = customTrack.ResourceDonor.GetAudioClip();
            return false;
        }

        [HarmonyPatch(nameof(TrackData.GetLoadingAnimation))]
        [HarmonyPrefix]
        private static bool GetLoadingAnimationPrefix(TrackData __instance, ref VideoClip __result)
        {
            if (!(__instance is CustomTrackData customTrack) || customTrack.ResourceDonor == null)
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
            if (!(__instance is CustomTrackData customTrack) || customTrack.ResourceDonor == null)
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
            if (!(__instance is CustomTrackData customTrack) || customTrack.ResourceDonor == null)
                return true;

            __result = customTrack.ResourceDonor.GetSixtarPatternDirectory(lv, willUseSXGT);
            return false;
        }

        private static bool TryUseCustomJacket(TrackData track, ref Sprite result)
        {
            if (!(track is CustomTrackData customTrack))
                return true;

            // 자켓은 곡마다 한 번만 찾는다. 파일이 없을 때도 결과를 기억해서, 게임이 자켓을 요청할 때마다
            // 파일 9개를 다시 확인하고 같은 경고를 반복하지 않는다.
            if (customTrack.CustomJacket == null && !customTrack.JacketSearched)
            {
                customTrack.JacketSearched = true;
                customTrack.CustomJacket = ThumbnailLoader.LoadThumbnail(
                    customTrack.ID,
                    customTrack.AlbumFolder);

                if (customTrack.CustomJacket == null)
                {
                    MelonLogger.Warning(
                        $"[TrackDataMediaHook] 커스텀 자켓을 찾지 못해 기본 자켓을 사용합니다: {customTrack.DisplayName}");
                }
            }

            if (customTrack.CustomJacket == null)
                return true;

            result = customTrack.CustomJacket;
            return false;
        }
    }
}
