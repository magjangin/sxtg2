using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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

            // 더 이상 읽지 않는다: 기본값 true가 config.txt의 BlockSave=0을 항상 덮어써서 저장 차단을 끌 수 없었다.
            // 기존 MelonPreferences.cfg에 이미 있는 항목이라 등록만 유지한다. 설정은 config.txt의 BlockSave를 쓴다.
            _blockSaveEntry = _category.CreateEntry(
                "BlockSaveBestRanking",
                true,
                "(사용 안 함) 랭킹/베스트 스코어 저장 차단",
                "더 이상 사용하지 않습니다. SaveCustomKey/config.txt의 BlockSave를 사용하세요.",
                false);
        }

        public static bool EnableAutoPlay => SaveCustomKeyConfig.AutoPlay || (_autoPlayEntry != null && Convert.ToBoolean(_autoPlayEntry.BoxedValue));
        public static bool EnableAllPerfect => SaveCustomKeyConfig.AllPerfect || (_allPerfectEntry != null && Convert.ToBoolean(_allPerfectEntry.BoxedValue));
        /// <summary>config.txt의 BlockSave만 따른다(MelonPreferences의 BlockSaveBestRanking은 읽지 않음).</summary>
        public static bool BlockSaveBestRanking => SaveCustomKeyConfig.BlockSave;

        /// <summary>
        /// 이번 플레이가 기록/업적/카운터에 남으면 안 되는 플레이인지(설정 쪽 조건): 오토플레이, 올퍼펙트, 점수 상한 변경.
        /// 커스텀 트랙인지는 호출하는 쪽에서 곡 정보로 따로 판단한다.
        /// </summary>
        public static bool IsTaintedPlayConfig => EnableAutoPlay || EnableAllPerfect || SaveCustomKeyConfig.IsMaxScoreCustom;

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
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string folderPath = Path.Combine(gamePath, "SaveCustomKey");
                Directory.CreateDirectory(folderPath);

                string configFilePath = Path.Combine(folderPath, "config.txt");
                if (!File.Exists(configFilePath))
                {
                    CreateDefaultConfigFile(configFilePath);
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

        private static void CreateDefaultConfigFile(string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# sxtg2 모드 설정 파일 (SaveCustomKey/config.txt)");
            sb.AppendLine("# 설정 변경 후 저장하면 게임 실행 시 자동 적용됩니다.");
            sb.AppendLine("# 지원 형식: 1/0, true/false, 켜짐/꺼짐, on/off");
            sb.AppendLine();
            sb.AppendLine("# 오토 플레이 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("AutoPlay=0");
            sb.AppendLine();
            sb.AppendLine("# 올 퍼펙트 판정 조작 - BLUESTAR (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("AllPerfect=0");
            sb.AppendLine();
            sb.AppendLine("# 베스트 스코어 / 랭킹 저장 차단 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("BlockSave=1");
            sb.AppendLine();
            sb.AppendLine("# 실시간 판정바 표시 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("EnableJudgmentBar=1");
            sb.AppendLine();
            sb.AppendLine("# 판정바 형태 (1 = 세로 판정바, 0 = 가로 판정바)");
            sb.AppendLine("JudgmentBarVertical=1");
            sb.AppendLine();
            AppendJudgmentBarCapsuleSection(sb);
            AppendJudgmentBarSideSection(sb);
            sb.AppendLine();
            sb.AppendLine("# 실시간 키뷰어 표시 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("EnableKeyViewer=1");
            AppendMaxScoreSection(sb);
            AppendNoteSwaySection(sb);
            AppendNoteSpeedChaosSection(sb);

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            MelonLogger.Msg($"[SaveCustomKey] 기본 설정 파일 생성 완료: {filePath}");
        }

        private static void AppendJudgmentBarCapsuleSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# 판정바 모양 (0 = 사각 바, 1 = 알약 캡슐 모양, 2 = 삼각/다이아몬드 모양)");
            sb.AppendLine("# 기존 JudgmentBarCapsule=1 키도 캡슐(1)로 동일하게 작동합니다.");
            sb.AppendLine("JudgmentBarShape=0");
            sb.AppendLine();
            sb.AppendLine("# 판정바 안쪽 범위 박스 모양 (-1 = 판정바 모양 추종, 0 = 사각, 1 = 알약, 2 = 삼각)");
            sb.AppendLine("JudgmentBarRangeShape=-1");
        }

        private static void AppendJudgmentBarSideSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# 판정바 위치 (Left = 화면 왼쪽, Right = 화면 오른쪽, Center = 기본 위치)");
            sb.AppendLine("# 기본 위치는 세로 판정바는 왼쪽 고정, 가로 판정바는 화면 정중앙입니다.");
            sb.AppendLine("JudgmentBarSide=Center");
        }

        private static void AppendKeyViewerColorSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# 키뷰어 눌림 색상 (일반 노트 키 입력 시)");
            sb.AppendLine("# 지원 형식: #RRGGBB, #RRGGBBAA, R,G,B,A, 영문/한글 색상명 (시안, 마젠타, 노랑, 빨강, 파랑, 초록, 흰색, 검정, 주황, 보라, 분홍, 하늘, 민트)");
            sb.AppendLine("KeyViewerPressedColor=#26BFD9D9");
            sb.AppendLine();
            sb.AppendLine("# 키뷰어 미입력 기본 색상");
            sb.AppendLine("KeyViewerNormalColor=#141414A6");
            sb.AppendLine();
            sb.AppendLine("# 키뷰어 GATE 키(중앙 4번) 전용 눌림 색상");
            sb.AppendLine("KeyViewerGatePressedColor=#FF4081E6");
        }

        private static void AppendMaxScoreSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# 점수 상한 (만점 기준값). 기본값 1000000 = 원본과 동일");
            sb.AppendLine("# 1000000 이외의 값을 넣으면 판정 점수 계산의 만점이 그 값으로 바뀝니다.");
            sb.AppendLine("# 0 이하 또는 숫자가 아닌 값은 무시되고 기본값이 사용됩니다.");
            sb.AppendLine($"MaxScore={DefaultMaxScore:0.###}");
        }

        private static void AppendNoteSwaySection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("# 판정에는 전혀 영향이 없는 순수 시각 효과입니다.");
            sb.AppendLine("NoteSway=0");
            sb.AppendLine();
            sb.AppendLine("# 흔들림 폭 (픽셀). 너무 크면 레인 밖으로 나가 잘릴 수 있습니다.");
            sb.AppendLine("NoteSwayAmplitude=12");
            sb.AppendLine();
            sb.AppendLine("# 흔들림 속도 (초당 왕복 횟수)");
            sb.AppendLine("NoteSwaySpeed=0.8");
            sb.AppendLine();
            sb.AppendLine("# 판정선에 가까워지면 흔들림을 잦아들게 함 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("# 끄면 판정선에 닿는 순간까지 계속 흔들립니다.");
            sb.AppendLine("NoteSwayDamping=1");
            sb.AppendLine();
            sb.AppendLine("# 판정선 도달 몇 초 전부터 흔들림이 잦아들지");
            sb.AppendLine("NoteSwayDampingTime=0.4");
        }

        private static void AppendNoteSpeedChaosSection(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("# [챌린지] 노트마다 낙하 속도를 제각각으로 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("# 노트끼리 서로 추월하므로 읽기가 매우 어려워집니다. 판정에는 영향이 없습니다.");
            sb.AppendLine("NoteSpeedChaos=0");
            sb.AppendLine();
            sb.AppendLine("# 속도 배율 범위 (1 = 원래 속도). 예: 0.6 ~ 1.8");
            sb.AppendLine("NoteSpeedChaosMin=0.6");
            sb.AppendLine("NoteSpeedChaosMax=1.8");
            sb.AppendLine();
            sb.AppendLine("# 1 = 레인마다 속도가 다름(같은 레인 안에서는 순서 유지, 읽을 수는 있음)");
            sb.AppendLine("# 0 = 노트마다 속도가 다름(완전 카오스)");
            sb.AppendLine("NoteSpeedChaosPerLane=0");
        }

        /// <summary>이전 버전에서 만들어진 설정 파일에는 새 항목이 없으므로 뒤에 덧붙여준다.</summary>
        private static void AppendMissingSections(string filePath, List<Action<StringBuilder>> sections, string keyNames)
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var section in sections)
                    section(sb);

                File.AppendAllText(filePath, sb.ToString(), Encoding.UTF8);
                MelonLogger.Msg($"[SaveCustomKey] 기존 설정 파일에 {keyNames} 항목을 추가했습니다: {filePath}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SaveCustomKey] 설정 항목 추가 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// config.txt 키(별칭 포함, 대소문자 무시) → 값 문자열을 읽어 설정에 반영하는 처리기.
        /// 설정을 추가할 때 파싱은 여기에 한 줄만 더하면 된다(기본값, 기본 파일 템플릿, Snapshot/요약은 따로 고친다).
        /// </summary>
        private static readonly Dictionary<string, Action<string>> SettingHandlers = BuildSettingHandlers();

        private static Dictionary<string, Action<string>> BuildSettingHandlers()
        {
            var map = new Dictionary<string, Action<string>>(StringComparer.OrdinalIgnoreCase);

            // names[0]이 경고 로그에 쓰이는 대표 이름이고, 나머지는 별칭이다.
            void Bool(Func<bool> get, Action<bool> set, params string[] names)
            {
                Action<string> handler = val => set(ConfigParsing.ParseBoolSetting(names[0], val, get()));
                foreach (var name in names)
                    map[name] = handler;
            }

            void Float(Func<float> get, Action<float> set, float min, float max, params string[] names)
            {
                Action<string> handler = val => set(ConfigParsing.ParseFloatSetting(names[0], val, get(), min, max));
                foreach (var name in names)
                    map[name] = handler;
            }

            void ColorValue(Func<Color> get, Action<Color> set, string name)
            {
                map[name] = val => set(ParseColorSetting(name, val, get()));
            }

            Bool(() => AutoPlay, v => AutoPlay = v, "AutoPlay");
            Bool(() => AllPerfect, v => AllPerfect = v, "AllPerfect");
            Bool(() => BlockSave, v => BlockSave = v, "BlockSave");

            Bool(() => EnableJudgmentBar, v => EnableJudgmentBar = v, "EnableJudgmentBar", "JudgmentBar");
            Bool(() => JudgmentBarVertical, v => JudgmentBarVertical = v, "JudgmentBarVertical");
            map["JudgmentBarShape"] = val => JudgmentBarShape = ConfigParsing.ParseShapeSetting("JudgmentBarShape", val, JudgmentBarShape);
            map["JudgmentBarRangeShape"] = val => JudgmentBarRangeShape = ConfigParsing.ParseShapeSetting("JudgmentBarRangeShape", val, JudgmentBarRangeShape);
            Bool(() => JudgmentBarCapsule, v => JudgmentBarCapsule = v, "JudgmentBarCapsule");
            Action<string> side = val => JudgmentBarSide = ConfigParsing.ParseSideSetting("JudgmentBarSide", val, JudgmentBarSide);
            map["JudgmentBarSide"] = side;
            map["JudgmentBarPosition"] = side;

            Bool(() => EnableKeyViewer, v => EnableKeyViewer = v, "EnableKeyViewer", "KeyViewer");
            ColorValue(() => KeyViewerPressedColor, c => KeyViewerPressedColor = c, "KeyViewerPressedColor");
            ColorValue(() => KeyViewerNormalColor, c => KeyViewerNormalColor = c, "KeyViewerNormalColor");
            ColorValue(() => KeyViewerGatePressedColor, c => KeyViewerGatePressedColor = c, "KeyViewerGatePressedColor");

            Float(() => MaxScore, v => MaxScore = v, 0.001f, float.MaxValue, "MaxScore", "ScoreLimit");

            Bool(() => EnableNoteSway, v => EnableNoteSway = v, "NoteSway", "EnableNoteSway");
            Float(() => NoteSwayAmplitude, v => NoteSwayAmplitude = v, 0f, 1000f, "NoteSwayAmplitude");
            Float(() => NoteSwaySpeed, v => NoteSwaySpeed = v, 0f, 50f, "NoteSwaySpeed");
            Bool(() => NoteSwayDamping, v => NoteSwayDamping = v, "NoteSwayDamping");
            Float(() => NoteSwayDampingTime, v => NoteSwayDampingTime = v, 0.01f, 30f, "NoteSwayDampingTime");

            Bool(() => EnableNoteSpeedChaos, v => EnableNoteSpeedChaos = v, "NoteSpeedChaos", "EnableNoteSpeedChaos");
            Float(() => NoteSpeedChaosMin, v => NoteSpeedChaosMin = v, 0.05f, 10f, "NoteSpeedChaosMin");
            Float(() => NoteSpeedChaosMax, v => NoteSpeedChaosMax = v, 0.05f, 10f, "NoteSpeedChaosMax");
            Bool(() => NoteSpeedChaosPerLane, v => NoteSpeedChaosPerLane = v, "NoteSpeedChaosPerLane");

            return map;
        }

        /// <summary>
        /// config.txt의 줄을 읽어 설정에 반영하고, 파일에 있던 키 목록을 돌려준다(누락 항목 추가 판단에 쓴다).
        /// 형식이 틀린 줄과 모르는 키는 경고를 남기고 무시한다.
        /// </summary>
        private static HashSet<string> ApplyConfigLines(string[] lines)
        {
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                    continue;

                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                {
                    MelonLogger.Warning($"[SaveCustomKey] 키=값 형식이 아닌 줄을 무시합니다: \"{trimmed}\"");
                    continue;
                }

                var key = parts[0].Trim();
                // 값 뒤의 줄 끝 주석(`AutoPlay=1 # 메모`)은 값에서 뗀다.
                var val = ConfigParsing.StripInlineComment(parts[1].Trim());
                seenKeys.Add(key);

                if (SettingHandlers.TryGetValue(key, out var apply))
                    apply(val);
                else
                    MelonLogger.Warning($"[SaveCustomKey] 알 수 없는 설정 키를 무시합니다: {key}");
            }

            return seenKeys;
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

                HashSet<string> seenKeys = ApplyConfigLines(lines);

                NormalizeNoteSpeedChaosRange();

                // 누락 항목 추가는 게임 시작 때만 한다. 플레이마다 재로드할 때 하면, 사용자가 지우거나 주석 처리한
                // 묶음을 모드가 다시 써 넣고 편집기에서 열어 둔 파일과 충돌한다(지운 줄은 기본값으로 동작하므로 불필요).
                if (!isReload)
                    AppendSectionsMissingFrom(filePath, seenKeys);

                string summary = BuildSettingsSummary();

                // 최초 로드는 전체를 남기고, 재로드는 바뀐 항목만 LogDiff가 남긴다.
                // 플레이할 때마다 이 긴 줄이 찍히면 로그가 못 쓰게 되므로 재로드 시엔 상세 레벨로 내린다.
                if (isReload)
                    ModLog.Verbose($"[SaveCustomKey] 재로드 후 전체 설정 - {summary}");
                else
                    MelonLogger.Msg($"[SaveCustomKey] 설정 로드 완료 - {summary}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파싱 실패: {ex.Message}");
            }
        }

        /// <summary>NoteSpeedChaosMin이 Max보다 크면 두 값을 맞바꾼다.</summary>
        private static void NormalizeNoteSpeedChaosRange()
        {
            if (NoteSpeedChaosMin <= NoteSpeedChaosMax)
                return;

            MelonLogger.Warning($"[SaveCustomKey] NoteSpeedChaosMin({NoteSpeedChaosMin:0.##})이 Max({NoteSpeedChaosMax:0.##})보다 큽니다 → 두 값을 맞바꿉니다.");
            float swap = NoteSpeedChaosMin;
            NoteSpeedChaosMin = NoteSpeedChaosMax;
            NoteSpeedChaosMax = swap;
        }

        /// <summary>로드 직후 로그에 남기는 현재 설정 한 줄 요약.</summary>
        private static string BuildSettingsSummary()
        {
            string shapeStr = JudgmentBarShape == 2 ? "삼각(2)" : (JudgmentBarShape == 1 ? "캡슐(1)" : "사각(0)");
            string rangeShapeStr = JudgmentBarRangeShape == -1 ? "추종(-1)" : (JudgmentBarRangeShape == 2 ? "삼각(2)" : (JudgmentBarRangeShape == 1 ? "캡슐(1)" : "사각(0)"));
            return $"AutoPlay={(AutoPlay ? "켜짐(1)" : "꺼짐(0)")}, AllPerfect={(AllPerfect ? "켜짐(1)" : "꺼짐(0)")}, BlockSave={(BlockSave ? "켜짐(1)" : "꺼짐(0)")}, JudgmentBar={(EnableJudgmentBar ? "켜짐(1)" : "꺼짐(0)")}, Vertical={(JudgmentBarVertical ? "세로(1)" : "가로(0)")}, Shape={shapeStr}(범위:{rangeShapeStr}), Side={JudgmentBarSide}, KeyViewer={(EnableKeyViewer ? "켜짐(1)" : "꺼짐(0)")}, MaxScore={MaxScore:0.###}{(IsMaxScoreCustom ? " (커스텀)" : " (기본)")}, NoteSway={(EnableNoteSway ? $"켜짐(폭 {NoteSwayAmplitude:0.#}px, 속도 {NoteSwaySpeed:0.##}Hz, 감쇠 {(NoteSwayDamping ? $"{NoteSwayDampingTime:0.##}초" : "없음")})" : "꺼짐(0)")}, NoteSpeedChaos={(EnableNoteSpeedChaos ? $"켜짐(배율 {NoteSpeedChaosMin:0.##}~{NoteSpeedChaosMax:0.##}, {(NoteSpeedChaosPerLane ? "레인별" : "노트별")})" : "꺼짐(0)")}";
        }

        /// <summary>재로드 전후를 비교해 실제로 바뀐 항목만 로그로 남기기 위한 스냅샷.</summary>
        private static Dictionary<string, string> Snapshot()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "AutoPlay", OnOffText(AutoPlay) },
                { "AllPerfect", OnOffText(AllPerfect) },
                { "BlockSave", OnOffText(BlockSave) },
                { "EnableJudgmentBar", OnOffText(EnableJudgmentBar) },
                { "JudgmentBarVertical", JudgmentBarVertical ? "세로(1)" : "가로(0)" },
                { "JudgmentBarShape", ShapeText(JudgmentBarShape) },
                { "JudgmentBarRangeShape", ShapeText(JudgmentBarRangeShape) },
                { "JudgmentBarSide", JudgmentBarSide ?? "(null)" },
                { "EnableKeyViewer", OnOffText(EnableKeyViewer) },
                { "KeyViewerPressedColor", ColorText(KeyViewerPressedColor) },
                { "KeyViewerNormalColor", ColorText(KeyViewerNormalColor) },
                { "KeyViewerGatePressedColor", ColorText(KeyViewerGatePressedColor) },
                { "MaxScore", NumText(MaxScore, "0.###") },
                { "NoteSway", OnOffText(EnableNoteSway) },
                { "NoteSwayAmplitude", NumText(NoteSwayAmplitude, "0.##") },
                { "NoteSwaySpeed", NumText(NoteSwaySpeed, "0.##") },
                { "NoteSwayDamping", OnOffText(NoteSwayDamping) },
                { "NoteSwayDampingTime", NumText(NoteSwayDampingTime, "0.##") },
                { "NoteSpeedChaos", OnOffText(EnableNoteSpeedChaos) },
                { "NoteSpeedChaosMin", NumText(NoteSpeedChaosMin, "0.##") },
                { "NoteSpeedChaosMax", NumText(NoteSpeedChaosMax, "0.##") },
                { "NoteSpeedChaosPerLane", NoteSpeedChaosPerLane ? "레인별(1)" : "노트별(0)" }
            };
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

        private static string OnOffText(bool value) => value ? "켜짐(1)" : "꺼짐(0)";

        private static string ShapeText(int shape)
        {
            switch (shape)
            {
                case -1: return "추종(-1)";
                case 1: return "캡슐(1)";
                case 2: return "삼각(2)";
                default: return $"사각({shape})";
            }
        }

        private static string ColorText(Color color) => "#" + ColorUtility.ToHtmlStringRGBA(color);

        private static string NumText(float value, string format) => value.ToString(format, CultureInfo.InvariantCulture);

        public static bool IsMaxScoreCustom => Math.Abs(MaxScore - DefaultMaxScore) > 0.001f;

        private static void AppendSectionsMissingFrom(string filePath, HashSet<string> seenKeys)
        {
            var sections = new List<Action<StringBuilder>>();
            var names = new List<string>();

            if (!seenKeys.Contains("JudgmentBarCapsule") && !seenKeys.Contains("JudgmentBarShape"))
            {
                sections.Add(AppendJudgmentBarCapsuleSection);
                names.Add("JudgmentBarCapsule");
            }

            if (!seenKeys.Contains("JudgmentBarSide") && !seenKeys.Contains("JudgmentBarPosition"))
            {
                sections.Add(AppendJudgmentBarSideSection);
                names.Add("JudgmentBarSide");
            }

            if (!seenKeys.Contains("KeyViewerPressedColor") && !seenKeys.Contains("KeyViewerNormalColor"))
            {
                sections.Add(AppendKeyViewerColorSection);
                names.Add("KeyViewerColor");
            }

            if (!seenKeys.Contains("MaxScore") && !seenKeys.Contains("ScoreLimit"))
            {
                sections.Add(AppendMaxScoreSection);
                names.Add("MaxScore");
            }

            if (!seenKeys.Contains("NoteSway") && !seenKeys.Contains("EnableNoteSway"))
            {
                sections.Add(AppendNoteSwaySection);
                names.Add("NoteSway");
            }

            if (!seenKeys.Contains("NoteSpeedChaos") && !seenKeys.Contains("EnableNoteSpeedChaos"))
            {
                sections.Add(AppendNoteSpeedChaosSection);
                names.Add("NoteSpeedChaos");
            }

            if (sections.Count > 0)
                AppendMissingSections(filePath, sections, string.Join(", ", names.ToArray()));
        }

        /// <summary>한글/영문 색상 이름 → 색. 알파는 이름마다 0.85~0.9로 고정이다(투명도를 정하려면 #RRGGBBAA나 R,G,B,A를 쓴다).</summary>
        private static readonly Dictionary<string, Color> NamedColors = BuildNamedColors();

        private static Dictionary<string, Color> BuildNamedColors()
        {
            var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);

            void Add(Color color, params string[] names)
            {
                foreach (var name in names)
                    map[name] = color;
            }

            Add(new Color(0.15f, 0.75f, 0.85f, 0.85f), "cyan", "시안", "청록", "청록색", "민트", "민트색");
            Add(new Color(1.0f, 0.25f, 0.5f, 0.9f), "magenta", "pink", "마젠타", "분홍", "분홍색", "핑크", "핑", "핫핑크");
            Add(new Color(1.0f, 0.85f, 0.20f, 0.9f), "yellow", "노랑", "노란색", "황색");
            Add(new Color(0.95f, 0.25f, 0.25f, 0.9f), "red", "빨강", "빨간색", "적색");
            Add(new Color(0.20f, 0.50f, 0.85f, 0.9f), "blue", "파랑", "파란색", "청색");
            Add(new Color(0.25f, 0.85f, 0.35f, 0.9f), "green", "초록", "초록색", "녹색");
            Add(new Color(0.95f, 0.95f, 0.95f, 0.9f), "white", "흰색", "하양", "백색");
            Add(new Color(0.08f, 0.08f, 0.08f, 0.85f), "black", "검정", "검은색", "흑색");
            Add(new Color(1.0f, 0.55f, 0.15f, 0.9f), "orange", "주황", "주황색");
            Add(new Color(0.65f, 0.35f, 0.85f, 0.9f), "purple", "violet", "보라", "보라색", "자색");
            Add(new Color(0.40f, 0.75f, 1.0f, 0.9f), "sky", "skyblue", "하늘", "하늘색");

            return map;
        }

        public static Color ParseColorSetting(string key, string val, Color defaultColor)
        {
            if (string.IsNullOrEmpty(val))
                return defaultColor;

            string s = val.Trim().ToLowerInvariant();

            // 1. 한글 / 영문 색상 키워드
            if (NamedColors.TryGetValue(s, out Color named))
                return named;

            // 2. HTML 헥스코드 (#RRGGBB, #RRGGBBAA, RRGGBB, RRGGBBAA)
            string hexCandidate = s.StartsWith("#") ? s : "#" + s;
            if (ColorUtility.TryParseHtmlString(hexCandidate, out Color parsedColor))
                return parsedColor;

            // 3. 쉼표 구분 RGBA (예: 255,128,0 또는 0.15,0.75,0.85,0.85)
            if (TryParseRgbaList(s, out Color listColor))
                return listColor;

            MelonLogger.Warning($"[SaveCustomKey] {key} 색상 값을 읽지 못했습니다: \"{val}\" → 기본값 유지");
            return defaultColor;
        }

        /// <summary>
        /// `R,G,B` 또는 `R,G,B,A`. 값 중 하나라도 1보다 크면 0~255 범위로, 모두 1 이하면 0~1 범위로 본다
        /// (그래서 `1,1,1`은 흰색이고 `1,0,0`은 0~1 범위의 빨강이다).
        /// </summary>
        private static bool TryParseRgbaList(string text, out Color color)
        {
            color = default(Color);

            var parts = text.Split(',');
            if (parts.Length != 3 && parts.Length != 4)
                return false;

            var values = new float[4] { 0f, 0f, 0f, 1f };
            for (int i = 0; i < parts.Length; i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }

            float r = values[0], g = values[1], b = values[2], a = values[3];
            if (r > 1f || g > 1f || b > 1f || a > 1f)
            {
                r = Mathf.Clamp01(r / 255f);
                g = Mathf.Clamp01(g / 255f);
                b = Mathf.Clamp01(b / 255f);
                a = a > 1f ? Mathf.Clamp01(a / 255f) : a;
            }

            color = new Color(r, g, b, a);
            return true;
        }

        public static bool ParseFlexibleBool(string val, bool defaultValue)
            => ConfigParsing.ParseFlexibleBool(val, defaultValue);
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
