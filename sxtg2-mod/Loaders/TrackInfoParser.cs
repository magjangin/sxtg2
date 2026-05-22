using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;

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
                    MelonLogger.Warning($"[TrackInfoParser] 앨범 폴더가 존재하지 않습니다: {albumFolder}");
                    return trackInfo;
                }

                // txt 파일 찾기
                string targetFile = FindTrackInfoFile(albumFolder, searchKey);
                if (string.IsNullOrEmpty(targetFile) || !File.Exists(targetFile))
                {
                    MelonLogger.Msg($"[TrackInfoParser] 곡 정보 파일을 찾을 수 없습니다: {searchKey}");
                    return trackInfo;
                }

                MelonLogger.Msg($"[TrackInfoParser] 곡 정보 파일 발견: {Path.GetFileName(targetFile)}");

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
                // txt 파일 목록 가져오기
                var txtFiles = Directory.GetFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly);
                if (txtFiles.Length == 0)
                {
                    return null;
                }

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
        /// txt 파일 내용을 파싱하여 TrackInfo에 저장합니다.
        /// </summary>
        private static void ParseTrackInfoFile(string filePath, TrackInfo trackInfo)
        {
            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        // 주석 라인 무시
                        if (line.StartsWith("#") || line.StartsWith("//"))
                            continue;

                        // 키:값 형식 파싱
                        int colonIndex = line.IndexOf(':');
                        if (colonIndex < 0)
                            continue;

                        string key = line.Substring(0, colonIndex).Trim();
                        string value = line.Substring(colonIndex + 1).Trim();

                        if (string.IsNullOrEmpty(value))
                            continue;

                        // 키 이름 매칭 및 값 설정
                        string keyLower = key.ToLower();
                        
                        if (keyLower.Contains("곡 제목") || keyLower.Contains("title") || keyLower == "제목")
                        {
                            trackInfo.Title = value;
                            MelonLogger.Msg($"[TrackInfoParser] 제목: {value}");
                        }
                        else if (keyLower.Contains("아티스트") || keyLower.Contains("artist") || keyLower == "작곡가")
                        {
                            trackInfo.Artist = value;
                            MelonLogger.Msg($"[TrackInfoParser] 아티스트: {value}");
                        }
                        else if (keyLower.Contains("난이도") || keyLower.Contains("difficulty") || keyLower.Contains("level"))
                        {
                            ParseDifficulties(value, trackInfo.Difficulties);
                            MelonLogger.Msg($"[TrackInfoParser] 난이도: [{string.Join(", ", trackInfo.Difficulties)}]");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TrackInfoParser] 파일 파싱 중 오류: {ex.Message}");
            }
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
                MelonLogger.Warning($"[TrackInfoParser] 난이도 파싱 실패: {ex.Message}");
            }
        }
    }
}
