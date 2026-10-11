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
                // 꺼져 있으면 렌더러 복구(SetNativeSize)까지 건너뛰어 원본과 완전히 같게 둔다.
                if (__result == null || !SaveCustomKeyConfig.EnableCustomNotes)
                    return;

                ApplyCustomSkin(__result);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[NoteSpriteHook] Generate 후처리 오류: {ex.Message}");
            }
        }

        /// <summary>노트 하나에 CustomNotes 스프라이트를 입힌다. 플레이 노트와 인벤토리 미리보기 노트가 같이 쓴다.</summary>
        internal static void ApplyCustomSkin(RG_NoteObject note)
        {
            var noteObject = note.gameObject;
            if (noteObject == null)
                return;

            ApplyCustomSprites(note, noteObject);
            NoteRendererRecovery.RecoverNoteRenderer(noteObject);
        }

        private static void ApplyCustomSprites(RG_NoteObject noteInstance, GameObject noteObject)
        {
            string noteType = CustomNoteSpriteLoader.ExtractNoteType(noteObject.name);
            ModLog.Verbose($"[NoteSpriteHook] 노트 생성: name={noteObject.name}, 추출된 타입={(string.IsNullOrEmpty(noteType) ? "(없음)" : noteType)}");

            ApplyToTarget(ShortNote(noteInstance), CustomNoteSpriteLoader.GetCustomSpriteForNote(noteType));
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
    /// 서든(레인 위쪽을 가려 노트가 늦게 보임)·히든(판정선 쪽을 가려 노트가 사라짐).
    /// 노트 루트마다 RectMask2D를 달아 보이는 띠만 남기고 잘라낸다. 알파로 노트를 통째로 숨기면
    /// 홀드 몸통이 가림 경계에 걸쳤을 때 반만 가릴 수 없어서 클리핑을 쓴다.
    /// 판정은 Note.timing만 보므로 정확도 자체에는 영향이 없다.
    ///
    /// 판정선은 프리팹의 앵커 기준점이 아니라 "지금 노트가 그려진 자리 - 아직 남은 낙하 거리"로 잰다.
    /// 앵커 기준점으로 계산하면 판정선이 실제보다 화면 아래쪽(캔버스 y -435, 실제 약 -180)으로 잡혀
    /// 노트가 통째로 안 보였다(2026-10-11 실게임). 경계는 매 프레임 다시 계산하고, 계산에 실패하면 가리지 않는다.
    ///
    /// 페이드 인/아웃(서서히 드러나기/사라지기)은 같은 마스크의 softness로 만든다. UI/Default 셰이더는
    /// 클립 경계에서 안쪽으로 softness/2 거리까지 알파를 0→1로 올리므로, 페이드 구간 길이의 2배를 softness로 준다.
    /// 서든/히든과 페이드 설정을 경계 위치로 합치는 규칙은 LaneCoverBand에 있다.
    /// </summary>
    [HarmonyPatch]
    public static class LaneCoverHook
    {
        private sealed class CoverState
        {
            public RectTransform Root;
            public RectTransform ShortNote;
            public RectMask2D Mask;
            public Vector4 LastPadding;
            public int LastSoftness;
        }

        private sealed class ClipResult
        {
            public Vector4 Padding;
            public int Softness;
        }

        // 가리지 않는 쪽에 주는 패딩. 음수 패딩은 클립 영역을 바깥으로 넓힌다.
        private const float Unbounded = 100000f;
        private static readonly Vector4 NoClip = new Vector4(-Unbounded, -Unbounded, -Unbounded, -Unbounded);

        private static readonly AccessTools.FieldRef<RG_NoteObject, RectTransform> ShortNote =
            AccessTools.FieldRefAccess<RG_NoteObject, RectTransform>("shortNote");

        // RectMask2D가 패딩을 적용하는 캔버스(비공개 프로퍼티). 이 캔버스 좌표계로 계산해야 Unity와 어긋나지 않는다.
        private static readonly Func<RectMask2D, Canvas> MaskCanvas =
            AccessTools.MethodDelegate<Func<RectMask2D, Canvas>>(AccessTools.PropertyGetter(typeof(RectMask2D), "Canvas"));

        private static readonly Dictionary<int, CoverState> States = new Dictionary<int, CoverState>();

        private static float _loggedJudgeY = float.NaN;
        private static int _moveLogCount;
        private static bool _loggedFailure;
        private static bool _dumpedHierarchy;

        // 디버그 오버레이용: 마지막으로 계산한 판정선/경계의 월드 좌표와 그 캔버스.
        private static bool _hasDebug;
        private static Vector3 _debugJudge, _debugAnchorJudge, _debugNear, _debugFar, _debugNearFade, _debugFarFade;
        private static bool _debugHasNear, _debugHasFar, _debugHasNearFade, _debugHasFarFade;
        private static Canvas _debugCanvas;
        private static Texture2D _whiteTex;

        // softness 상한(캔버스 단위). 소실점 근처에서 서서히 구간이 터무니없이 길게 잡혀도 int로 안전하게 넣기 위함.
        private const float MaxSoftness = 20000f;

        public static void Reset()
        {
            States.Clear();
            LaneCache.Clear();
            _loggedJudgeY = float.NaN;
            _moveLogCount = 0;
            _loggedFailure = false;
            _dumpedHierarchy = false;
            _hasDebug = false;
        }

        private static bool Enabled => SaveCustomKeyConfig.LaneCover.Enabled;

        [HarmonyPatch(typeof(NoteGenerator), "Generate")]
        [HarmonyPostfix]
        private static void GeneratePostfix(RG_NoteObject __result)
        {
            if (__result == null || !Enabled)
                return;

            try
            {
                var root = __result.transform as RectTransform;
                var shortNote = ShortNote(__result);
                if (root == null || shortNote == null)
                    return;

                if (!_dumpedHierarchy && SaveCustomKeyConfig.LaneCoverDebug)
                {
                    _dumpedHierarchy = true;
                    DumpHierarchy(root, shortNote);
                }

                // 마스크를 달기 전에 루트 사각형부터 늘린다. 못 늘리면 마스크를 달지 않는다
                // (작은 사각형에 마스크를 달면 Unity가 노트를 통째로 컬링해서 안 보이게 된다).
                if (!TryExpandRootRect(root, out string reason))
                {
                    LogFailureOnce($"노트 구조가 예상과 달라 가림을 적용하지 않습니다: {reason}");
                    return;
                }

                var mask = __result.GetComponent<RectMask2D>();
                if (mask == null)
                    mask = __result.gameObject.AddComponent<RectMask2D>();

                var state = new CoverState { Root = root, ShortNote = shortNote, Mask = mask, LastPadding = new Vector4(float.NaN, 0f, 0f, 0f), LastSoftness = -1 };
                States[__result.GetInstanceID()] = state;
                Apply(state);
            }
            catch (Exception ex)
            {
                LogFailureOnce($"가림 준비 오류: {ex.Message}");
            }
        }

        // 노트 위치가 갱신될 때마다 경계도 다시 맞춘다.
        [HarmonyPatch(typeof(RG_NoteObject), "CalculatePosition")]
        [HarmonyPostfix]
        private static void CalculatePositionPostfix(RG_NoteObject __instance)
        {
            if (!Enabled || !States.TryGetValue(__instance.GetInstanceID(), out var state))
                return;

            try
            {
                Apply(state);
            }
            catch (Exception ex)
            {
                LogFailureOnce($"가림 갱신 오류: {ex.Message}");
            }
        }

        private static void Apply(CoverState state)
        {
            var clip = ComputeClip(state);
            Vector4 padding = clip?.Padding ?? NoClip;
            int softness = clip?.Softness ?? Mathf.RoundToInt(SaveCustomKeyConfig.LaneCoverSoftness);

            // padding을 넣을 때마다 자식들의 클리핑이 다시 계산되므로 실제로 바뀐 경우에만 넣는다.
            if (float.IsNaN(state.LastPadding.x) || (padding - state.LastPadding).sqrMagnitude > 0.25f)
            {
                state.Mask.padding = padding;
                state.LastPadding = padding;
            }

            if (softness != state.LastSoftness)
            {
                state.Mask.softness = new Vector2Int(0, softness);
                state.LastSoftness = softness;
            }
        }

        /// <summary>보이는 띠에 해당하는 RectMask2D 패딩과 흐림 폭. 계산할 수 없으면 null(= 가리지 않음).</summary>
        private static ClipResult ComputeClip(CoverState state)
        {
            RectTransform shortNote = state.ShortNote;
            var space = shortNote.parent as RectTransform;
            Canvas maskCanvas = MaskCanvas(state.Mask);
            if (space == null || maskCanvas == null)
                return null;

            Transform canvasSpace = maskCanvas.transform;
            Canvas rootCanvas = maskCanvas.rootCanvas;
            Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;

            // CalculatePosition은 shortNote.anchoredPosition.y = (Timing - curTime) * 속도로 둔다.
            // 그러니 "지금 노트 그림의 중심 - 아직 남은 낙하 거리"가 노트가 맞는 자리(판정선)다.
            Vector3 upWorld = space.TransformVector(Vector3.up);
            Vector3 headWorld = shortNote.TransformPoint(shortNote.rect.center);
            Vector3 judgeWorld = headWorld - upWorld * shortNote.anchoredPosition.y;

            var band = SaveCustomKeyConfig.LaneCover;
            if (!TryGetLaneDistances(state.Root, cam, judgeWorld, upWorld, band, out var lane))
                return null;

            bool hasHidden = band.HasNear;
            bool hasSudden = band.HasFar;
            Vector3 nearWorld = judgeWorld + upWorld * lane.Near;
            Vector3 farWorld = judgeWorld + upWorld * lane.Far;

            // 여기부터는 RectMask2D가 실제로 쓰는 캔버스·사각형 기준(Clipping.FindCullAndClipWorldRect와 같은 계산).
            float nearY = canvasSpace.InverseTransformPoint(nearWorld).y;
            float farY = canvasSpace.InverseTransformPoint(farWorld).y;
            bool upward = canvasSpace.InverseTransformVector(upWorld).y > 0f;
            bool hasLower = upward ? hasHidden : hasSudden;
            bool hasUpper = upward ? hasSudden : hasHidden;
            float lower = upward ? nearY : farY;
            float upper = upward ? farY : nearY;
            Rect maskRect = state.Mask.canvasRect;

            // RectMask2D는 클립 띠가 자기 사각형과 겹치지 않으면 자식을 전부 컬링한다. 그럴 바엔 가리지 않는다.
            float clipBottom = hasLower ? lower : maskRect.yMin - Unbounded;
            float clipTop = hasUpper ? upper : maskRect.yMax + Unbounded;
            if (clipTop <= maskRect.yMin || clipBottom >= maskRect.yMax || clipTop <= clipBottom)
            {
                LogFailureOnce($"보이는 띠({clipBottom:0.#} ~ {clipTop:0.#})가 노트 사각형({maskRect.yMin:0.#} ~ {maskRect.yMax:0.#})과 겹치지 않아 가리지 않습니다.");
                return null;
            }

            // 경계마다 흐림 폭: 페이드 구간이 있으면 그 길이의 2배(셰이더는 softness/2 안쪽에서 알파 1), 없으면 LaneCoverSoftness.
            Vector3 nearFadeWorld = judgeWorld + upWorld * lane.NearOpaque;
            Vector3 farFadeWorld = judgeWorld + upWorld * lane.FarOpaque;
            bool hiddenFades = band.NearFades;
            bool suddenFades = band.FarFades;
            float hiddenSoftness = hiddenFades
                ? 2f * Mathf.Abs(canvasSpace.InverseTransformPoint(nearFadeWorld).y - nearY)
                : SaveCustomKeyConfig.LaneCoverSoftness;
            float suddenSoftness = suddenFades
                ? 2f * Mathf.Abs(canvasSpace.InverseTransformPoint(farFadeWorld).y - farY)
                : SaveCustomKeyConfig.LaneCoverSoftness;

            // softness는 위아래 경계에 똑같이 걸려서 경계마다 따로 줄 수 없다. 마스크는 노트마다 따로 있으니
            // 노트 머리가 가까운 쪽 경계의 폭을 쓴다. 머리는 한 번에 한 경계 근처에만 있으므로 머리가 드러나고 사라지는 건
            // 정확하다. 두 경계에 동시에 걸칠 만큼 긴 홀드 몸통만 먼 쪽 경계의 흐림 폭이 달라질 수 있다.
            float softness;
            if (!hasSudden)
                softness = hiddenSoftness;
            else if (!hasHidden)
                softness = suddenSoftness;
            else
            {
                float headY = canvasSpace.InverseTransformPoint(headWorld).y;
                softness = Mathf.Abs(headY - nearY) <= Mathf.Abs(headY - farY) ? hiddenSoftness : suddenSoftness;
            }

            RecordDebug(maskCanvas, judgeWorld, space, shortNote,
                nearWorld, farWorld, nearFadeWorld, farFadeWorld,
                hasHidden, hasSudden, hasHidden && hiddenFades, hasSudden && suddenFades);
            LogJudgeLine(cam, lane.JudgeScreenY, band, nearWorld, farWorld, nearFadeWorld, farFadeWorld, shortNote);

            return new ClipResult
            {
                Padding = new Vector4(
                    -Unbounded,
                    hasLower ? lower - maskRect.yMin : -Unbounded,
                    -Unbounded,
                    hasUpper ? maskRect.yMax - upper : -Unbounded),
                Softness = Mathf.RoundToInt(Mathf.Clamp(softness, 0f, MaxSoftness))
            };
        }

        private sealed class LaneDistances
        {
            public int Frame;
            public float Near;        // 판정선 쪽 경계(히든 / 페이드 아웃 끝)
            public float Far;         // 화면 끝 쪽 경계(서든 / 페이드 인 시작)
            public float NearOpaque;  // 페이드 아웃이 시작되는 곳(여기부터 위로는 완전히 보임)
            public float FarOpaque;   // 페이드 인이 끝나는 곳(여기부터 아래로는 완전히 보임)
            public float JudgeScreenY;
        }

        // 레인을 따라 이만큼(로컬 단위) 가도 화면 끝에 안 닿으면 소실점에 수렴하는 것으로 본다.
        private const float MaxLaneDistance = 100000f;
        private static readonly Dictionary<int, LaneDistances> LaneCache = new Dictionary<int, LaneDistances>();

        /// <summary>
        /// 판정선에서 레인을 따라 몇 로컬 단위를 가야 화면에서 band의 각 위치(경계·페이드 끝)가 되는지.
        /// 기어가 원근 카메라 앞 깊숙이(Gear Layer z = 1256) 그려지므로 캔버스 좌표가 아니라 카메라로 투영한
        /// 실제 화면 픽셀로 비율을 잰다. 판정선 ~ 화면 끝(노트가 오는 쪽)이 100%다.
        /// 같은 레인의 노트는 판정선을 공유하므로 레인(노트 루트의 부모)마다 프레임당 한 번만 계산한다.
        /// </summary>
        private static bool TryGetLaneDistances(RectTransform root, Camera cam, Vector3 judgeWorld, Vector3 upWorld,
            LaneCoverBand band, out LaneDistances lane)
        {
            int key = root.parent != null ? root.parent.GetInstanceID() : root.GetInstanceID();
            if (LaneCache.TryGetValue(key, out lane) && lane.Frame == Time.frameCount)
                return true;

            Vector2 judge = RectTransformUtility.WorldToScreenPoint(cam, judgeWorld);
            Vector2 probe = RectTransformUtility.WorldToScreenPoint(cam, judgeWorld + upWorld * 10f) - judge;
            if (Mathf.Abs(probe.y) <= Mathf.Abs(probe.x) || judge.y < 0f || judge.y > Screen.height)
            {
                LogFailureOnce($"판정선을 화면에서 찾지 못해 가리지 않습니다 (판정선 화면 {judge}, 진행 방향 {probe}).");
                return false;
            }

            float sign = Mathf.Sign(probe.y);
            float edge = sign > 0f ? Screen.height : 0f;
            float farthest = ScreenY(cam, judgeWorld, upWorld, MaxLaneDistance);
            if ((farthest - edge) * sign < 0f)
                edge = farthest;
            float span = edge - judge.y;

            float At(float percent) => FindDistance(cam, judgeWorld, upWorld, judge.y + span * (percent / 100f), sign);

            lane = new LaneDistances
            {
                Frame = Time.frameCount,
                Near = At(band.NearEdge),
                Far = At(band.FarEdge),
                NearOpaque = band.NearFades ? At(band.NearOpaque) : 0f,
                FarOpaque = band.FarFades ? At(band.FarOpaque) : 0f,
                JudgeScreenY = Screen.height - judge.y
            };
            LaneCache[key] = lane;
            return true;
        }

        private static float ScreenY(Camera cam, Vector3 origin, Vector3 direction, float distance)
        {
            return RectTransformUtility.WorldToScreenPoint(cam, origin + direction * distance).y;
        }

        // 화면 y가 target이 되는 레인 위 거리. 레인을 따라가면 화면 y가 한 방향으로만 변하므로 이분 탐색한다.
        private static float FindDistance(Camera cam, Vector3 origin, Vector3 direction, float target, float sign)
        {
            float lo = 0f;
            float hi = 1f;
            while ((ScreenY(cam, origin, direction, hi) - target) * sign < 0f && hi < MaxLaneDistance)
                hi *= 2f;

            for (int i = 0; i < 30; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if ((ScreenY(cam, origin, direction, mid) - target) * sign < 0f)
                    lo = mid;
                else
                    hi = mid;
            }
            return (lo + hi) * 0.5f;
        }

        // 노트 루트 사각형의 세로 길이(로컬 단위). 레인 전체보다 충분히 길면 된다.
        private const float ExpandedHeight = 20000f;

        /// <summary>
        /// RectMask2D.PerformClipping은 클립 띠가 마스크 오브젝트 자신의 사각형과 겹치지 않으면 자식을 전부 컬링한다.
        /// 노트 루트의 사각형은 레인 바닥의 작은 점이라 그대로 두면 노트가 통째로 사라진다(2026-10-11 "블라인드").
        /// 그래서 루트 사각형을 세로로 크게 늘리되, 피벗을 함께 옮겨 자식들의 앵커 기준점(= 높이 × (앵커 - 피벗))이
        /// 그대로 유지되게 한다. 자식 위치·크기와 루트 자신의 위치는 변하지 않는다.
        /// </summary>
        private static bool TryExpandRootRect(RectTransform root, out string reason)
        {
            reason = null;
            if (Mathf.Abs(root.anchorMin.y - root.anchorMax.y) > 0.0001f)
            {
                // 세로로 늘어나는 앵커면 피벗을 옮길 때 루트 자체가 움직인다.
                reason = $"노트 루트 앵커가 세로로 늘어남 ({root.anchorMin.y}~{root.anchorMax.y})";
                return false;
            }

            var graphic = root.GetComponent<Graphic>();
            if (graphic != null && graphic.enabled)
            {
                // 루트 자신이 그림을 그리면 사각형을 늘리는 순간 그 그림도 커진다.
                reason = $"노트 루트에 그림 컴포넌트가 있음 ({graphic.GetType().Name})";
                return false;
            }

            float anchorY = float.NaN;
            foreach (Transform child in root)
            {
                if (!(child is RectTransform rt))
                    continue;
                if (Mathf.Abs(rt.anchorMin.y - rt.anchorMax.y) > 0.0001f)
                {
                    reason = $"{rt.name}의 앵커가 세로로 늘어남";
                    return false;
                }
                if (float.IsNaN(anchorY))
                    anchorY = rt.anchorMin.y;
                else if (Mathf.Abs(anchorY - rt.anchorMin.y) > 0.0001f)
                {
                    reason = $"자식마다 세로 앵커가 다름 ({anchorY} / {rt.name} {rt.anchorMin.y})";
                    return false;
                }
            }

            if (float.IsNaN(anchorY))
            {
                reason = "노트 루트에 자식이 없음";
                return false;
            }

            float oldHeight = root.rect.height;
            if (oldHeight >= ExpandedHeight)
                return true;

            float newPivotY = anchorY - oldHeight / ExpandedHeight * (anchorY - root.pivot.y);
            root.pivot = new Vector2(root.pivot.x, newPivotY);
            root.sizeDelta = new Vector2(root.sizeDelta.x, root.sizeDelta.y + (ExpandedHeight - oldHeight));
            return true;
        }

        private static void RecordDebug(Canvas canvas, Vector3 judgeWorld, RectTransform space, RectTransform shortNote,
            Vector3 nearWorld, Vector3 farWorld, Vector3 nearFadeWorld, Vector3 farFadeWorld,
            bool hasNear, bool hasFar, bool hasNearFade, bool hasFarFade)
        {
            if (!SaveCustomKeyConfig.LaneCoverDebug)
                return;

            Rect spaceRect = space.rect;
            float anchorY = spaceRect.yMin + spaceRect.height * Mathf.Lerp(shortNote.anchorMin.y, shortNote.anchorMax.y, shortNote.pivot.y);
            _debugCanvas = canvas;
            _debugJudge = judgeWorld;
            _debugAnchorJudge = space.TransformPoint(new Vector3(0f, anchorY, 0f));
            _debugNear = nearWorld;
            _debugFar = farWorld;
            _debugNearFade = nearFadeWorld;
            _debugFarFade = farFadeWorld;
            _debugHasNear = hasNear;
            _debugHasFar = hasFar;
            _debugHasNearFade = hasNearFade;
            _debugHasFarFade = hasFarFade;
            _hasDebug = true;
        }

        /// <summary>
        /// LaneCoverDebug=1이면 계산한 판정선(빨강)·옛 앵커 기준 판정선(노랑)·가림 경계(하늘색)·서서히 구간 끝(연두)을
        /// 화면에 가로선으로 그린다. 스크린샷 한 장으로 계산이 실제 판정선과 맞는지 확인하기 위한 것.
        /// </summary>
        public static void DrawDebug()
        {
            if (!SaveCustomKeyConfig.LaneCoverDebug || !_hasDebug || _debugCanvas == null)
                return;

            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            Canvas root = _debugCanvas.rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            DrawLine(cam, _debugJudge, new Color(1f, 0.15f, 0.15f, 0.95f), "판정선(계산)");
            DrawLine(cam, _debugAnchorJudge, new Color(1f, 0.9f, 0.1f, 0.95f), "판정선(앵커, 옛 계산)");
            var edgeColor = new Color(0.2f, 0.9f, 1f, 0.95f);
            var fadeColor = new Color(0.55f, 1f, 0.35f, 0.95f);
            if (_debugHasNear)
                DrawLine(cam, _debugNear, edgeColor, "아래 가림 경계(히든 / 페이드 아웃 끝)");
            if (_debugHasFar)
                DrawLine(cam, _debugFar, edgeColor, "위 가림 경계(서든 / 페이드 인 시작)");
            if (_debugHasNearFade)
                DrawLine(cam, _debugNearFade, fadeColor, "페이드 아웃 시작");
            if (_debugHasFarFade)
                DrawLine(cam, _debugFarFade, fadeColor, "페이드 인 끝");
        }

        private static void DrawLine(Camera cam, Vector3 world, Color color, string label)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            float y = Screen.height - screen.y;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, y - 1f, Screen.width, 3f), _whiteTex);
            GUI.Label(new Rect(8f, y - 20f, 400f, 20f), $"{label}  (화면 y {y:0})");
            GUI.color = Color.white;
        }

        // 처음 적용할 때와, 판정선이 눈에 띄게 움직였을 때만 남긴다(씬마다 움직임 로그는 4줄까지). 좌표는 화면 위에서부터의 픽셀.
        private static void LogJudgeLine(Camera cam, float judgeScreenY, LaneCoverBand cover, Vector3 nearWorld, Vector3 farWorld,
            Vector3 nearFadeWorld, Vector3 farFadeWorld, RectTransform shortNote)
        {
            if (!float.IsNaN(_loggedJudgeY) && (Mathf.Abs(judgeScreenY - _loggedJudgeY) < 20f || _moveLogCount >= 4))
                return;

            float ScreenTopY(Vector3 world) => Screen.height - RectTransformUtility.WorldToScreenPoint(cam, world).y;
            string band = $"아래 경계 y {ScreenTopY(nearWorld):0}, 위 경계 y {ScreenTopY(farWorld):0}";
            if (cover.NearFades)
                band += $", 페이드 아웃 시작 y {ScreenTopY(nearFadeWorld):0}";
            if (cover.FarFades)
                band += $", 페이드 인 끝 y {ScreenTopY(farFadeWorld):0}";
            if (float.IsNaN(_loggedJudgeY))
            {
                ModLog.Msg($"[LaneCover] 서든 {SaveCustomKeyConfig.SuddenPercent:0.#}% / 히든 {SaveCustomKeyConfig.HiddenPercent:0.#}% / " +
                    $"페이드 인 {(SaveCustomKeyConfig.EnableFadeIn ? "켜짐" : "꺼짐")} / 페이드 아웃 {(SaveCustomKeyConfig.EnableFadeOut ? "켜짐" : "꺼짐")} 적용: {cover} " +
                    $"(화면 {Screen.width}x{Screen.height}, 판정선 y {judgeScreenY:0}, {band}, " +
                    $"노트 셰이더 {shortNote.GetComponent<Image>()?.materialForRendering?.shader?.name ?? "(없음)"})");
            }
            else
            {
                _moveLogCount++;
                ModLog.Msg($"[LaneCover] 판정선이 움직임: 화면 y {_loggedJudgeY:0} → {judgeScreenY:0} ({band})");
            }
            _loggedJudgeY = judgeScreenY;
        }

        // 첫 노트의 shortNote부터 루트 캔버스까지 배치 정보를 한 번 남긴다(좌표 문제 진단용).
        private static void DumpHierarchy(RectTransform root, RectTransform shortNote)
        {
            try
            {
                ModLog.Msg($"[LaneCover] 진단: 화면 {Screen.width}x{Screen.height}, shortNote.anchoredPosition {shortNote.anchoredPosition}, shortNote 그림 중심(로컬) {shortNote.rect.center}");
                foreach (Transform child in root)
                {
                    if (child is RectTransform c)
                        ModLog.Msg($"[LaneCover]   루트 자식 {c.name}: anchor {c.anchorMin}~{c.anchorMax}, pivot {c.pivot}, rect {c.rect}, active {c.gameObject.activeSelf}");
                }

                Transform t = shortNote;
                for (int depth = 0; t != null && depth < 14; depth++, t = t.parent)
                {
                    string info = $"{new string(' ', depth * 2)}- {t.name}: localPos {t.localPosition}, scale {t.localScale}";
                    if (t is RectTransform rt)
                        info += $", rect {rt.rect}, anchor {rt.anchorMin}~{rt.anchorMax}, pivot {rt.pivot}";
                    var canvas = t.GetComponent<Canvas>();
                    if (canvas != null)
                    {
                        info += $", [Canvas {canvas.renderMode}, scaleFactor {canvas.scaleFactor}, camera {(canvas.worldCamera != null ? canvas.worldCamera.name : "없음")}" +
                            $"{(canvas.worldCamera != null && canvas.worldCamera.targetTexture != null ? " → RenderTexture" : "")}]";
                    }
                    if (t.GetComponent<RectMask2D>() != null)
                        info += ", [RectMask2D]";
                    if (t.GetComponent<Mask>() != null)
                        info += ", [Mask]";
                    ModLog.Msg($"[LaneCover]   {info}");
                    if (canvas != null && canvas.isRootCanvas)
                        break;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[LaneCover] 진단 로그 실패: {ex.Message}");
            }
        }

        private static void LogFailureOnce(string message)
        {
            if (_loggedFailure)
                return;
            _loggedFailure = true;
            MelonLogger.Warning($"[LaneCover] {message}");
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

        /// <summary>
        /// 항상 패치한다. 예전에는 시작 시점에 MaxScore가 기본값이면 패치를 건너뛰었는데,
        /// 그러면 설정을 재로드해 MaxScore를 바꿔도 훅 자체가 없어서 영영 반영되지 않는다.
        /// GetMaxScore()가 실시간으로 읽으므로 기본값일 때 동작은 원본과 완전히 동일하다.
        /// </summary>
        private static bool Prepare()
        {
            SaveCustomKeyConfig.EnsureInitialized();

            MelonLogger.Msg(
                $"[JudgeScoreMax] 점수 상한 훅 적용 (현재 {SaveCustomKeyConfig.MaxScore:0.###}" +
                $"{(SaveCustomKeyConfig.IsMaxScoreCustom ? " - 커스텀" : " - 기본값, 원본과 동일 동작")}, " +
                $"원본 상수 {OriginalMaxScore:0.###}).");
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
