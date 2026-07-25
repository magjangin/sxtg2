using System;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Helpers
{
    /// <summary>
    /// SaveCustomKey/config.txt 설정 파일 읽기 및 유연한 불리언 파싱 도우미
    /// </summary>
    public static class SaveCustomKeyConfig
    {
        public static bool AutoPlay { get; set; } = false;
        public static bool AllPerfect { get; set; } = false;
        public static bool BlockSave { get; set; } = true;

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
                }

                MelonLogger.Msg($"[SaveCustomKey] 설정 로드 완료 - AutoPlay={(AutoPlay ? "켜짐(1)" : "꺼짐(0)")}, AllPerfect={(AllPerfect ? "켜짐(1)" : "꺼짐(0)")}, BlockSave={(BlockSave ? "켜짐(1)" : "꺼짐(0)")}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKey] 설정 파일 읽기 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 1/0, true/false, 켜짐/꺼짐, on/off, yes/no 등 유연한 불리언 파서
        /// </summary>
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
