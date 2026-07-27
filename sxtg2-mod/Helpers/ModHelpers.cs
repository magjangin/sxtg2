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

        public static bool AutoPlay { get; set; } = false;
        public static bool AllPerfect { get; set; } = false;
        public static bool BlockSave { get; set; } = true;
        public static bool EnableJudgmentBar { get; set; } = true;
        public static bool JudgmentBarVertical { get; set; } = true;
        public static bool EnableKeyViewer { get; set; } = true;
        public static float MaxScore { get; set; } = DefaultMaxScore;

        public static bool EnableNoteSway { get; set; } = false;
        public static float NoteSwayAmplitude { get; set; } = 12f;
        public static float NoteSwaySpeed { get; set; } = 0.8f;
        public static bool NoteSwayDamping { get; set; } = true;
        public static float NoteSwayDampingTime { get; set; } = 0.4f;

        public static bool EnableNoteSpeedChaos { get; set; } = false;
        public static float NoteSpeedChaosMin { get; set; } = 0.6f;
        public static float NoteSpeedChaosMax { get; set; } = 1.8f;
        public static bool NoteSpeedChaosPerLane { get; set; } = false;

        public static void EnsureInitialized()
        {
            if (!_initialized)
                Initialize();
        }

        public static void Initialize()
        {
            _initialized = true;

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

                LoadConfigFile(configFilePath);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 초기화 중 오류: {ex.Message}");
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
            sb.AppendLine("# 실시간 키뷰어 표시 (1 = 켜짐, 0 = 꺼짐)");
            sb.AppendLine("EnableKeyViewer=1");
            AppendMaxScoreSection(sb);
            AppendNoteSwaySection(sb);
            AppendNoteSpeedChaosSection(sb);

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            MelonLogger.Msg($"[SaveCustomKey] 기본 설정 파일 생성 완료: {filePath}");
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

        private static void LoadConfigFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
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
                    else if (key.Equals("EnableKeyViewer", StringComparison.OrdinalIgnoreCase) || key.Equals("KeyViewer", StringComparison.OrdinalIgnoreCase))
                    {
                        EnableKeyViewer = ParseFlexibleBool(val, EnableKeyViewer);
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

                MelonLogger.Msg($"[SaveCustomKey] 설정 로드 완료 - AutoPlay={(AutoPlay ? "켜짐(1)" : "꺼짐(0)")}, AllPerfect={(AllPerfect ? "켜짐(1)" : "꺼짐(0)")}, BlockSave={(BlockSave ? "켜짐(1)" : "꺼짐(0)")}, JudgmentBar={(EnableJudgmentBar ? "켜짐(1)" : "꺼짐(0)")}, Vertical={(JudgmentBarVertical ? "세로(1)" : "가로(0)")}, KeyViewer={(EnableKeyViewer ? "켜짐(1)" : "꺼짐(0)")}, MaxScore={MaxScore:0.###}{(IsMaxScoreCustom ? " (커스텀)" : " (기본)")}, NoteSway={(EnableNoteSway ? $"켜짐(폭 {NoteSwayAmplitude:0.#}px, 속도 {NoteSwaySpeed:0.##}Hz, 감쇠 {(NoteSwayDamping ? $"{NoteSwayDampingTime:0.##}초" : "없음")})" : "꺼짐(0)")}, NoteSpeedChaos={(EnableNoteSpeedChaos ? $"켜짐(배율 {NoteSpeedChaosMin:0.##}~{NoteSpeedChaosMax:0.##}, {(NoteSpeedChaosPerLane ? "레인별" : "노트별")})" : "꺼짐(0)")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 읽기 실패: {ex.Message}");
            }
        }

        public static bool IsMaxScoreCustom => Math.Abs(MaxScore - DefaultMaxScore) > 0.001f;

        private static void AppendSectionsMissingFrom(string filePath, HashSet<string> seenKeys)
        {
            var sections = new List<Action<StringBuilder>>();
            var names = new List<string>();

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

        public static bool ParseFlexibleBool(string val, bool defaultValue)
        {
            if (string.IsNullOrEmpty(val))
                return defaultValue;

            var s = val.Trim().ToLowerInvariant();

            if (s == "1" || s == "true" || s == "t" || s == "on" || s == "켜짐" || s == "사용" || s == "활성화" || s == "enable" || s == "enabled" || s == "yes" || s == "y")
                return true;

            if (s == "0" || s == "false" || s == "f" || s == "off" || s == "꺼짐" || s == "미사용" || s == "비활성화" || s == "disable" || s == "disabled" || s == "no" || s == "n")
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
