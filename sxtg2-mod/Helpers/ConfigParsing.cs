using System;
using System.Globalization;
using MelonLoader;

namespace sxtg2.Helpers
{
    /// <summary>
    /// config.txt의 값 문자열을 해석하는 순수 함수 모음. Unity 타입을 쓰지 않으므로
    /// sxtg2.LogicTests가 이 파일을 그대로 링크해 실제 구현을 검증한다(예전에는 테스트가 복사본을 검증했다).
    /// </summary>
    public static class ConfigParsing
    {
        /// <summary>
        /// 경고 출력기. 게임 안에서는 ModLog.Warning(LogLevel 적용)으로 바뀌고, 테스트 프로젝트는 기본값(MelonLogger)을 쓴다.
        /// </summary>
        public static Action<string> Warn = message => MelonLogger.Warning(message);

        /// <summary>
        /// 값 뒤에 붙은 줄 끝 주석(`AutoPlay=1 # 메모`, `Side=Left // 메모`)을 떼어낸다.
        /// `#`/`//`는 앞에 공백이 있을 때만 주석으로 본다. 그래서 `#RRGGBB` 색상 값은 그대로 남는다.
        /// </summary>
        public static string StripInlineComment(string val)
        {
            if (string.IsNullOrEmpty(val))
                return val;

            for (int i = 1; i < val.Length; i++)
            {
                if (!char.IsWhiteSpace(val[i - 1]))
                    continue;

                char c = val[i];
                if (c == '#' || (c == '/' && i + 1 < val.Length && val[i + 1] == '/'))
                    return val.Substring(0, i).TrimEnd();
            }

            return val;
        }

        /// <summary>켜기/끄기 단어를 인식하면 true, 모르는 단어면 false를 돌려준다.</summary>
        public static bool TryParseFlexibleBool(string val, out bool result)
        {
            result = false;
            if (string.IsNullOrEmpty(val))
                return false;

            var s = val.Trim().ToLowerInvariant();

            if (s == "1" || s == "true" || s == "t" || s == "on" || s == "켜짐" || s == "사용" || s == "활성화" || s == "enable" || s == "enabled" || s == "yes" || s == "y" || s == "트루" || s == "참" || s == "켜기")
            {
                result = true;
                return true;
            }

            if (s == "0" || s == "false" || s == "f" || s == "off" || s == "꺼짐" || s == "미사용" || s == "비활성화" || s == "disable" || s == "disabled" || s == "no" || s == "n" || s == "폴스" || s == "거짓" || s == "끄기")
            {
                result = false;
                return true;
            }

            return false;
        }

        /// <summary>모르는 단어는 조용히 기본값을 돌려준다(경고가 필요하면 <see cref="ParseBoolSetting"/>).</summary>
        public static bool ParseFlexibleBool(string val, bool defaultValue)
        {
            return TryParseFlexibleBool(val, out bool parsed) ? parsed : defaultValue;
        }

        /// <summary>켜기/끄기 설정을 읽는다. 값이 있는데 모르는 단어면 경고를 남기고 기본값을 유지한다.</summary>
        public static bool ParseBoolSetting(string key, string val, bool defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            if (TryParseFlexibleBool(val, out bool parsed))
                return parsed;

            Warn($"[SaveCustomKey] {key} 값을 켜기/끄기로 읽지 못했습니다: \"{val}\" → 기본값 {(defaultValue ? 1 : 0)} 유지");
            return defaultValue;
        }

        public static float ParseFloatSetting(string key, string val, float defaultValue, float min, float max)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            if (!float.TryParse(val.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                Warn($"[SaveCustomKey] {key} 값을 숫자로 읽지 못했습니다: \"{val}\" → 기본값 {defaultValue:0.###} 유지");
                return defaultValue;
            }

            if (parsed < min || parsed > max)
            {
                Warn($"[SaveCustomKey] {key}는 {min:0.###} ~ {max:0.###} 범위여야 합니다: {parsed:0.###} → 기본값 {defaultValue:0.###} 유지");
                return defaultValue;
            }

            return parsed;
        }

        public static string ParseSideSetting(string key, string val, string defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            var s = val.Trim().ToLowerInvariant();

            if (s == "left" || s == "l" || s == "왼쪽" || s == "좌" || s == "-1")
                return "Left";

            if (s == "right" || s == "r" || s == "오른쪽" || s == "우" || s == "1")
                return "Right";

            if (s == "center" || s == "c" || s == "중앙" || s == "가운데" || s == "default" || s == "기본" || s == "0")
                return "Center";

            Warn($"[SaveCustomKey] {key} 값을 알 수 없습니다: \"{val}\" (Left/Right/Center 중 하나) → 기본값 {defaultValue} 유지");
            return defaultValue;
        }

        public static int ParseShapeSetting(string key, string val, int defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            var s = val.Trim().ToLowerInvariant();

            if (s == "0" || s == "rect" || s == "rectangle" || s == "square" || s == "사각" || s == "사각형" || s == "false" || s == "off")
                return 0;

            if (s == "1" || s == "capsule" || s == "pill" || s == "알약" || s == "캡슐" || s == "true" || s == "on")
                return 1;

            if (s == "2" || s == "triangle" || s == "diamond" || s == "삼각" || s == "삼각형" || s == "다이아몬드")
                return 2;

            if (s == "-1" || s == "same" || s == "default" || s == "기본" || s == "추종")
                return -1;

            if (int.TryParse(s, out int parsedInt) && parsedInt >= -1 && parsedInt <= 2)
                return parsedInt;

            Warn($"[SaveCustomKey] {key} 모양 설정 값을 알 수 없습니다: \"{val}\" (0=사각, 1=알약, 2=삼각) → 기본값 유지");
            return defaultValue;
        }
    }
}
