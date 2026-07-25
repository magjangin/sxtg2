using System;
using System.Collections;
using System.Collections.Generic;
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
        public static bool AutoPlay { get; set; } = false;
        public static bool AllPerfect { get; set; } = false;
        public static bool BlockSave { get; set; } = true;
        public static bool EnableJudgmentBar { get; set; } = true;
        public static bool JudgmentBarVertical { get; set; } = true;
        public static bool EnableKeyViewer { get; set; } = true;

        public static void Initialize()
        {
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

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            MelonLogger.Msg($"[SaveCustomKey] 기본 설정 파일 생성 완료: {filePath}");
        }

        private static void LoadConfigFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath, Encoding.UTF8);
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
                }

                MelonLogger.Msg($"[SaveCustomKey] 설정 로드 완료 - AutoPlay={(AutoPlay ? "켜짐(1)" : "꺼짐(0)")}, AllPerfect={(AllPerfect ? "켜짐(1)" : "꺼짐(0)")}, BlockSave={(BlockSave ? "켜짐(1)" : "꺼짐(0)")}, JudgmentBar={(EnableJudgmentBar ? "켜짐(1)" : "꺼짐(0)")}, Vertical={(JudgmentBarVertical ? "세로(1)" : "가로(0)")}, KeyViewer={(EnableKeyViewer ? "켜짐(1)" : "꺼짐(0)")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 읽기 실패: {ex.Message}");
            }
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
