using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Loaders;

namespace sxtg2.Helpers.Track
{
    public static class CustomTrackHelper
    {
        private const string DEFAULT_CUSTOM_TITLE = "커스텀 차트";
        private static string _cachedCustomTitle = null;
        private static string _lastTrackId = null;
        
        // 현재 선택된 커스텀 트랙 정보 캐싱
        private static string _selectedTrackId = null;
        private static string _selectedDisplayName = null;
        private static string _selectedAlbumFolder = null;
        private static bool _isCustomPlayActive = false;
        
        /// <summary>
        /// 현재 선택된 커스텀 트랙 정보를 저장합니다.
        /// </summary>
        public static void SetSelectedTrack(string trackId, string displayName, string albumFolder)
        {
            _selectedTrackId = trackId;
            _selectedDisplayName = displayName;
            _selectedAlbumFolder = albumFolder;
            _isCustomPlayActive = true;
            MelonLogger.Msg($"[CustomTrackHelper] 선택된 트랙 캐싱: ID={trackId}, DisplayName={displayName}, AlbumFolder={albumFolder}");
        }

        /// <summary>
        /// 현재 커스텀 플레이 상태를 해제합니다.
        /// </summary>
        public static void ClearSelectedTrack()
        {
            _selectedTrackId = null;
            _selectedDisplayName = null;
            _selectedAlbumFolder = null;
            _isCustomPlayActive = false;
            MelonLogger.Msg("[CustomTrackHelper] 선택된 커스텀 트랙 캐시 해제");
        }
        
        /// <summary>
        /// 현재 선택된 커스텀 트랙 정보를 가져옵니다.
        /// </summary>
        public static (string TrackId, string DisplayName, string AlbumFolder) GetSelectedTrack()
        {
            return (_selectedTrackId, _selectedDisplayName, _selectedAlbumFolder);
        }
        
        /// <summary>
        /// 선택된 트랙 정보가 있는지 확인합니다.
        /// </summary>
        public static bool HasSelectedTrack()
        {
            return !string.IsNullOrEmpty(_selectedTrackId);
        }

        /// <summary>
        /// 현재 플레이 흐름이 커스텀 차트 세션인지 확인합니다.
        /// </summary>
        public static bool IsCustomPlayActive()
        {
            return _isCustomPlayActive && HasSelectedTrack();
        }

        /// <summary>
        /// 트랙이 커스텀 트랙인지 확인합니다.
        /// DisplayName이 info.txt 파일에 설정된 제목과 일치할 때만 커스텀 트랙으로 판단합니다.
        /// </summary>
        public static bool IsCustomTrack(object trackData)
        {
            if (trackData == null)
                return false;

            try
            {
                string displayName = ReflectionHelper.GetFirstMemberValueSafe(trackData, ReflectionMemberNames.TrackData.DisplayName) as string;

                if (string.IsNullOrEmpty(displayName))
                {
                    return false;
                }

                // hwa 폴더의 모든 앨범 폴더에서 info.txt 파일 검색
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                if (!Directory.Exists(hwaFolder))
                {
                    return false;
                }

                // 앨범 폴더들 검색
                var albumFolders = Directory.GetDirectories(hwaFolder);
                foreach (var albumFolder in albumFolders)
                {
                    // txt 파일 검색 (info.txt, trackinfo.txt 등)
                    var txtFiles = Directory.GetFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly);
                    foreach (var txtFile in txtFiles)
                    {
                        try
                        {
                            // txt 파일에서 곡 정보 파싱
                            var trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, Path.GetFileNameWithoutExtension(txtFile));
                            
                            // DisplayName이 info.txt의 제목과 일치하는지 확인
                            if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title == displayName)
                            {
                                MelonLogger.Msg($"[CustomTrackHelper] 커스텀 트랙 확인: DisplayName='{displayName}' == info.txt 제목='{trackInfo.Title}'");
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 개별 파일 파싱 실패는 무시하고 계속 진행
                            MelonLogger.Warning($"[CustomTrackHelper] 파일 파싱 실패 ({txtFile}): {ex.Message}");
                        }
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomTrackHelper] 커스텀 트랙 확인 중 오류: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 커스텀 트랙의 제목을 가져옵니다.
        /// </summary>
        public static string GetCustomTitle(string trackId)
        {
            if (string.IsNullOrEmpty(trackId))
                return DEFAULT_CUSTOM_TITLE;

            // 캐시 확인
            if (_lastTrackId == trackId && !string.IsNullOrEmpty(_cachedCustomTitle))
            {
                return _cachedCustomTitle;
            }

            try
            {
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                if (Directory.Exists(hwaFolder))
                {
                    // txt 파일에서 곡 정보 파싱
                    var trackInfo = TrackInfoParser.ParseTrackInfo(hwaFolder, trackId);
                    _cachedCustomTitle = !string.IsNullOrEmpty(trackInfo.Title) ? trackInfo.Title : DEFAULT_CUSTOM_TITLE;
                    _lastTrackId = trackId;
                    return _cachedCustomTitle;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomTrackHelper] 커스텀 제목 가져오기 중 오류: {ex.Message}");
            }

            return DEFAULT_CUSTOM_TITLE;
        }

        /// <summary>
        /// 캐시를 리셋합니다.
        /// </summary>
        public static void ResetCache()
        {
            _cachedCustomTitle = null;
            _lastTrackId = null;
        }
    }
}

















