using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using RhythmGame;
using RhythmGame.Play;
using UnityEngine;
using sxtg2.Helpers;

namespace sxtg2.Features
{
    public struct HitTick
    {
        public float offsetMs;
        public float timeAdded;
        public Color color;
    }

    public static class JudgmentBar
    {
        private static readonly List<HitTick> HitHistory = new List<HitTick>();
        private static float _lastHitTime = -999f;
        private static Color _lastHitColor = Color.white;

        /// <summary>마지막 히트의 라벨 문자열. 히트가 들어올 때 한 번만 만들고, 라벨이 보이는 동안 매 그리기마다 새로 만들지 않는다.</summary>
        private static string _lastHitText = "";

        // EJudges 순서: BLUESTAR / WHITESTAR / YELLOWSTAR / REDSTAR
        private static readonly string[] JudgeNames = { "BLUESTAR", "WHITESTAR", "YELLOWSTAR", "REDSTAR" };

        // 게임의 RG_PS_Judgement.JudgeRange(초)를 ms로 캐시. 초기값은 SuperNova/Quasar 기준 폴백.
        private static readonly float[] RangeMs = { 36f, 85f, 136f, 187f };

        private static Texture2D _whiteTex;
        private static GUIStyle _labelStyle;
        private static readonly Dictionary<(int, int), Texture2D> CapsuleTexCache = new Dictionary<(int, int), Texture2D>();
        private static readonly Dictionary<(int, int), Texture2D> TriangleTexCache = new Dictionary<(int, int), Texture2D>();

        /// <summary>캐시에서 밀려난 텍스처. 밀려난 프레임에 이미 그려졌을 수 있어서 다음 OnGUI에서 해제한다.</summary>
        private static readonly List<Texture2D> RetiredTextures = new List<Texture2D>();

        /// <summary>난이도별로 다른 실제 판정 범위를 게임에서 읽어온다.</summary>
        public static void RefreshJudgeRange()
        {
            // 판정바가 꺼져 있으면 눈금 범위를 쓰지 않으므로 매 프레임 게임 객체를 읽을 필요가 없다.
            if (!SaveCustomKeyConfig.EnableJudgmentBar)
                return;

            try
            {
                ManagerPlay manager = ManagerPlay.Instance;
                if (manager == null)
                    return;

                RG_PS_Judgement judgeModule = manager.judgeModule;
                if (judgeModule == null)
                    return;

                var ranges = judgeModule.JudgeRange;
                if (ranges == null || ranges.Count < RangeMs.Length)
                    return;

                for (int i = 0; i < RangeMs.Length; i++)
                {
                    float ms = ranges[i] * 1000f;
                    if (ms > 0f)
                        RangeMs[i] = ms;
                }
            }
            catch (Exception ex)
            {
                // 판정 모듈이 아직 준비되지 않은 프레임은 폴백 값을 유지한다. 원인 확인용으로 상세 로그에만 남긴다.
                ModLog.Verbose($"[JudgmentBar] 판정 범위 읽기 실패(폴백 값 유지): {ex.Message}");
            }
        }

        private static string FormatHitLabel(float offsetMs, int judgeIndex)
        {
            string sign = offsetMs >= 0f ? "+" : "";
            string judgeName = (judgeIndex >= 0 && judgeIndex < JudgeNames.Length) ? JudgeNames[judgeIndex] : "?";
            string tag = offsetMs >= 0f ? "FAST" : "SLOW";
            return $"{sign}{offsetMs:F1} ms · {judgeName} ({tag})";
        }

