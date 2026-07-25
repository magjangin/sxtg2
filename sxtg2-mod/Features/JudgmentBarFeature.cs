using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
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

        private static Texture2D _whiteTex;
        private static GUIStyle _labelStyle;

        public static void RegisterHit(float gapInSeconds)
        {
            try
            {
                float offsetMs = gapInSeconds * 1000f;

                Color tickColor;
                float absOffset = Math.Abs(offsetMs);

                if (absOffset <= 30f)
                {
                    tickColor = new Color(1f, 0.82f, 0f); // Perfect: Gold
                }
                else if (absOffset <= 70f)
                {
                    tickColor = new Color(0.25f, 0.85f, 0.3f); // Great: Green
                }
                else
                {
                    if (offsetMs > 0f)
                    {
                        tickColor = new Color(0.2f, 0.6f, 0.95f); // Fast: Cyan/Blue
                    }
                    else
                    {
                        tickColor = new Color(0.95f, 0.25f, 0.25f); // Slow: Red
                    }
                }

                _lastHitOffsetMs = offsetMs;
                _lastHitTime = Time.time;
                _lastHitColor = tickColor;

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

                float maxMsRange = 150f;
                float scale = (isVertical ? (barH / 2f) : (barW / 2f)) / maxMsRange;

                // 1. 전체 배경 트랙 (±150ms 범위)
                DrawColorRect(new Rect(centerX - barW / 2f, centerY - barH / 2f, barW, barH), new Color(0.08f, 0.08f, 0.08f, 0.65f));

                // 2. Great 범위 박스 시각화 (±70ms)
                float greatSize = 70f * 2f * scale;
                if (isVertical)
                    DrawColorRect(new Rect(centerX - barW / 2f + 1f, centerY - greatSize / 2f, barW - 2f, greatSize), new Color(0.6f, 0.55f, 0.15f, 0.2f));
                else
                    DrawColorRect(new Rect(centerX - greatSize / 2f, centerY - barH / 2f + 1f, greatSize, barH - 2f), new Color(0.6f, 0.55f, 0.15f, 0.2f));

                // 3. Perfect 범위 박스 시각화 (±30ms)
                float perfectSize = 30f * 2f * scale;
                if (isVertical)
                    DrawColorRect(new Rect(centerX - barW / 2f + 1f, centerY - perfectSize / 2f, barW - 2f, perfectSize), new Color(0.15f, 0.65f, 0.75f, 0.3f));
                else
                    DrawColorRect(new Rect(centerX - perfectSize / 2f, centerY - barH / 2f + 1f, perfectSize, barH - 2f), new Color(0.15f, 0.65f, 0.75f, 0.3f));

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
                    string tag = Math.Abs(_lastHitOffsetMs) <= 30f ? "PERFECT" : (_lastHitOffsetMs > 0f ? "FAST" : "SLOW");
                    string msText = $"{sign}{_lastHitOffsetMs:F0} ms ({tag})";

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
                        ? new Rect(centerX + barW / 2f + 10f, centerY - 12f, 180f, 24f)
                        : new Rect(centerX - 90f, centerY - barH / 2f - 28f, 180f, 24f);

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

                JudgmentBar.RegisterHit(deltaTime);
                ModLog.Verbose($"[JudgmentBar] 히트 감지: deltaTime={deltaTime:F4}s ({deltaTime * 1000f:F1}ms)");
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[JudgmentBar.Hook] OnGetJudge Postfix 에러: {ex.Message}");
            }
        }
    }
}
