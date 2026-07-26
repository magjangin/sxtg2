using System;
using GameSetting;
using UnityEngine;
using sxtg2.Helpers;

namespace sxtg2.Features
{
    public static class KeyViewer
    {
        // 화면 좌 → 우 순서 (LT LL L GATE R RR RT)
        private static readonly SixtarInput[] Lanes =
        {
            SixtarInput.RhythmGame_LT,
            SixtarInput.RhythmGame_LL,
            SixtarInput.RhythmGame_L,
            SixtarInput.RhythmGame_GATE,
            SixtarInput.RhythmGame_R,
            SixtarInput.RhythmGame_RR,
            SixtarInput.RhythmGame_RT
        };

        private static readonly KeyCode[] Keys = new KeyCode[Lanes.Length];
        private static readonly string[] Labels = new string[Lanes.Length];
        private static readonly bool[] Pressed = new bool[Lanes.Length];

        private static bool _bound;

        private static Texture2D _whiteTex;
        private static GUIStyle _keyStyle;

        /// <summary>씬이 바뀌면 키 바인딩을 다시 읽는다.</summary>
        public static void Reset()
        {
            _bound = false;
            for (int i = 0; i < Lanes.Length; i++)
            {
                Keys[i] = KeyCode.None;
                Labels[i] = "-";
                Pressed[i] = false;
            }
        }

        public static void Poll()
        {
            if (!SaveCustomKeyConfig.EnableKeyViewer)
                return;

            try
            {
                if (!_bound)
                    TryBindKeys();

                for (int i = 0; i < Lanes.Length; i++)
                {
                    KeyCode kc = Keys[i];
                    Pressed[i] = kc != KeyCode.None && Input.GetKey(kc);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[KeyViewer] 입력 폴링 에러: {ex.Message}");
            }
        }

        private static void TryBindKeys()
        {
            UserAccountModule module = UserAccountModule.Instance;
            if (module == null)
                return;

            SXGT_UserData userData = module.userData;
            if (userData == null)
                return;

            SixtarKeySetting keySetting = userData.keySetting;
            if (keySetting == null)
                return;

            for (int i = 0; i < Lanes.Length; i++)
            {
                Keys[i] = keySetting.GetKeyFromLane(Lanes[i]);
                Labels[i] = ToLabel(Keys[i]);
            }

            _bound = true;
            ModLog.Verbose($"[KeyViewer] 키 바인딩 로드 완료: {string.Join(" ", Labels)}");
        }

        public static void Draw()
        {
            if (!SaveCustomKeyConfig.EnableKeyViewer)
                return;

            try
            {
                if (_whiteTex == null)
                {
                    _whiteTex = new Texture2D(1, 1);
                    _whiteTex.SetPixel(0, 0, Color.white);
                    _whiteTex.Apply();
                }

                if (_keyStyle == null)
                {
                    _keyStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 15
                    };
                }

                const float boxW = 44f;
                const float boxH = 44f;
                const float gap = 6f;
                const float bottomMargin = 56f;

                float totalW = Lanes.Length * boxW + (Lanes.Length - 1) * gap;
                float startX = (Screen.width - totalW) / 2f;
                float y = Screen.height - bottomMargin - boxH;

                for (int i = 0; i < Lanes.Length; i++)
                {
                    float x = startX + i * (boxW + gap);
                    var box = new Rect(x, y, boxW, boxH);

                    bool pressed = Pressed[i];
                    bool unbound = Keys[i] == KeyCode.None;

                    // 박스 배경
                    Color bg = unbound
                        ? new Color(0.08f, 0.08f, 0.08f, 0.35f)
                        : (pressed ? new Color(0.15f, 0.75f, 0.85f, 0.85f) : new Color(0.08f, 0.08f, 0.08f, 0.65f));
                    DrawColorRect(box, bg);

                    // 테두리
                    Color border = pressed ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.35f);
                    DrawBorder(box, border);

                    // 키 라벨
                    _keyStyle.normal.textColor = pressed
                        ? new Color(0.05f, 0.05f, 0.05f, 1f)
                        : new Color(1f, 1f, 1f, unbound ? 0.35f : 0.9f);
                    GUI.Label(box, Labels[i] ?? "-", _keyStyle);
                }
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[KeyViewer] OnGUI 드로우 에러: {ex.Message}");
            }
        }

        private static string ToLabel(KeyCode kc)
        {
            if (kc == KeyCode.None)
                return "-";

            string name = kc.ToString();

            if (name.StartsWith("Alpha") && name.Length == 6)
                return name.Substring(5);
            if (name.StartsWith("Keypad"))
                return "#" + name.Substring(6);

            switch (kc)
            {
                case KeyCode.Space: return "SPC";
                case KeyCode.LeftShift: return "LSFT";
                case KeyCode.RightShift: return "RSFT";
                case KeyCode.LeftControl: return "LCTL";
                case KeyCode.RightControl: return "RCTL";
                case KeyCode.LeftAlt: return "LALT";
                case KeyCode.RightAlt: return "RALT";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
            }

            return name.Length <= 4 ? name : name.Substring(0, 4);
        }

        private static void DrawColorRect(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, _whiteTex);
            GUI.color = Color.white;
        }

        private static void DrawBorder(Rect rect, Color color)
        {
            const float t = 1.5f;
            DrawColorRect(new Rect(rect.x, rect.y, rect.width, t), color);
            DrawColorRect(new Rect(rect.x, rect.yMax - t, rect.width, t), color);
            DrawColorRect(new Rect(rect.x, rect.y, t, rect.height), color);
            DrawColorRect(new Rect(rect.xMax - t, rect.y, t, rect.height), color);
        }
    }
}
