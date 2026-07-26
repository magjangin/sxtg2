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
        private static float _lastHitOffsetMs = 0f;
        private static float _lastHitTime = -999f;
        private static Color _lastHitColor = Color.white;
        private static int _lastHitJudge = -1;

        // EJudges 순서: BLUESTAR / WHITESTAR / YELLOWSTAR / REDSTAR
        private static readonly string[] JudgeNames = { "BLUESTAR", "WHITESTAR", "YELLOWSTAR", "REDSTAR" };

        // 게임의 RG_PS_Judgement.JudgeRange(초)를 ms로 캐시. 초기값은 SuperNova/Quasar 기준 폴백.
        private static readonly float[] RangeMs = { 36f, 85f, 136f, 187f };

        private static Texture2D _whiteTex;
        private static GUIStyle _labelStyle;

        /// <summary>난이도별로 다른 실제 판정 범위를 게임에서 읽어온다.</summary>
        public static void RefreshJudgeRange()
        {
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
            catch
            {
                // 판정 모듈이 아직 준비되지 않은 프레임은 폴백 값 유지
            }
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

        public static void RegisterHit(float gapInSeconds, int judgeIndex)
        {
            try
            {
                float offsetMs = gapInSeconds * 1000f;
                Color tickColor = JudgeColor(judgeIndex);

                _lastHitOffsetMs = offsetMs;
                _lastHitTime = Time.time;
                _lastHitColor = tickColor;
                _lastHitJudge = judgeIndex;

                HitHistory.Add(new HitTick
                {
                    offsetMs = offsetMs,
                    timeAdded = Time.time,
                    color = tickColor
                });
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[JudgmentBar] 히트 데이터 등록 중 에러: {ex.Message}");
            }
        }

        public static void DrawJudgmentBar()
        {
            if (!SaveCustomKeyConfig.EnableJudgmentBar)
                return;

            try
            {
                float duration = 1.5f;
                HitHistory.RemoveAll(tick => Time.time - tick.timeAdded > duration);

                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }

                float screenWidth = Screen.width;
                float screenHeight = Screen.height;

                bool isVertical = SaveCustomKeyConfig.JudgmentBarVertical;
                float barW = isVertical ? 24f : 300f;
                float barH = isVertical ? 300f : 24f;

                float centerX = isVertical ? 60f : screenWidth / 2f;
                float centerY = screenHeight / 2f;

                // 눈금 범위는 게임의 최대 판정 폭(REDSTAR 경계)에 맞춘다.
                float maxMsRange = RangeMs[3];
                float scale = (isVertical ? (barH / 2f) : (barW / 2f)) / maxMsRange;

                // 1. 전체 배경 트랙 (±REDSTAR 범위)
                DrawColorRect(new Rect(centerX - barW / 2f, centerY - barH / 2f, barW, barH), new Color(0.08f, 0.08f, 0.08f, 0.65f));

                // 2~4. 실제 판정 범위 박스 (넓은 등급부터 겹쳐 그림)
                DrawRangeBox(centerX, centerY, barW, barH, isVertical, RangeMs[2] * 2f * scale, new Color(0.65f, 0.55f, 0.12f, 0.18f)); // YELLOWSTAR
                DrawRangeBox(centerX, centerY, barW, barH, isVertical, RangeMs[1] * 2f * scale, new Color(0.75f, 0.75f, 0.75f, 0.20f)); // WHITESTAR
                DrawRangeBox(centerX, centerY, barW, barH, isVertical, RangeMs[0] * 2f * scale, new Color(0.20f, 0.50f, 0.85f, 0.32f)); // BLUESTAR

                // 4. Center Line (0ms)
                if (isVertical)
                    DrawColorRect(new Rect(centerX - barW / 2f - 2f, centerY - 1f, barW + 4f, 2f), Color.white);
                else
                    DrawColorRect(new Rect(centerX - 1f, centerY - barH / 2f - 2f, 2f, barH + 4f), Color.white);

                // 5. 히트 잔상 틱 렌더링
                foreach (var tick in HitHistory)
                {
                    float ms = Mathf.Clamp(tick.offsetMs, -maxMsRange, maxMsRange);
                    float elapsed = Time.time - tick.timeAdded;
                    float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                    Color finalColor = new Color(tick.color.r, tick.color.g, tick.color.b, alpha * 0.9f);

                    if (isVertical)
                    {
                        // 세로 바: 위쪽 = +ms (FAST), 아래쪽 = -ms (SLOW)
                        float tickY = centerY - (ms * scale);
                        DrawColorRect(new Rect(centerX - barW / 2f - 1f, tickY - 1f, barW + 2f, 2f), finalColor);
                    }
                    else
                    {
                        // 가로 바: 오른쪽 = +ms (FAST), 왼쪽 = -ms (SLOW)
                        float tickX = centerX + (ms * scale);
                        DrawColorRect(new Rect(tickX - 1f, centerY - barH / 2f - 1f, 2f, barH + 2f), finalColor);
                    }
                }

                // 6. 실시간 ms 오차 텍스트 출력
                float textElapsed = Time.time - _lastHitTime;
                if (textElapsed < duration)
                {
                    string sign = _lastHitOffsetMs >= 0f ? "+" : "";
                    string judgeName = (_lastHitJudge >= 0 && _lastHitJudge < JudgeNames.Length) ? JudgeNames[_lastHitJudge] : "?";
                    string tag = _lastHitOffsetMs >= 0f ? "FAST" : "SLOW";
                    string msText = $"{sign}{_lastHitOffsetMs:F1} ms · {judgeName} ({tag})";

                    float alpha = Mathf.Clamp01(1f - (textElapsed / duration));

                    if (_labelStyle == null)
                    {
                        _labelStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleLeft
                        };
                    }
                    _labelStyle.fontSize = 16;

                    Rect labelRect = isVertical
                        ? new Rect(centerX + barW / 2f + 10f, centerY - 12f, 260f, 24f)
                        : new Rect(centerX - 90f, centerY - barH / 2f - 28f, 260f, 24f);

                    // 그림자
                    _labelStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.8f);
                    float offset = 1.5f;
                    GUI.Label(new Rect(labelRect.x - offset, labelRect.y - offset, labelRect.width, labelRect.height), msText, _labelStyle);
                    GUI.Label(new Rect(labelRect.x + offset, labelRect.y - offset, labelRect.width, labelRect.height), msText, _labelStyle);
                    GUI.Label(new Rect(labelRect.x - offset, labelRect.y + offset, labelRect.width, labelRect.height), msText, _labelStyle);
                    GUI.Label(new Rect(labelRect.x + offset, labelRect.y + offset, labelRect.width, labelRect.height), msText, _labelStyle);

                    // 메인 텍스트
                    _labelStyle.normal.textColor = new Color(_lastHitColor.r, _lastHitColor.g, _lastHitColor.b, alpha);
                    GUI.Label(labelRect, msText, _labelStyle);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[JudgmentBar] OnGUI 드로우 에러: {ex.Message}");
            }
        }

        private static void DrawRangeBox(float centerX, float centerY, float barW, float barH, bool isVertical, float size, Color color)
        {
            if (isVertical)
                DrawColorRect(new Rect(centerX - barW / 2f + 1f, centerY - size / 2f, barW - 2f, size), color);
            else
                DrawColorRect(new Rect(centerX - size / 2f, centerY - barH / 2f + 1f, size, barH - 2f), color);
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
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

            foreach (var typeName in typeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type == null) continue;

                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name != "OnGetJudge") continue;
                    var parameters = m.GetParameters();
                    if (parameters.Length >= 2 && (parameters[1].ParameterType == typeof(float) || parameters[1].ParameterType == typeof(double)))
                    {
                        yield return m;
                    }
                }
            }
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
                ModLog.Verbose($"[JudgmentBar] 히트 감지: judge={judgeIndex}, deltaTime={deltaTime:F4}s ({deltaTime * 1000f:F1}ms)");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[JudgmentBar.Hook] OnGetJudge Postfix 에러: {ex.Message}");
            }
        }
    }
}
