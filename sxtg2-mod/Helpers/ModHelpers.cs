using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Helpers
{
    public enum ModLogLevel
    {
        ErrorsOnly = 0,
        Normal = 1,
        Verbose = 2
    }

    public static class ModLog
    {
        private const string CategoryId = "sxtg2";
        private static MelonPreferences_Category _category;
        private static MelonPreferences_Entry _logLevelEntry;
        private static readonly Stack<string> CorrelationStack = new Stack<string>();

        private static MelonPreferences_Entry _autoPlayEntry;
        private static MelonPreferences_Entry _allPerfectEntry;
        private static MelonPreferences_Entry _blockSaveEntry;

        public static void RegisterPreferences()
        {
            if (_category != null)
                return;

            _category = MelonPreferences.CreateCategory(CategoryId, "sxtg2");
            _logLevelEntry = _category.CreateEntry(
                "LogLevel",
                (int)ModLogLevel.Normal,
                "로그 레벨",
                "0=오류만, 1=보통, 2=상세/대량 덤프",
                false);

            _autoPlayEntry = _category.CreateEntry(
                "EnableAutoPlay",
                false,
                "오토 플레이 활성화",
                "플레이 씬 진입 시 오토플레이 자동 작동",
                false);

            _allPerfectEntry = _category.CreateEntry(
                "EnableAllPerfect",
                false,
                "올 퍼펙트 판정 조작 (BLUESTAR)",
                "모든 판정을 BLUESTAR로 강제 변환",
                false);

            _blockSaveEntry = _category.CreateEntry(
                "BlockSaveBestRanking",
                true,
                "랭킹/베스트 스코어 저장 차단",
                "오토플레이 또는 판정 조작 시 스코어 저장을 차단",
                false);
        }

        public static bool EnableAutoPlay => SaveCustomKeyConfig.AutoPlay || (_autoPlayEntry != null && Convert.ToBoolean(_autoPlayEntry.BoxedValue));
        public static bool EnableAllPerfect => SaveCustomKeyConfig.AllPerfect || (_allPerfectEntry != null && Convert.ToBoolean(_allPerfectEntry.BoxedValue));
        public static bool BlockSaveBestRanking => SaveCustomKeyConfig.BlockSave || (_blockSaveEntry != null && Convert.ToBoolean(_blockSaveEntry.BoxedValue));

        public static ModLogLevel Level
        {
            get
            {
                if (_logLevelEntry == null)
                    return ModLogLevel.Normal;
                try
                {
                    var v = Convert.ToInt32(_logLevelEntry.BoxedValue);
                    if (v < 0) return ModLogLevel.ErrorsOnly;
                    if (v > 2) return ModLogLevel.Verbose;
                    return (ModLogLevel)v;
                }
                catch
                {
                    return ModLogLevel.Normal;
                }
            }
        }

        public static bool IsVerbose => Level == ModLogLevel.Verbose;

        public static IDisposable BeginCorrelation(string operation, string hint = null)
        {
            var suffix = string.IsNullOrEmpty(hint) ? Guid.NewGuid().ToString("N").Substring(0, 8) : SanitizeHint(hint);
            var id = $"{operation}:{suffix}";
            lock (CorrelationStack)
                CorrelationStack.Push(id);
            return new CorrelationScope();
        }

        private static string SanitizeHint(string hint)
        {
            if (string.IsNullOrEmpty(hint))
                return "unknown";
            if (hint.Length > 32)
                hint = hint.Substring(0, 32);
            return hint.Replace('\r', '_').Replace('\n', '_');
        }

        private static string Prefix(string message)
        {
            string cid;
            lock (CorrelationStack)
                cid = CorrelationStack.Count > 0 ? CorrelationStack.Peek() : null;
            return string.IsNullOrEmpty(cid) ? message : $"[cid:{cid}] {message}";
        }

        public static void Msg(string message)
        {
            if (Level == ModLogLevel.ErrorsOnly)
                return;
            MelonLogger.Msg(Prefix(message));
        }

        public static void Warning(string message)
        {
            if (Level == ModLogLevel.ErrorsOnly)
                return;
            MelonLogger.Warning(Prefix(message));
        }

        public static void Error(string message)
        {
            MelonLogger.Error(Prefix(message));
        }

        public static void Exception(string context, Exception ex)
        {
            if (ex == null)
            {
                MelonLogger.Error(Prefix($"[{context}] (null exception)"));
                return;
            }

            MelonLogger.Error(Prefix($"[{context}]\n{FormatException(ex)}"));
        }

        public static void Verbose(string message)
        {
            if (Level != ModLogLevel.Verbose)
                return;
            MelonLogger.Msg(Prefix(message));
        }

        public static string FormatException(Exception ex, int maxInnerDepth = 8)
        {
            if (ex == null)
                return "(null)";

            var sb = new StringBuilder();
            var depth = 0;
            for (Exception e = ex; e != null && depth < maxInnerDepth; e = e.InnerException, depth++)
            {
                if (depth > 0)
                    sb.AppendLine("--- InnerException ---");
                sb.Append('[').Append(e.GetType().FullName).Append("] ").AppendLine(e.Message ?? "");
            }

            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                sb.AppendLine("--- StackTrace ---");
                sb.AppendLine(ex.StackTrace);
            }

            return sb.ToString();
        }

        private sealed class CorrelationScope : IDisposable
        {
            private bool _disposed;

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;
                lock (CorrelationStack)
                {
                    if (CorrelationStack.Count > 0)
                        CorrelationStack.Pop();
                }
            }
        }
    }

    public static class SaveCustomKeyConfig
    {
        public const float DefaultMaxScore = 1000000f;

        private static bool _initialized = false;
        private static int _reloadCount = 0;

        public static bool AutoPlay { get; set; }
        public static bool AllPerfect { get; set; }
        public static bool BlockSave { get; set; }
        public static bool EnableJudgmentBar { get; set; }
        public static bool JudgmentBarVertical { get; set; }
        public static int JudgmentBarShape { get; set; }
        public static int JudgmentBarRangeShape { get; set; }
        public static bool JudgmentBarCapsule
        {
            get => JudgmentBarShape == 1;
            set => JudgmentBarShape = value ? 1 : (JudgmentBarShape == 1 ? 0 : JudgmentBarShape);
        }
        public static string JudgmentBarSide { get; set; }
        public static bool EnableKeyViewer { get; set; }
        public static Color KeyViewerPressedColor { get; set; }
        public static Color KeyViewerNormalColor { get; set; }
        public static Color KeyViewerGatePressedColor { get; set; }
        public static float MaxScore { get; set; }

        public static bool EnableNoteSway { get; set; }
        public static float NoteSwayAmplitude { get; set; }
        public static float NoteSwaySpeed { get; set; }
        public static bool NoteSwayDamping { get; set; }
        public static float NoteSwayDampingTime { get; set; }

        public static bool EnableNoteSpeedChaos { get; set; }
        public static float NoteSpeedChaosMin { get; set; }
        public static float NoteSpeedChaosMax { get; set; }
        public static bool NoteSpeedChaosPerLane { get; set; }

        public static bool EnableCustomNotes { get; set; }

        public static float SuddenPercent { get; set; }
        public static float HiddenPercent { get; set; }
        public static float LaneCoverSoftness { get; set; }
        public static bool LaneCoverDebug { get; set; }

        // 페이드 인/아웃. 위치와 길이는 판정선 = 0%, 화면 끝 = 100% 기준.
        public static bool EnableFadeIn { get; set; }
        public static float FadeInStart { get; set; }
        public static float FadeInLength { get; set; }
        public static bool EnableFadeOut { get; set; }
        public static float FadeOutStart { get; set; }
        public static float FadeOutLength { get; set; }

        /// <summary>서든/히든과 페이드 인/아웃을 합친 레인 가림 위치.</summary>
        public static LaneCoverBand LaneCover => LaneCoverBand.From(
            SuddenPercent, HiddenPercent,
            EnableFadeIn, FadeInStart, FadeInLength,
            EnableFadeOut, FadeOutStart, FadeOutLength);

        public static bool EnableCustomMarathon { get; set; }
        public static float CustomMarathonAutoNext { get; set; }

        // ───────────── 설정 항목 표 ─────────────
        // 파일 읽기(이름/별칭 → 값), 기본 파일 만들기, 구역 형식으로 다시 쓰기, 재로드 전후 비교가 전부 이 표 하나를 쓴다.
        // 새 설정은 여기에 한 줄만 넣으면 된다.

        private sealed class Setting
        {
            public string Key;
            public string[] Aliases = new string[0];
            public string[] Comments = new string[0];
            public Action<string> Parse;
            public Func<string> Format;
            // 예전 이름. 읽기만 하고 파일에는 쓰지 않는다(구역 형식으로 다시 쓸 때 대표 항목으로 옮겨 적힌다).
            public bool Legacy;

            public Setting Alias(params string[] aliases)
            {
                Aliases = aliases;
                return this;
            }
        }

        private sealed class Section
        {
            public string Title;
            public string[] Comments = new string[0];
            public Setting[] Settings;
        }

        private const string SectionMarker = "# ===== [";

        private static Setting Bool(string key, Func<bool> get, Action<bool> set, params string[] comments) => new Setting
        {
            Key = key,
            Comments = comments,
            Parse = val => set(ParseFlexibleBool(val, get())),
            Format = () => get() ? "1" : "0"
        };

        private static Setting Number(string key, Func<float> get, Action<float> set, float min, float max, params string[] comments) => new Setting
        {
            Key = key,
            Comments = comments,
            Parse = val => set(ParseFloatSetting(key, val.TrimEnd('%', ' '), get(), min, max)),
            Format = () => NumText(get(), "0.###")
        };

        private static Setting Shape(string key, Func<int> get, Action<int> set, params string[] comments) => new Setting
        {
            Key = key,
            Comments = comments,
            Parse = val => set(ParseShapeSetting(key, val, get())),
            Format = () => get().ToString(CultureInfo.InvariantCulture)
        };

        private static Setting ColorValue(string key, Func<Color> get, Action<Color> set, params string[] comments) => new Setting
        {
            Key = key,
            Comments = comments,
            Parse = val => set(ParseColorSetting(key, val, get())),
            Format = () => ColorText(get())
        };

        private static readonly Section[] Sections =
        {
            new Section
            {
                Title = "플레이",
                Settings = new[]
                {
                    Bool("AutoPlay", () => AutoPlay, v => AutoPlay = v, "오토 플레이 (1 = 켜짐, 0 = 꺼짐)"),
                    Bool("AllPerfect", () => AllPerfect, v => AllPerfect = v, "올 퍼펙트 판정 조작 - BLUESTAR (1 = 켜짐, 0 = 꺼짐)"),
                    Bool("BlockSave", () => BlockSave, v => BlockSave = v, "베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐)"),
                    Number("MaxScore", () => MaxScore, v => MaxScore = v, 0.001f, float.MaxValue,
                        "점수 상한 (만점 기준값). 기본값 1000000 = 원본과 동일",
                        "1000000 이외의 값을 넣으면 판정 점수 계산의 만점이 그 값으로 바뀝니다.").Alias("ScoreLimit"),
                }
            },
            new Section
            {
                Title = "판정바",
                Settings = new[]
                {
                    Bool("EnableJudgmentBar", () => EnableJudgmentBar, v => EnableJudgmentBar = v, "실시간 판정바 표시 (1 = 켜짐, 0 = 꺼짐)").Alias("JudgmentBar"),
                    Bool("JudgmentBarVertical", () => JudgmentBarVertical, v => JudgmentBarVertical = v, "판정바 방향 (1 = 세로, 0 = 가로)"),
                    Shape("JudgmentBarShape", () => JudgmentBarShape, v => JudgmentBarShape = v, "판정바 모양 (0 = 사각 바, 1 = 알약 캡슐, 2 = 삼각/다이아몬드)"),
                    Shape("JudgmentBarRangeShape", () => JudgmentBarRangeShape, v => JudgmentBarRangeShape = v,
                        "판정바 안쪽 범위 박스 모양 (-1 = 판정바 모양 따라감, 0 = 사각, 1 = 알약, 2 = 삼각)"),
                    new Setting
                    {
                        Key = "JudgmentBarSide",
                        Comments = new[] { "판정바 위치 (Left = 화면 왼쪽, Right = 화면 오른쪽, Center = 기본 위치)",
                            "기본 위치는 세로 판정바는 왼쪽, 가로 판정바는 화면 정중앙입니다." },
                        Parse = val => JudgmentBarSide = ParseSideSetting("JudgmentBarSide", val, JudgmentBarSide),
                        Format = () => JudgmentBarSide
                    }.Alias("JudgmentBarPosition"),
                    new Setting
                    {
                        Key = "JudgmentBarCapsule",
                        Legacy = true,
                        Parse = val => JudgmentBarCapsule = ParseFlexibleBool(val, JudgmentBarCapsule),
                        Format = () => JudgmentBarCapsule ? "1" : "0"
                    },
                }
            },
            new Section
            {
                Title = "키뷰어",
                Settings = new[]
                {
                    Bool("EnableKeyViewer", () => EnableKeyViewer, v => EnableKeyViewer = v, "실시간 키뷰어 표시 (1 = 켜짐, 0 = 꺼짐)").Alias("KeyViewer"),
                    ColorValue("KeyViewerPressedColor", () => KeyViewerPressedColor, v => KeyViewerPressedColor = v,
                        "키를 눌렀을 때 색 (일반 노트 키)",
                        "형식: #RRGGBB, #RRGGBBAA, R,G,B,A, 색 이름 (시안, 마젠타, 노랑, 빨강, 파랑, 초록, 흰색, 검정, 주황, 보라, 분홍, 하늘, 민트)"),
                    ColorValue("KeyViewerNormalColor", () => KeyViewerNormalColor, v => KeyViewerNormalColor = v, "누르지 않았을 때 색"),
                    ColorValue("KeyViewerGatePressedColor", () => KeyViewerGatePressedColor, v => KeyViewerGatePressedColor = v, "GATE 키(가운데 4번)를 눌렀을 때 색"),
                }
            },
            new Section
            {
                Title = "노트 흔들림",
                Comments = new[] { "노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출. 판정에는 영향이 없습니다." },
                Settings = new[]
                {
                    Bool("NoteSway", () => EnableNoteSway, v => EnableNoteSway = v, "흔들림 연출 (1 = 켜짐, 0 = 꺼짐)").Alias("EnableNoteSway"),
                    Number("NoteSwayAmplitude", () => NoteSwayAmplitude, v => NoteSwayAmplitude = v, 0f, 1000f, "흔들림 폭 (픽셀). 너무 크면 레인 밖으로 나가 잘릴 수 있습니다."),
                    Number("NoteSwaySpeed", () => NoteSwaySpeed, v => NoteSwaySpeed = v, 0f, 50f, "흔들림 속도 (초당 왕복 횟수)"),
                    Bool("NoteSwayDamping", () => NoteSwayDamping, v => NoteSwayDamping = v,
                        "판정선에 가까워지면 흔들림을 잦아들게 함 (1 = 켜짐, 0 = 꺼짐). 끄면 판정선에 닿을 때까지 흔들립니다."),
                    Number("NoteSwayDampingTime", () => NoteSwayDampingTime, v => NoteSwayDampingTime = v, 0.01f, 30f, "판정선 도달 몇 초 전부터 잦아들지"),
                }
            },
            new Section
            {
                Title = "노트 속도 카오스",
                Comments = new[] { "[챌린지] 노트마다 낙하 속도가 제각각이라 서로 추월합니다. 판정에는 영향이 없습니다." },
                Settings = new[]
                {
                    Bool("NoteSpeedChaos", () => EnableNoteSpeedChaos, v => EnableNoteSpeedChaos = v, "속도 카오스 (1 = 켜짐, 0 = 꺼짐)").Alias("EnableNoteSpeedChaos"),
                    Number("NoteSpeedChaosMin", () => NoteSpeedChaosMin, v => NoteSpeedChaosMin = v, 0.05f, 10f, "속도 배율 최솟값 (1 = 원래 속도)"),
                    Number("NoteSpeedChaosMax", () => NoteSpeedChaosMax, v => NoteSpeedChaosMax = v, 0.05f, 10f, "속도 배율 최댓값"),
                    Bool("NoteSpeedChaosPerLane", () => NoteSpeedChaosPerLane, v => NoteSpeedChaosPerLane = v,
                        "1 = 레인마다 속도가 다름(같은 레인 안에서는 순서 유지), 0 = 노트마다 다름(완전 카오스)"),
                }
            },
            new Section
            {
                Title = "커스텀 노트 스킨",
                Settings = new[]
                {
                    Bool("CustomNotes", () => EnableCustomNotes, v => EnableCustomNotes = v,
                        "게임 폴더 CustomNotes 폴더의 PNG(Blue.png, Red.png 등)로 노트 모양을 바꿈 (1 = 켜짐, 0 = 꺼짐)",
                        "게임 인벤토리의 노트 스킨 목록에서 \"커스텀 노트\"를 고르거나 다른 스킨으로 옮기면 자동으로 바뀝니다.").Alias("EnableCustomNotes"),
                }
            },
            new Section
            {
                Title = "서든 / 히든",
                Comments = new[]
                {
                    "[챌린지] 레인 일부를 칼같이 가립니다. 판정선 = 0%, 노트가 나오는 화면 끝 = 100%로 잽니다.",
                    "Sudden/Hidden은 게임을 켤 때마다 0(끔)으로 돌아갑니다. 켠 뒤 여기서 올리면 다음 곡부터 적용됩니다."
                },
                Settings = new[]
                {
                    Number("Sudden", () => SuddenPercent, v => SuddenPercent = v, 0f, 90f,
                        "서든: 위쪽을 가릴 비율 (%, 0 = 끔). 예: 30 = 위쪽 30%를 가려 노트가 늦게 보임").Alias("SuddenPlus"),
                    Number("Hidden", () => HiddenPercent, v => HiddenPercent = v, 0f, 90f,
                        "히든: 판정선 쪽을 가릴 비율 (%, 0 = 끔). 서든과 합쳐 90%를 넘으면 히든이 줄어듭니다.").Alias("HiddenPlus"),
                    Number("LaneCoverSoftness", () => LaneCoverSoftness, v => LaneCoverSoftness = v, 0f, 500f,
                        "칼같이 가린 경계를 살짝 흐리는 폭 (픽셀, 0 = 칼같이 자름)"),
                }
            },
            new Section
            {
                Title = "페이드 인 / 페이드 아웃",
                Comments = new[]
                {
                    "[챌린지] 노트가 서서히 나타나거나(페이드 인) 서서히 사라집니다(페이드 아웃).",
                    "위치·길이는 판정선 = 0%, 노트가 나오는 화면 끝 = 100% 기준입니다.",
                    "같은 쪽에 서든(히든)도 켜면 더 많이 가리는 쪽 경계가 쓰입니다.",
                    "FadeIn/FadeOut은 게임을 켤 때마다 0(끔)으로 돌아갑니다. 위치와 길이는 그대로 남습니다."
                },
                Settings = new[]
                {
                    Bool("FadeIn", () => EnableFadeIn, v => EnableFadeIn = v, "페이드 인: 내려오는 노트가 서서히 나타남 (1 = 켜짐, 0 = 꺼짐)"),
                    Number("FadeInStart", () => FadeInStart, v => FadeInStart = v, 0f, 100f,
                        "나타나기 시작하는 위치 (%). 100 = 화면 끝에서부터, 60 = 레인 60% 높이에서부터"),
                    Number("FadeInLength", () => FadeInLength, v => FadeInLength = v, 0f, 100f,
                        "이 길이(%)만큼 내려오는 동안 투명 → 불투명. 예: 시작 100, 길이 20 → 80% 높이부터 완전히 보임"),
                    Bool("FadeOut", () => EnableFadeOut, v => EnableFadeOut = v, "페이드 아웃: 판정선에 다가가는 노트가 서서히 사라짐 (1 = 켜짐, 0 = 꺼짐)"),
                    Number("FadeOutStart", () => FadeOutStart, v => FadeOutStart = v, 0f, 100f,
                        "사라지기 시작하는 위치 (%). 예: 40 = 레인 40% 높이부터 흐려지기 시작"),
                    Number("FadeOutLength", () => FadeOutLength, v => FadeOutLength = v, 0f, 100f,
                        "이 길이(%)만큼 내려오는 동안 불투명 → 투명. 예: 시작 20, 길이 20 → 판정선에서 완전히 사라짐"),
                }
            },
            new Section
            {
                Title = "커스텀 곡 마라톤",
                Settings = new[]
                {
                    Bool("CustomMarathon", () => EnableCustomMarathon, v => EnableCustomMarathon = v,
                        "마라톤 (1 = 켜짐, 0 = 꺼짐). 커스텀 곡 하나를 시작하면 결과창 Enter로 아직 안 친 다음 커스텀 곡이 바로 시작됩니다.",
                        "모든 커스텀 곡을 한 번씩 돌면 목록으로 돌아갑니다. 고른 곡이 1번, 나머지는 매번 무작위(같은 곡은 다시 안 나옴).",
                        "결과창에서 Esc = 마라톤 그만하고 목록으로").Alias("EnableCustomMarathon"),
                    Number("CustomMarathonAutoNext", () => CustomMarathonAutoNext, v => CustomMarathonAutoNext = v, 0f, 600f,
                        "결과창에서 몇 초 뒤 자동으로 다음 곡으로 넘어갈지 (0 = Enter를 누를 때만)"),
                }
            },
            new Section
            {
                Title = "진단",
                Settings = new[]
                {
                    Bool("LaneCoverDebug", () => LaneCoverDebug, v => LaneCoverDebug = v,
                        "서든/히든/페이드 계산선을 화면에 그림 (빨강 = 판정선, 하늘색 = 가림 경계, 연두 = 페이드가 끝나는 곳). 확인이 끝나면 0으로"),
                }
            },
        };

        // 대표 이름과 별칭(대소문자 무시) → 항목. Sections 다음에 선언해야 초기화 순서가 맞는다.
        private static readonly Dictionary<string, Setting> SettingsByName = BuildSettingsLookup();

        private static Dictionary<string, Setting> BuildSettingsLookup()
        {
            var lookup = new Dictionary<string, Setting>(StringComparer.OrdinalIgnoreCase);
            foreach (var section in Sections)
            {
                foreach (var setting in section.Settings)
                {
                    lookup[setting.Key] = setting;
                    foreach (var alias in setting.Aliases)
                        lookup[alias] = setting;
                }
            }
            return lookup;
        }

        static SaveCustomKeyConfig()
        {
            ResetToDefaults();
        }

        /// <summary>
        /// 모든 값을 기본값으로 되돌린다. 재로드 때 이걸 먼저 하지 않으면, 사용자가 설정 줄을
        /// 지우거나 주석 처리해도 파서가 폴백으로 "현재 값"을 쓰기 때문에 직전 값이 그대로 살아남는다.
        /// </summary>
        public static void ResetToDefaults()
        {
            AutoPlay = false;
            AllPerfect = false;
            BlockSave = true;
            EnableJudgmentBar = true;
            JudgmentBarVertical = true;
            JudgmentBarShape = 0;
            JudgmentBarRangeShape = -1;
            JudgmentBarSide = "Center";
            EnableKeyViewer = true;
            KeyViewerPressedColor = new Color(0.15f, 0.75f, 0.85f, 0.85f);
            KeyViewerNormalColor = new Color(0.08f, 0.08f, 0.08f, 0.65f);
            KeyViewerGatePressedColor = new Color(1.0f, 0.25f, 0.5f, 0.9f);
            MaxScore = DefaultMaxScore;

            EnableNoteSway = false;
            NoteSwayAmplitude = 12f;
            NoteSwaySpeed = 0.8f;
            NoteSwayDamping = true;
            NoteSwayDampingTime = 0.4f;

            EnableNoteSpeedChaos = false;
            NoteSpeedChaosMin = 0.6f;
            NoteSpeedChaosMax = 1.8f;
            NoteSpeedChaosPerLane = false;

            EnableCustomNotes = true;

            SuddenPercent = 0f;
            HiddenPercent = 0f;
            LaneCoverSoftness = 24f;
            LaneCoverDebug = false;

            EnableFadeIn = false;
            FadeInStart = 100f;
            FadeInLength = 20f;
            EnableFadeOut = false;
            FadeOutStart = 20f;
            FadeOutLength = 20f;

            EnableCustomMarathon = false;
            CustomMarathonAutoNext = 0f;
        }

        public static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        public static void Initialize()
        {
            _initialized = true;
            LoadFromDisk(isReload: false, reason: null);
            ResetLaneCoverOnStart();
        }

        private static string ConfigFilePath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), "SaveCustomKey", "config.txt");

        private static readonly string[] LaneCoverKeys = { "Sudden", "SuddenPlus", "Hidden", "HiddenPlus", "FadeIn", "FadeOut" };

        private static readonly string[] CustomNotesKeys = { "CustomNotes", "EnableCustomNotes" };

        /// <summary>
        /// 서든/히든과 페이드 인/아웃은 그 판에만 거는 챌린지라 게임을 켤 때마다 꺼진 상태로 시작한다(페이드 위치·길이는 유지).
        /// 설정 파일은 플레이 씬에 들어갈 때마다 다시 읽히므로 메모리 값만 끄면 첫 곡에서 되살아난다.
        /// 그래서 파일의 해당 줄도 0으로 고쳐 쓴다(다른 줄과 주석, 인코딩, 줄바꿈은 그대로).
        /// 게임을 켠 뒤 파일에서 다시 켜면 그 세션 동안은 적용된다.
        /// </summary>
        private static void ResetLaneCoverOnStart()
        {
            if (SuddenPercent <= 0f && HiddenPercent <= 0f && !EnableFadeIn && !EnableFadeOut)
                return;

            string before = $"Sudden {SuddenPercent:0.#}%, Hidden {HiddenPercent:0.#}%, FadeIn {(EnableFadeIn ? 1 : 0)}, FadeOut {(EnableFadeOut ? 1 : 0)}";
            SuddenPercent = 0f;
            HiddenPercent = 0f;
            EnableFadeIn = false;
            EnableFadeOut = false;

            try
            {
                RewriteConfigValues(LaneCoverKeys, "0");
                MelonLogger.Msg($"[SaveCustomKey] 서든/히든·페이드 인/아웃은 게임을 켤 때마다 꺼진 상태로 시작합니다 ({before} → 전부 0, config.txt도 0으로 고침)");
            }
            catch (Exception ex)
            {
                // 파일을 못 고쳐도 이번 세션 메모리 값은 0이다. 다만 첫 곡에서 파일 값이 다시 읽힐 수 있다.
                MelonLogger.Warning($"[SaveCustomKey] 서든/히든·페이드 초기화 중 config.txt를 고치지 못했습니다: {ex.Message}");
            }
        }

        /// <summary>
        /// 게임 인벤토리에서 커스텀 노트 스킨을 고르거나 다른 스킨으로 옮겼을 때 호출된다.
        /// 설정은 플레이 씬에 들어갈 때마다 파일에서 다시 읽히므로 config.txt의 CustomNotes 줄도 같이 고친다.
        /// </summary>
        public static void SetCustomNotesFromInventory(bool enabled)
        {
            if (EnableCustomNotes == enabled)
                return;

            EnableCustomNotes = enabled;
            try
            {
                string value = enabled ? "1" : "0";
                if (!RewriteConfigValues(CustomNotesKeys, value))
                    File.AppendAllText(ConfigFilePath, Environment.NewLine + "CustomNotes=" + value + Environment.NewLine, Encoding.UTF8);
                MelonLogger.Msg($"[SaveCustomKey] 인벤토리에서 노트 스킨 변경 → CustomNotes={value} (config.txt 저장)");
            }
            catch (Exception ex)
            {
                // 메모리 값은 바뀌었지만, 다음 곡에서 파일의 옛 값이 다시 읽힐 수 있다.
                MelonLogger.Warning($"[SaveCustomKey] CustomNotes를 config.txt에 저장하지 못했습니다: {ex.Message}");
            }
        }

        /// <summary>
        /// config.txt에서 keys 중 하나로 시작하는 설정 줄의 값만 value로 고쳐 쓴다(다른 줄과 주석, BOM, 줄바꿈은 그대로).
        /// 해당하는 줄이 하나라도 있었으면 true.
        /// </summary>
        private static bool RewriteConfigValues(string[] keys, string value)
        {
            string filePath = ConfigFilePath;
            byte[] raw = File.ReadAllBytes(filePath);
            bool hasBom = raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF;
            string text = new UTF8Encoding(false).GetString(raw, hasBom ? 3 : 0, raw.Length - (hasBom ? 3 : 0));

            var pattern = new Regex(
                @"^([ \t]*(?:" + string.Join("|", Array.ConvertAll(keys, Regex.Escape)) + @")[ \t]*=)[^\r\n]*",
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            if (!pattern.IsMatch(text))
                return false;

            string updated = pattern.Replace(text, "${1}" + value);
            if (updated != text)
                File.WriteAllText(filePath, updated, new UTF8Encoding(hasBom));
            return true;
        }

        /// <summary>
        /// 설정 파일을 디스크에서 다시 읽는다. 플레이 씬으로 넘어가는 순간에만 호출되므로
        /// 한 판이 진행되는 도중에 값이 바뀌는 일은 없다(= 수정한 설정은 다음 플레이부터 적용).
        /// 게임을 재시작하지 않아도 되지만, 플레이 중 노트가 튀거나 굳는 부작용도 없다.
        /// </summary>
        public static void Reload(string reason)
        {
            // 씬 전환 콜백에서 불리므로 여기서 예외가 새어나가면 다른 구독자까지 끊긴다.
            try
            {
                EnsureInitialized();

                var before = Snapshot();
                LoadFromDisk(isReload: true, reason: reason);
                _reloadCount++;
                LogDiff(before, Snapshot(), reason);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 재로드 실패 ({reason}): {ex.Message}");
            }
        }

        private static void LoadFromDisk(bool isReload, string reason)
        {
            string stage = isReload ? "재로드" : "초기화";

            try
            {
                string configFilePath = ConfigFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));

                if (!File.Exists(configFilePath))
                {
                    File.WriteAllText(configFilePath, BuildConfigText(), Encoding.UTF8);
                    MelonLogger.Msg($"[SaveCustomKey] 기본 설정 파일 생성 완료: {configFilePath}");
                }

                if (isReload)
                    ModLog.Verbose($"[SaveCustomKey] 설정 재로드 시도 #{_reloadCount + 1} ({reason}) - {configFilePath}");

                LoadConfigFile(configFilePath, isReload);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 {stage} 중 오류: {ex.Message}");
            }
        }

        /// <summary>표의 구역 순서대로, 지금 값을 넣은 설정 파일 전체 내용.</summary>
        private static string BuildConfigText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# sxtg2 모드 설정 파일 (SaveCustomKey/config.txt)");
            sb.AppendLine("# 저장하면 다음 곡(플레이 씬에 들어갈 때)부터 적용됩니다. 게임을 다시 켤 필요는 없습니다.");
            sb.AppendLine("# 켜기/끄기 값: 1/0, true/false, 켜짐/꺼짐, on/off 모두 됩니다. # 또는 // 로 시작하는 줄은 주석입니다.");

            foreach (var section in Sections)
            {
                sb.AppendLine();
                sb.AppendLine($"{SectionMarker}{section.Title}] =====");
                foreach (var line in section.Comments)
                    sb.AppendLine("# " + line);

                foreach (var setting in section.Settings)
                {
                    if (setting.Legacy)
                        continue;

                    sb.AppendLine();
                    foreach (var line in setting.Comments)
                        sb.AppendLine("# " + line);
                    sb.AppendLine($"{setting.Key}={setting.Format()}");
                }
            }

            return sb.ToString();
        }

        private static void LoadConfigFile(string filePath, bool isReload)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 에디터가 저장하는 중이면 파일이 잠겨 있을 수 있다. 여기서 값을 기본값으로 밀어버리면
                // 멀쩡히 쓰던 설정이 통째로 날아가므로, 손대지 않고 물러난다(다음 플레이 때 다시 시도).
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 읽기 실패 - 기존 값을 유지합니다: {ex.Message}");
                return;
            }

            try
            {
                // 반드시 읽기에 성공한 뒤에 리셋한다. 지워진 줄이 이전 값으로 남는 걸 막는 용도라
                // 읽기 실패 시점에 먼저 리셋해버리면 설정이 날아간다.
                if (isReload)
                    ResetToDefaults();

                var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var unknownKeys = new List<string>();
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;

                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    if (!SettingsByName.TryGetValue(key, out var setting))
                    {
                        unknownKeys.Add(key);
                        continue;
                    }

                    setting.Parse(parts[1].Trim());
                    seenKeys.Add(setting.Key);
                }

                if (unknownKeys.Count > 0)
                {
                    string message = $"[SaveCustomKey] 모르는 설정 이름이라 무시했습니다: {string.Join(", ", unknownKeys.ToArray())}";
                    if (isReload)
                        ModLog.Verbose(message);
                    else
                        MelonLogger.Warning(message);
                }

                Validate();

                // 파일 전체를 다시 쓰는 건 게임을 켤 때만 한다. 곡마다 하는 재로드 중에 하면, 마침 에디터가 저장하던
                // 파일을 반쯤 읽고 "빠진 항목이 있다"며 덮어써 버릴 수 있다.
                if (!isReload)
                    RewriteIfOutdated(filePath, lines, seenKeys);

                // 최초 로드는 전체를 남기고, 재로드는 바뀐 항목만 LogDiff가 남긴다.
                // 플레이할 때마다 이 긴 줄이 찍히면 로그가 못 쓰게 되므로 재로드 시엔 상세 레벨로 내린다.
                if (isReload)
                    ModLog.Verbose($"[SaveCustomKey] 재로드 후 전체 설정 - {Summary()}");
                else
                    MelonLogger.Msg($"[SaveCustomKey] 설정 로드 완료 - {Summary()}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파싱 실패: {ex.Message}");
            }
        }

        /// <summary>서로 얽힌 값들을 맞춘다. 각 값의 범위 검사는 읽을 때 이미 끝났다.</summary>
        private static void Validate()
        {
            if (NoteSpeedChaosMin > NoteSpeedChaosMax)
            {
                MelonLogger.Warning($"[SaveCustomKey] NoteSpeedChaosMin({NoteSpeedChaosMin:0.##})이 Max({NoteSpeedChaosMax:0.##})보다 큽니다 → 두 값을 맞바꿉니다.");
                float swap = NoteSpeedChaosMin;
                NoteSpeedChaosMin = NoteSpeedChaosMax;
                NoteSpeedChaosMax = swap;
            }

            if (SuddenPercent + HiddenPercent > 90f)
            {
                float reduced = 90f - SuddenPercent;
                MelonLogger.Warning($"[SaveCustomKey] Sudden({SuddenPercent:0.#}%) + Hidden({HiddenPercent:0.#}%)이 90%를 넘어 노트가 안 보이게 됩니다 → Hidden을 {reduced:0.#}%로 줄입니다.");
                HiddenPercent = reduced;
            }

            if (!EnableFadeIn && !EnableFadeOut)
                return;

            var band = LaneCover;
            if (band.VisibleSpan < 10f)
            {
                // 서든+히든만으로는 항상 10% 이상 남으므로, 페이드를 끄면 다시 보인다.
                MelonLogger.Warning($"[SaveCustomKey] 페이드 인/아웃까지 합치면 보이는 구간이 {Math.Max(0f, band.VisibleSpan):0.#}%뿐이라 노트가 거의 안 보입니다 " +
                    $"(FadeIn 시작 {FadeInStart:0.#}%, FadeOut 끝 {Math.Max(0f, FadeOutStart - FadeOutLength):0.#}%) → 페이드 인/아웃을 끕니다.");
                EnableFadeIn = false;
                EnableFadeOut = false;
            }
            else if (band.FarOpaque < band.NearOpaque)
            {
                MelonLogger.Warning($"[SaveCustomKey] 페이드 인이 끝나기({band.FarOpaque:0.#}%) 전에 페이드 아웃이 시작돼({band.NearOpaque:0.#}%) 노트가 끝까지 완전히 진해지지 않습니다.");
            }
        }

        /// <summary>
        /// 파일이 구역 형식이 아니거나(이전 버전) 빠진 항목이 있으면, 지금 값으로 파일 전체를 구역 형식으로 다시 쓴다.
        /// 값은 그대로 옮겨지고(별칭은 대표 이름으로), 원래 파일은 config.txt.bak으로 남긴다. 직접 단 주석은 백업에만 남는다.
        /// </summary>
        private static void RewriteIfOutdated(string filePath, string[] lines, HashSet<string> seenKeys)
        {
            bool sectioned = Array.Exists(lines, line => line.TrimStart().StartsWith(SectionMarker, StringComparison.Ordinal));
            var missing = new List<string>();
            foreach (var section in Sections)
            {
                foreach (var setting in section.Settings)
                {
                    if (!setting.Legacy && !seenKeys.Contains(setting.Key))
                        missing.Add(setting.Key);
                }
            }

            if (sectioned && missing.Count == 0)
                return;

            try
            {
                File.Copy(filePath, filePath + ".bak", overwrite: true);
                File.WriteAllText(filePath, BuildConfigText(), Encoding.UTF8);
                MelonLogger.Msg(sectioned
                    ? $"[SaveCustomKey] 설정 파일에 없던 항목({string.Join(", ", missing.ToArray())})을 넣어 다시 정리했습니다. 원본은 config.txt.bak"
                    : "[SaveCustomKey] 설정 파일을 카테고리 구역 형식으로 다시 정리했습니다. 값은 그대로이고, 원본은 config.txt.bak");
            }
            catch (Exception ex)
            {
                // 못 고쳐도 읽은 값은 그대로 쓴다. 다음 로드 때 다시 시도한다.
                MelonLogger.Warning($"[SaveCustomKey] 설정 파일을 구역 형식으로 정리하지 못했습니다: {ex.Message}");
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> CurrentValues()
        {
            foreach (var section in Sections)
            {
                foreach (var setting in section.Settings)
                {
                    if (!setting.Legacy)
                        yield return new KeyValuePair<string, string>(setting.Key, setting.Format());
                }
            }
        }

        private static string Summary()
        {
            var parts = new List<string>();
            foreach (var section in Sections)
            {
                var values = new List<string>();
                foreach (var setting in section.Settings)
                {
                    if (!setting.Legacy)
                        values.Add($"{setting.Key}={setting.Format()}");
                }
                parts.Add($"[{section.Title}] {string.Join(", ", values.ToArray())}");
            }
            return string.Join(" ", parts.ToArray());
        }

        /// <summary>재로드 전후를 비교해 실제로 바뀐 항목만 로그로 남기기 위한 스냅샷.</summary>
        private static Dictionary<string, string> Snapshot()
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var entry in CurrentValues())
                snapshot[entry.Key] = entry.Value;
            return snapshot;
        }

        private static void LogDiff(Dictionary<string, string> before, Dictionary<string, string> after, string reason)
        {
            var changes = new List<string>();
            foreach (var entry in after)
            {
                if (before.TryGetValue(entry.Key, out string old) && !string.Equals(old, entry.Value, StringComparison.Ordinal))
                    changes.Add($"{entry.Key}: {old} → {entry.Value}");
            }

            if (changes.Count == 0)
            {
                ModLog.Verbose($"[SaveCustomKey] 설정 재로드 #{_reloadCount} ({reason}) - 변경 없음");
                return;
            }

            MelonLogger.Msg($"[SaveCustomKey] 설정 재로드 #{_reloadCount} ({reason}) - {changes.Count}개 항목이 이번 플레이부터 적용됩니다");
            foreach (var change in changes)
                MelonLogger.Msg($"[SaveCustomKey]   · {change}");
        }

        private static string ColorText(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);

        private static string NumText(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        public static bool IsMaxScoreCustom => Math.Abs(MaxScore - DefaultMaxScore) > 0.001f;

        public static float ParseFloatSetting(string key, string val, float defaultValue, float min, float max)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            if (!float.TryParse(val.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            {
                MelonLogger.Warning($"[SaveCustomKey] {key} 값을 숫자로 읽지 못했습니다: \"{val}\" → 기본값 {defaultValue:0.###} 유지");
                return defaultValue;
            }

            if (parsed < min || parsed > max)
            {
                MelonLogger.Warning($"[SaveCustomKey] {key}는 {min:0.###} ~ {max:0.###} 범위여야 합니다: {parsed:0.###} → 기본값 {defaultValue:0.###} 유지");
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

            MelonLogger.Warning($"[SaveCustomKey] {key} 값을 알 수 없습니다: \"{val}\" (Left/Right/Center 중 하나) → 기본값 {defaultValue} 유지");
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

            MelonLogger.Warning($"[SaveCustomKey] {key} 모양 설정 값을 알 수 없습니다: \"{val}\" (0=사각, 1=알약, 2=삼각) → 기본값 유지");
            return defaultValue;
        }

        public static Color ParseColorSetting(string key, string val, Color defaultColor)
        {
            if (string.IsNullOrEmpty(val))
                return defaultColor;

            string s = val.Trim().ToLowerInvariant();

            // 1. 한글 / 영문 색상 키워드 파싱
            switch (s)
            {
                case "cyan": case "시안": case "청록": case "청록색": case "민트": case "민트색":
                    return new Color(0.15f, 0.75f, 0.85f, 0.85f);
                case "magenta": case "pink": case "마젠타": case "분홍": case "분홍색": case "핑": case "핫핑크":
                    return new Color(1.0f, 0.25f, 0.5f, 0.9f);
                case "yellow": case "노랑": case "노란색": case "황색":
                    return new Color(1.0f, 0.85f, 0.20f, 0.9f);
                case "red": case "빨강": case "빨간색": case "적색":
                    return new Color(0.95f, 0.25f, 0.25f, 0.9f);
                case "blue": case "파랑": case "파란색": case "청색":
                    return new Color(0.20f, 0.50f, 0.85f, 0.9f);
                case "green": case "초록": case "초록색": case "녹색":
                    return new Color(0.25f, 0.85f, 0.35f, 0.9f);
                case "white": case "흰색": case "하양": case "백색":
                    return new Color(0.95f, 0.95f, 0.95f, 0.9f);
                case "black": case "검정": case "검은색": case "흑색":
                    return new Color(0.08f, 0.08f, 0.08f, 0.85f);
                case "orange": case "주황": case "주황색":
                    return new Color(1.0f, 0.55f, 0.15f, 0.9f);
                case "purple": case "violet": case "보라": case "보라색": case "자색":
                    return new Color(0.65f, 0.35f, 0.85f, 0.9f);
                case "sky": case "skyblue": case "하늘": case "하늘색":
                    return new Color(0.40f, 0.75f, 1.0f, 0.9f);
            }

            // 2. HTML 헥스코드 파싱 (#RRGGBB, #RRGGBBAA, RRGGBB, RRGGBBAA)
            string hexCandidate = s.StartsWith("#") ? s : "#" + s;
            if (ColorUtility.TryParseHtmlString(hexCandidate, out Color parsedColor))
            {
                return parsedColor;
            }

            // 3. 쉼표 구분 RGBA 파싱 (예: 255,128,0 또는 0.15,0.75,0.85,0.85)
            var parts = s.Split(',');
            if (parts.Length == 3 || parts.Length == 4)
            {
                try
                {
                    float r = float.Parse(parts[0].Trim(), CultureInfo.InvariantCulture);
                    float g = float.Parse(parts[1].Trim(), CultureInfo.InvariantCulture);
                    float b = float.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                    float a = parts.Length == 4 ? float.Parse(parts[3].Trim(), CultureInfo.InvariantCulture) : 1f;

                    if (r > 1f || g > 1f || b > 1f || a > 1f)
                    {
                        r = Mathf.Clamp01(r / 255f);
                        g = Mathf.Clamp01(g / 255f);
                        b = Mathf.Clamp01(b / 255f);
                        a = a > 1f ? Mathf.Clamp01(a / 255f) : a;
                    }

                    return new Color(r, g, b, a);
                }
                catch { }
            }

            MelonLogger.Warning($"[SaveCustomKey] {key} 색상 값을 읽지 못했습니다: \"{val}\" → 기본값 유지");
            return defaultColor;
        }

        public static bool ParseFlexibleBool(string val, bool defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            var s = val.Trim().ToLowerInvariant();

            if (s == "1" || s == "true" || s == "t" || s == "on" || s == "켜짐" || s == "사용" || s == "활성화" || s == "enable" || s == "enabled" || s == "yes" || s == "y" || s == "트루" || s == "참" || s == "켜기")
                return true;

            if (s == "0" || s == "false" || s == "f" || s == "off" || s == "꺼짐" || s == "미사용" || s == "비활성화" || s == "disable" || s == "disabled" || s == "no" || s == "n" || s == "폴스" || s == "거짓" || s == "끄기")
                return false;

            return defaultValue;
        }
    }
}

namespace sxtg2.Helpers.Track
{
    public static class ThumbnailLoader
    {
        private static readonly string[] DefaultNames =
        {
            "thumb.png",
            "thumbnail.png",
            "jacket.png",
            "cover.png",
            "image.png"
        };

        public static Sprite LoadThumbnail(string trackId, string albumFolder)
        {
            if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
                return null;

            try
            {
                foreach (string fileName in GetCandidateNames(trackId))
                {
                    string path = Path.Combine(albumFolder, fileName);
                    if (File.Exists(path))
                        return LoadSprite(path);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ThumbnailLoader] 썸네일 로드 실패: {ex.Message}");
            }

            return null;
        }

        private static IEnumerable<string> GetCandidateNames(string trackId)
        {
            foreach (string name in DefaultNames)
                yield return name;

            if (string.IsNullOrEmpty(trackId))
                yield break;

            yield return trackId + "_thumb.png";
            yield return trackId + "_thumbnail.png";
            yield return trackId + "_jacket.png";
            yield return trackId + ".png";
        }

        private static Sprite LoadSprite(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2);
            if (!texture.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            MelonLogger.Msg(
                $"[ThumbnailLoader] 자켓 로드: {Path.GetFileName(path)} " +
                $"({texture.width}x{texture.height})");
            return Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
        }
    }
}
