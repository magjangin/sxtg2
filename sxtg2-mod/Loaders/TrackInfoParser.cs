using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Loaders
{
    public static class TrackInfoParser
    {
        public class TrackInfo
        {
            public string Title { get; set; } = "";
            public string Artist { get; set; } = "";
            public List<int> Difficulties { get; set; } = new List<int>();
        }

        private static readonly string[] TRACK_INFO_FILE_NAMES =
            { "trackinfo.txt", "info.txt" };

        /// <summary>
        /// 앨범 폴더에서 txt 파일을 찾아 곡 정보를 파싱합니다.
        /// </summary>
        /// <param name="albumFolder">앨범 폴더 경로</param>
        /// <param name="searchKey">검색 키 (BMS 파일명 또는 Track ID)</param>
        /// <returns>파싱된 곡 정보</returns>
        public static TrackInfo ParseTrackInfo(string albumFolder, string searchKey)
        {
            var trackInfo = new TrackInfo();

            try
            {
                if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
                {
                    ModLog.Warning($"[TrackInfoParser] 앨범 폴더가 존재하지 않습니다: {albumFolder}");
                    return trackInfo;
                }

                // txt 파일 찾기
                string targetFile = FindTrackInfoFile(albumFolder, searchKey);
                if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
                {
                    // 곡 선택 화면에 들어갈 때마다 앨범마다 찍히므로 상세 로그로 둔다(제목은 폴더 이름으로 대체됨).
                    ModLog.Verbose($"[TrackInfoParser] 곡 정보 파일을 찾을 수 없습니다: {searchKey}");
                    return trackInfo;
                }

                ModLog.Verbose($"[TrackInfoParser] 곡 정보 파일 발견: {Path.GetFileName(targetFile)}");

                // 파일 내용 파싱
                ParseTrackInfoFile(targetFile, trackInfo);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TrackInfoParser] 곡 정보 파싱 중 오류: {ex.Message}");
            }

            return trackInfo;
        }

        /// <summary>
        /// 앨범 폴더에서 곡 정보 txt 파일을 찾습니다.
        /// </summary>
        private static string FindTrackInfoFile(string albumFolder, string searchKey)
        {
            try
            {
                // txt 파일 목록 가져오기 (순서가 보장되지 않으므로 이름순으로 정렬해 항상 같은 파일이 고르게 한다)
                var txtFiles = Directory.GetFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly);
                if (txtFiles.Length == 0)
                {
                    return null;
                }
                Array.Sort(txtFiles, StringComparer.OrdinalIgnoreCase);

                // 1. {searchKey}.txt 파일 찾기
                if (!string.IsNullOrEmpty(searchKey))
                {
                    string trackIdFileName = $"{searchKey}.txt";
                    var exactMatch = txtFiles.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(trackIdFileName, StringComparison.OrdinalIgnoreCase));
                    if (exactMatch != null)
                    {
                        return exactMatch;
                    }
                }

                // 2. 기본 파일명 찾기 (trackinfo.txt, info.txt)
                foreach (var defaultFileName in TRACK_INFO_FILE_NAMES)
                {
                    var defaultFile = txtFiles.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(defaultFileName, StringComparison.OrdinalIgnoreCase));
                    if (defaultFile != null)
                    {
                        return defaultFile;
                    }
                }

                // 3. 첫 번째 txt 파일 사용
                return txtFiles[0];
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TrackInfoParser] 파일 검색 중 오류: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 줄 목록을 읽는다. UTF-8(BOM 있어도 됨)이 아니면 경고를 남기고 깨진 글자가 섞인 채로 읽는다.
        /// 메모장에서 ANSI(CP949)로 저장한 파일은 `제목:` 키가 깨져서 아무 항목도 못 읽는데, 예전에는 경고도 없었다.
        /// </summary>
        private static string[] ReadLines(string filePath)
        {
            try
            {
                using (var reader = new StreamReader(filePath, new UTF8Encoding(false, true), true))
                {
                    var lines = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                        lines.Add(line);
                    return lines.ToArray();
                }
            }
            catch (DecoderFallbackException)
            {
                ModLog.Warning(
                    $"[TrackInfoParser] {Path.GetFileName(filePath)}가 UTF-8이 아닙니다(메모장 ANSI 저장?). 한글 키/값이 깨져 읽히지 않을 수 있으니 UTF-8로 다시 저장하세요.");
                return File.ReadAllLines(filePath);
            }
        }

        /// <summary>
        /// txt 파일 내용을 파싱하여 TrackInfo에 저장합니다.
        /// </summary>
        private static void ParseTrackInfoFile(string filePath, TrackInfo trackInfo)
        {
            try
            {
                int recognizedFields = 0;

                foreach (string rawLine in ReadLines(filePath))
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line))
                        continue;

                    // 주석 라인 무시
                    if (line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    // 키:값 형식 파싱
                    int colonIndex = line.IndexOf(':');
                    if (colonIndex < 0)
                        continue;

                    string key = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();

                    if (string.IsNullOrEmpty(value))
                        continue;

                    if (ApplyField(key.ToLowerInvariant(), value, trackInfo))
                        recognizedFields++;
                }

                if (recognizedFields == 0)
                {
                    ModLog.Warning(
                        $"[TrackInfoParser] {Path.GetFileName(filePath)}에서 제목/아티스트/난이도를 하나도 읽지 못했습니다. " +
                        "`제목: ...`, `아티스트: ...`, `난이도: 3, 7, 11, 14` 형식과 UTF-8 저장인지 확인하세요.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TrackInfoParser] 파일 파싱 중 오류: {ex.Message}");
            }
        }

        /// <summary>키 이름에 맞는 항목을 채우고, 인식했으면 true.</summary>
        private static bool ApplyField(string keyLower, string value, TrackInfo trackInfo)
        {
            // `subtitle:`처럼 title이 들어간 다른 키가 제목을 덮어쓰지 않게 부제는 건너뛴다.
            bool isSubtitle = keyLower.Contains("subtitle") || keyLower.Contains("sub title") || keyLower.Contains("부제");

            if (!isSubtitle && (keyLower.Contains("곡 제목") || keyLower.Contains("title") || keyLower == "제목"))
            {
                trackInfo.Title = value;
                ModLog.Verbose($"[TrackInfoParser] 제목: {value}");
                return true;
            }

            if (keyLower.Contains("아티스트") || keyLower.Contains("artist") || keyLower == "작곡가")
            {
                trackInfo.Artist = value;
                ModLog.Verbose($"[TrackInfoParser] 아티스트: {value}");
                return true;
            }

            if (keyLower.Contains("난이도") || keyLower.Contains("difficulty") || keyLower.Contains("level"))
            {
                ParseDifficulties(value, trackInfo.Difficulties);
                ModLog.Verbose($"[TrackInfoParser] 난이도: [{string.Join(", ", trackInfo.Difficulties)}]");
                return true;
            }

            return false;
        }

        /// <summary>
        /// 난이도 문자열을 파싱하여 정수 리스트로 변환합니다.
        /// 예: "1,2,3" 또는 "1 2 3" 또는 "1, 2, 3"
        /// </summary>
        private static void ParseDifficulties(string value, List<int> difficulties)
        {
            try
            {
                difficulties.Clear();

                // 쉼표 또는 공백으로 분리
                var parts = value.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in parts)
                {
                    string trimmed = part.Trim();
                    if (int.TryParse(trimmed, out int difficulty))
                    {
                        difficulties.Add(difficulty);
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[TrackInfoParser] 난이도 파싱 실패: {ex.Message}");
            }
        }
    }
}
