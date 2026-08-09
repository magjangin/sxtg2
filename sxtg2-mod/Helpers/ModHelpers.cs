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
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;

                    var parts = trimmed.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var val = parts[1].Trim();
                    seenKeys.Add(key);

                    if (key.Equals("AutoPlay", StringComparison.OrdinalIgnoreCase))
                    {
                        AutoPlay = ParseFlexibleBool(val, AutoPlay);
                    }
                    else if (key.Equals("AllPerfect", StringComparison.OrdinalIgnoreCase))
                    {
                        AllPerfect = ParseFlexibleBool(val, AllPerfect);
                    }
                    else if (key.Equals("BlockSave", StringComparison.OrdinalIgnoreCase))
                    {
                        BlockSave = ParseFlexibleBool(val, BlockSave);
                    }
                    else if (key.Equals("EnableJudgmentBar", StringComparison.OrdinalIgnoreCase) || key.Equals("JudgmentBar", StringComparison.OrdinalIgnoreCase))
                    {
                        EnableJudgmentBar = ParseFlexibleBool(val, EnableJudgmentBar);
                    }
                    else if (key.Equals("JudgmentBarVertical", StringComparison.OrdinalIgnoreCase))
                    {
                        JudgmentBarVertical = ParseFlexibleBool(val, JudgmentBarVertical);
                    }
                    else if (key.Equals("JudgmentBarShape", StringComparison.OrdinalIgnoreCase))
                    {
                        JudgmentBarShape = ParseShapeSetting("JudgmentBarShape", val, JudgmentBarShape);
                    }
                    else if (key.Equals("JudgmentBarRangeShape", StringComparison.OrdinalIgnoreCase))
                    {
                        JudgmentBarRangeShape = ParseShapeSetting("JudgmentBarRangeShape", val, JudgmentBarRangeShape);
                    }
                    else if (key.Equals("JudgmentBarCapsule", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isCap = ParseFlexibleBool(val, JudgmentBarCapsule);
                        JudgmentBarCapsule = isCap;
                    }
                    else if (key.Equals("JudgmentBarSide", StringComparison.OrdinalIgnoreCase) || key.Equals("JudgmentBarPosition", StringComparison.OrdinalIgnoreCase))
                    {
                        JudgmentBarSide = ParseSideSetting("JudgmentBarSide", val, JudgmentBarSide);
                    }
                    else if (key.Equals("EnableKeyViewer", StringComparison.OrdinalIgnoreCase) || key.Equals("KeyViewer", StringComparison.OrdinalIgnoreCase))
                    {
                        EnableKeyViewer = ParseFlexibleBool(val, EnableKeyViewer);
                    }
                    else if (key.Equals("KeyViewerPressedColor", StringComparison.OrdinalIgnoreCase))
                    {
                        KeyViewerPressedColor = ParseColorSetting("KeyViewerPressedColor", val, KeyViewerPressedColor);
                    }
                    else if (key.Equals("KeyViewerNormalColor", StringComparison.OrdinalIgnoreCase))
                    {
                        KeyViewerNormalColor = ParseColorSetting("KeyViewerNormalColor", val, KeyViewerNormalColor);
                    }
                    else if (key.Equals("KeyViewerGatePressedColor", StringComparison.OrdinalIgnoreCase))
                    {
                        KeyViewerGatePressedColor = ParseColorSetting("KeyViewerGatePressedColor", val, KeyViewerGatePressedColor);
                    }
                    else if (key.Equals("MaxScore", StringComparison.OrdinalIgnoreCase) || key.Equals("ScoreLimit", StringComparison.OrdinalIgnoreCase))
                    {
                        MaxScore = ParseFloatSetting("MaxScore", val, MaxScore, 0.001f, float.MaxValue);
                    }
                    else if (key.Equals("NoteSway", StringComparison.OrdinalIgnoreCase) || key.Equals("EnableNoteSway", StringComparison.OrdinalIgnoreCase))
                    {
                        EnableNoteSway = ParseFlexibleBool(val, EnableNoteSway);
                    }
                    else if (key.Equals("NoteSwayAmplitude", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSwayAmplitude = ParseFloatSetting("NoteSwayAmplitude", val, NoteSwayAmplitude, 0f, 1000f);
                    }
                    else if (key.Equals("NoteSwaySpeed", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSwaySpeed = ParseFloatSetting("NoteSwaySpeed", val, NoteSwaySpeed, 0f, 50f);
                    }
                    else if (key.Equals("NoteSwayDamping", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSwayDamping = ParseFlexibleBool(val, NoteSwayDamping);
                    }
                    else if (key.Equals("NoteSwayDampingTime", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSwayDampingTime = ParseFloatSetting("NoteSwayDampingTime", val, NoteSwayDampingTime, 0.01f, 30f);
                    }
                    else if (key.Equals("NoteSpeedChaos", StringComparison.OrdinalIgnoreCase) || key.Equals("EnableNoteSpeedChaos", StringComparison.OrdinalIgnoreCase))
                    {
                        EnableNoteSpeedChaos = ParseFlexibleBool(val, EnableNoteSpeedChaos);
                    }
                    else if (key.Equals("NoteSpeedChaosMin", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSpeedChaosMin = ParseFloatSetting("NoteSpeedChaosMin", val, NoteSpeedChaosMin, 0.05f, 10f);
                    }
                    else if (key.Equals("NoteSpeedChaosMax", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSpeedChaosMax = ParseFloatSetting("NoteSpeedChaosMax", val, NoteSpeedChaosMax, 0.05f, 10f);
                    }
                    else if (key.Equals("NoteSpeedChaosPerLane", StringComparison.OrdinalIgnoreCase))
                    {
                        NoteSpeedChaosPerLane = ParseFlexibleBool(val, NoteSpeedChaosPerLane);
                    }
                }

                if (NoteSpeedChaosMin > NoteSpeedChaosMax)
                {
                    MelonLogger.Warning($"[SaveCustomKey] NoteSpeedChaosMin({NoteSpeedChaosMin:0.##})이 Max({NoteSpeedChaosMax:0.##})보다 큽니다 → 두 값을 맞바꿉니다.");
                    float swap = NoteSpeedChaosMin;
                    NoteSpeedChaosMin = NoteSpeedChaosMax;
                    NoteSpeedChaosMax = swap;
                }

                AppendSectionsMissingFrom(filePath, seenKeys);

                string shapeStr = JudgmentBarShape == 2 ? "삼각(2)" : (JudgmentBarShape == 1 ? "캡슐(1)" : "사각(0)");
                string rangeShapeStr = JudgmentBarRangeShape == -1 ? "추종(-1)" : (JudgmentBarRangeShape == 2 ? "삼각(2)" : (JudgmentBarRangeShape == 1 ? "캡슐(1)" : "사각(0)"));
                string summary = $"AutoPlay={(AutoPlay ? "켜짐(1)" : "꺼짐(0)")}, AllPerfect={(AllPerfect ? "켜짐(1)" : "꺼짐(0)")}, BlockSave={(BlockSave ? "켜짐(1)" : "꺼짐(0)")}, JudgmentBar={(EnableJudgmentBar ? "켜짐(1)" : "꺼짐(0)")}, Vertical={(JudgmentBarVertical ? "세로(1)" : "가로(0)")}, Shape={shapeStr}(범위:{rangeShapeStr}), Side={JudgmentBarSide}, KeyViewer={(EnableKeyViewer ? "켜짐(1)" : "꺼짐(0)")}, MaxScore={MaxScore:0.###}{(IsMaxScoreCustom ? " (커스텀)" : " (기본)")}, NoteSway={(EnableNoteSway ? $"켜짐(폭 {NoteSwayAmplitude:0.#}px, 속도 {NoteSwaySpeed:0.##}Hz, 감쇠 {(NoteSwayDamping ? $"{NoteSwayDampingTime:0.##}초" : "없음")})" : "꺼짐(0)")}, NoteSpeedChaos={(EnableNoteSpeedChaos ? $"켜짐(배율 {NoteSpeedChaosMin:0.##}~{NoteSpeedChaosMax:0.##}, {(NoteSpeedChaosPerLane ? "레인별" : "노트별")})" : "꺼짐(0)")}";

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