        private static Color JudgeColor(int judgeIndex)
        {
            switch (judgeIndex)
            {
                case 0: return new Color(0.30f, 0.65f, 1f);    // BLUESTAR
                case 1: return new Color(0.95f, 0.95f, 0.95f); // WHITESTAR
                case 2: return new Color(1f, 0.85f, 0.20f);    // YELLOWSTAR
                case 3: return new Color(0.95f, 0.25f, 0.25f); // REDSTAR
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // 같은 프레임에 같은 판정이 위젯 수만큼 겹쳐 들어오는 것을 걸러내기 위한 기록.
        private static int _lastHitFrame = -1;
        private static float _lastHitGapSeconds;
        private static int _lastHitJudgeIndex = -1;

        public static void RegisterHit(float gapInSeconds, int judgeIndex)
        {
            // 꺼져 있으면 DrawJudgmentBar가 HitHistory를 정리하지 않으므로, 여기서 쌓으면 끝없이 늘어난다.
            if (!SaveCustomKeyConfig.EnableJudgmentBar)
                return;

            // 게임은 판정 하나를 장착한 위젯마다(최대 5개) OnGetJudge로 알리므로 같은 프레임에 같은 값이 여러 번 들어온다.
            // 그대로 쌓으면 틱이 겹쳐 그려져 더 진해 보이고 히트 수가 부풀려진다. 같은 프레임의 같은 (판정, 오차)는 한 번만 받는다.
            // (같은 시각에 두 레인을 동시에 쳐서 완전히 같은 값이 나오는 경우도 같은 위치에 겹쳐 그려지므로 화면은 똑같다.)
            int frame = Time.frameCount;
            if (frame == _lastHitFrame && judgeIndex == _lastHitJudgeIndex && gapInSeconds == _lastHitGapSeconds)
                return;
            _lastHitFrame = frame;
            _lastHitJudgeIndex = judgeIndex;
            _lastHitGapSeconds = gapInSeconds;

            try
            {
                float offsetMs = gapInSeconds * 1000f;
                Color tickColor = JudgeColor(judgeIndex);

                _lastHitTime = Time.time;
                _lastHitColor = tickColor;
                _lastHitText = FormatHitLabel(offsetMs, judgeIndex);

                HitHistory.Add(new HitTick
                {
                    offsetMs = offsetMs,
                    timeAdded = Time.time,
                    color = tickColor
                });
            }
            catch (Exception ex)
            {
                ModLog.WarningThrottled("JudgmentBar.RegisterHit", $"[JudgmentBar] 히트 데이터 등록 중 에러: {ex.Message}");
            }
        }

        /// <summary>틱과 라벨이 서서히 사라지는 시간(초).</summary>
        private const float HitFadeSeconds = 1.5f;

        /// <summary>한 번의 그리기에서 쓰는 판정바 배치(화면 크기, 방향, 위치, 눈금 배율).</summary>
        private struct BarLayout
        {
            public bool IsVertical;
            public bool IsRight;
            public float CenterX;
            public float CenterY;
            public float BarW;
            public float BarH;
            public float MaxMsRange;
            public float Scale;
        }

        public static void DrawJudgmentBar()
        {
            if (!SaveCustomKeyConfig.EnableJudgmentBar)
                return;

            // OnGUI는 프레임당 Layout/Repaint 등 여러 이벤트로 불린다. 실제로 그려지는 건 Repaint뿐이라
            // 나머지 이벤트에서는 정리/텍스트 조립/GUI 호출을 전부 건너뛴다.
            Event guiEvent = Event.current;
            if (guiEvent == null || guiEvent.type != EventType.Repaint)
                return;

            try
            {
                // 상수 필드만 쓰므로 이 람다는 변수를 캡처하지 않아 호출마다 클로저가 할당되지 않는다.
                HitHistory.RemoveAll(tick => Time.time - tick.timeAdded > HitFadeSeconds);

                DestroyRetiredTextures();
                EnsureWhiteTexture();

                BarLayout layout = ComputeLayout();
                int trackShape = SaveCustomKeyConfig.JudgmentBarShape;
                int rangeShape = SaveCustomKeyConfig.JudgmentBarRangeShape == -1 ? trackShape : SaveCustomKeyConfig.JudgmentBarRangeShape;

                DrawTrackAndRanges(layout, trackShape, rangeShape);
                DrawCenterLine(layout);
                DrawHitTicks(layout);
                DrawHitLabel(layout);
            }
            catch (Exception ex)
            {
                ModLog.WarningThrottled("JudgmentBar.Draw", $"[JudgmentBar] OnGUI 드로우 에러: {ex.Message}");
            }
        }

        private static void EnsureWhiteTexture()
        {
            if (_whiteTex != null)
                return;

            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();
        }

        private static BarLayout ComputeLayout()
        {
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            bool isVertical = SaveCustomKeyConfig.JudgmentBarVertical;
            string side = SaveCustomKeyConfig.JudgmentBarSide;
            bool isLeft = side.Equals("Left", StringComparison.OrdinalIgnoreCase);
            bool isRight = side.Equals("Right", StringComparison.OrdinalIgnoreCase);
            float barW = isVertical ? 24f : 300f;
            float barH = isVertical ? 300f : 24f;

            const float edgeMargin = 60f;
            float centerX;
            if (isLeft)
                centerX = isVertical ? edgeMargin : edgeMargin + barW / 2f;
            else if (isRight)
                centerX = isVertical ? (screenWidth - edgeMargin) : (screenWidth - edgeMargin - barW / 2f);
            else // Center(기본값): 세로는 왼쪽 고정, 가로는 화면 정중앙 - 기존 동작 그대로
                centerX = isVertical ? edgeMargin : screenWidth / 2f;

            // 눈금 범위는 게임의 최대 판정 폭(REDSTAR 경계)에 맞춘다.
            float maxMsRange = RangeMs[3];

            return new BarLayout
            {
                IsVertical = isVertical,
                IsRight = isRight,
                CenterX = centerX,
                CenterY = screenHeight / 2f,
                BarW = barW,
                BarH = barH,
                MaxMsRange = maxMsRange,
                Scale = (isVertical ? (barH / 2f) : (barW / 2f)) / maxMsRange
            };
        }

        /// <summary>전체 배경 트랙(±REDSTAR 범위)과 실제 판정 범위 박스(넓은 등급부터 겹쳐 그림).</summary>
        private static void DrawTrackAndRanges(BarLayout l, int trackShape, int rangeShape)
        {
            DrawBarShape(trackShape, new Rect(l.CenterX - l.BarW / 2f, l.CenterY - l.BarH / 2f, l.BarW, l.BarH), new Color(0.08f, 0.08f, 0.08f, 0.65f));

            DrawRangeBox(l.CenterX, l.CenterY, l.BarW, l.BarH, l.IsVertical, RangeMs[2] * 2f * l.Scale, new Color(0.65f, 0.55f, 0.12f, 0.18f), rangeShape); // YELLOWSTAR
            DrawRangeBox(l.CenterX, l.CenterY, l.BarW, l.BarH, l.IsVertical, RangeMs[1] * 2f * l.Scale, new Color(0.75f, 0.75f, 0.75f, 0.20f), rangeShape); // WHITESTAR
            DrawRangeBox(l.CenterX, l.CenterY, l.BarW, l.BarH, l.IsVertical, RangeMs[0] * 2f * l.Scale, new Color(0.20f, 0.50f, 0.85f, 0.32f), rangeShape); // BLUESTAR
        }

        /// <summary>0ms 기준선.</summary>
        private static void DrawCenterLine(BarLayout l)
        {
            if (l.IsVertical)
                DrawColorRect(new Rect(l.CenterX - l.BarW / 2f - 2f, l.CenterY - 1f, l.BarW + 4f, 2f), Color.white);
            else
                DrawColorRect(new Rect(l.CenterX - 1f, l.CenterY - l.BarH / 2f - 2f, 2f, l.BarH + 4f), Color.white);
        }

        /// <summary>히트 잔상 틱.</summary>
        private static void DrawHitTicks(BarLayout l)
        {
            foreach (var tick in HitHistory)
            {
                float ms = Mathf.Clamp(tick.offsetMs, -l.MaxMsRange, l.MaxMsRange);
                float elapsed = Time.time - tick.timeAdded;
                float alpha = Mathf.Clamp01(1f - (elapsed / HitFadeSeconds));
                Color finalColor = new Color(tick.color.r, tick.color.g, tick.color.b, alpha * 0.9f);

                if (l.IsVertical)
                {
                    // 세로 바: 위쪽 = +ms (FAST), 아래쪽 = -ms (SLOW)
                    float tickY = l.CenterY - (ms * l.Scale);
                    DrawColorRect(new Rect(l.CenterX - l.BarW / 2f - 1f, tickY - 1f, l.BarW + 2f, 2f), finalColor);
                }
                else
                {
                    // 가로 바: 오른쪽 = +ms (FAST), 왼쪽 = -ms (SLOW)
                    float tickX = l.CenterX + (ms * l.Scale);
                    DrawColorRect(new Rect(tickX - 1f, l.CenterY - l.BarH / 2f - 1f, 2f, l.BarH + 2f), finalColor);
                }
            }
        }

        /// <summary>마지막 히트의 오차(ms)·등급 텍스트. 그림자 4방향 + 메인 텍스트.</summary>
        private static void DrawHitLabel(BarLayout l)
        {
            float textElapsed = Time.time - _lastHitTime;
            if (textElapsed >= HitFadeSeconds)
                return;

            float alpha = Mathf.Clamp01(1f - (textElapsed / HitFadeSeconds));

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
            }
            _labelStyle.fontSize = 16;

            // 세로 바가 화면 오른쪽에 있으면 라벨이 화면 밖으로 나가므로 바 왼쪽에 붙인다.
            bool labelOnLeftOfBar = l.IsVertical && l.IsRight;
            _labelStyle.alignment = labelOnLeftOfBar ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;

            const float labelWidth = 260f;
            Rect labelRect = l.IsVertical
                ? (labelOnLeftOfBar
                    ? new Rect(l.CenterX - l.BarW / 2f - 10f - labelWidth, l.CenterY - 12f, labelWidth, 24f)
                    : new Rect(l.CenterX + l.BarW / 2f + 10f, l.CenterY - 12f, labelWidth, 24f))
                : new Rect(l.CenterX - 90f, l.CenterY - l.BarH / 2f - 28f, labelWidth, 24f);

            // 그림자
            _labelStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
            float offset = 1.5f;
            GUI.Label(new Rect(labelRect.x - offset, labelRect.y - offset, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x + offset, labelRect.y - offset, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x - offset, labelRect.y + offset, labelRect.width, labelRect.height), _lastHitText, _labelStyle);
            GUI.Label(new Rect(labelRect.x + offset, labelRect.y + offset, labelRect.width, labelRect.height), _lastHitText, _labelStyle);

            // 메인 텍스트
            _labelStyle.normal.textColor = new Color(_lastHitColor.r, _lastHitColor.g, _lastHitColor.b, alpha);
            GUI.Label(labelRect, _lastHitText, _labelStyle);
        }

        private static void DrawRangeBox(float centerX, float centerY, float barW, float barH, bool isVertical, float size, Color color, int shapeType)
        {
            Rect rect = isVertical
                ? new Rect(centerX - barW / 2f + 1f, centerY - size / 2f, barW - 2f, size)
                : new Rect(centerX - size / 2f, centerY - barH / 2f + 1f, size, barH - 2f);

            DrawBarShape(shapeType, rect, color);
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
        }

        /// <summary>shapeType(0=사각, 1=알약 캡슐, 2=삼각/다이아몬드)에 따라 판정 바를 그린다.</summary>
        private static void DrawBarShape(int shapeType, Rect rect, Color color)
        {
            int w = Mathf.RoundToInt(rect.width);
            int h = Mathf.RoundToInt(rect.height);
            if (w < 2 || h < 2 || shapeType == 0)
            {
                DrawColorRect(rect, color);
                return;
            }

            Texture2D tex = null;
            if (shapeType == 1)
                tex = GetCapsuleTexture(w, h);
            else if (shapeType == 2)
                tex = GetTriangleTexture(w, h);

            if (tex == null)
            {
                DrawColorRect(rect, color);
                return;
            }

            GUI.color = color;
            GUI.DrawTexture(rect, tex);
            GUI.color = Color.white;
        }

        /// <summary>캐시를 비우되 텍스처는 바로 해제하지 않고 폐기 목록에 넣는다. Clear만 하면 네이티브 텍스처가 남는다.</summary>
        private static void RetireCache(Dictionary<(int, int), Texture2D> cache)
        {
            foreach (var tex in cache.Values)
            {
                if (tex != null)
                    RetiredTextures.Add(tex);
            }

            cache.Clear();
        }

        /// <summary>밀려난 텍스처를 해제한다. 밀려난 OnGUI가 끝난 뒤의 Repaint에서만 부른다.</summary>
        private static void DestroyRetiredTextures()
        {
            foreach (var tex in RetiredTextures)
                UnityEngine.Object.Destroy(tex);
            RetiredTextures.Clear();
        }

        /// <summary>양끝이 반원인 알약(스타디움) 모양의 알파 마스크 텍스처를 생성/캐시한다.</summary>
        private static Texture2D GetCapsuleTexture(int w, int h)
        {
            var key = (w, h);
            if (CapsuleTexCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            // 판정 범위가 자잘하게 바뀔 때마다 캐시가 무한정 쌓이는 걸 막는 안전장치
            if (CapsuleTexCache.Count > 64)
                RetireCache(CapsuleTexCache);

            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float radius = Mathf.Min(w, h) / 2f;
            float ax, ay, bx, by;
            if (w >= h)
            {
                ax = radius; ay = h / 2f;
                bx = w - radius; by = h / 2f;
            }
            else
            {
                ax = w / 2f; ay = radius;
                bx = w / 2f; by = h - radius;
            }

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f;
                    float signedDist = DistanceToSegment(px, py, ax, ay, bx, by) - radius;
                    float alpha = Mathf.Clamp01(0.5f - signedDist);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            CapsuleTexCache[key] = tex;
            return tex;
        }

        /// <summary>중앙이 가장 넓고 양끝이 뾰족해지는 삼각/다이아몬드 모양의 알파 마스크 텍스처를 생성/캐시한다.</summary>
        private static Texture2D GetTriangleTexture(int w, int h)
        {
            var key = (w, h);
            if (TriangleTexCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            if (TriangleTexCache.Count > 64)
                RetireCache(TriangleTexCache);

            var tex = new Texture2D(w, h, TextureFormat.ARGB32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float cx = w / 2f;
            float cy = h / 2f;
            float halfW = w / 2f;
            float halfH = h / 2f;
            float hyp = Mathf.Sqrt(halfW * halfW + halfH * halfH);

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float py = y + 0.5f;
                float dy = Mathf.Abs(py - cy);
                for (int x = 0; x < w; x++)
                {
                    float px = x + 0.5f;
                    float dx = Mathf.Abs(px - cx);

                    // 다이아몬드/삼각 빗면 직선방정식 기반 부호있는 거리 계산 (halfH * dx + halfW * dy - halfW * halfH = 0)
                    float signedDist = (halfH * dx + halfW * dy - halfW * halfH) / hyp;
                    float alpha = Mathf.Clamp01(0.5f - signedDist);
                    pixels[y * w + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            TriangleTexCache[key] = tex;
            return tex;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            float t = lenSq > 0.0001f ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq) : 0f;
            float cx = ax + t * dx, cy = ay + t * dy;
            float ddx = px - cx, ddy = py - cy;
            return Mathf.Sqrt(ddx * ddx + ddy * ddy);
        }
    }

    [HarmonyPatch]
    public static class FastSlowMeter_OnGetJudge_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            string[] typeNames = {
                "RhythmGame.Play.FastSlowMeter",
                "RhythmGame.Play.JudgeTextViewer",
                "FastSlowMeter",
                "JudgeTextViewer"
            };

            var targets = new List<MethodBase>();
            var seen = new HashSet<string>();
            foreach (var typeName in typeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name != "OnGetJudge") continue;
                    var parameters = m.GetParameters();
                    if (parameters.Length >= 2 && (parameters[1].ParameterType == typeof(float) || parameters[1].ParameterType == typeof(double)) &&
                        seen.Add(m.DeclaringType.FullName + "::" + m))
                    {
                        targets.Add(m);
                    }
                }
            }

            if (targets.Count == 0)
                ModLog.Warning("[JudgmentBar] OnGetJudge 판정 메서드를 하나도 찾지 못해 판정바에 틱이 나오지 않습니다 (게임 업데이트로 바뀌었을 수 있음).");

            return targets;
        }

        [HarmonyPostfix]
        private static void Postfix(object[] __args)
        {
            try
            {
                if (__args == null || __args.Length < 2) return;

                float deltaTime = 0f;
                if (__args[1] is float f)
                {
                    deltaTime = f;
                }
                else if (__args[1] is double d)
                {
                    deltaTime = (float)d;
                }
                else
                {
                    return;
                }

                // __args[0]은 EJudges (0=BLUESTAR ~ 3=REDSTAR)
                int judgeIndex = -1;
                if (__args[0] != null && __args[0].GetType().IsEnum)
                {
                    judgeIndex = Convert.ToInt32(__args[0]);
                }

                JudgmentBar.RegisterHit(deltaTime, judgeIndex);
                if (ModLog.IsVerbose)
                    ModLog.Verbose($"[JudgmentBar] 히트 감지: judge={judgeIndex}, deltaTime={deltaTime:F4}s ({deltaTime * 1000f:F1}ms)");
            }
            catch (Exception ex)
            {
                ModLog.WarningThrottled("JudgmentBar.Hook", $"[JudgmentBar.Hook] OnGetJudge Postfix 에러: {ex.Message}");
            }
        }
    }
}
