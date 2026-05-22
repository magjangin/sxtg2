using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;
using sxtg2.Loaders;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static readonly Dictionary<string, string> AlbumFolderByDisplayNameCache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> AlbumFolderByTrackIdCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string[] BmsExtensions = { "*.bms", "*.bme", "*.bml" };

        /// <summary>
        /// DisplayName을 기반으로 앨범 폴더를 찾습니다.
        /// </summary>
        private static string FindAlbumFolderByDisplayName(string displayName, string trackId)
        {
            try
            {
                string gamePath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                if (!Directory.Exists(hwaFolder))
                {
                    return hwaFolder; // 기본값 반환
                }

                if (TryGetCachedAlbumFolder(hwaFolder, displayName, trackId, out var cachedFolder))
                {
                    return cachedFolder;
                }

                // 1. 모든 앨범 폴더에서 txt 파일 검색하여 DisplayName과 매칭
                string albumFolder = FindAlbumByDisplayName(hwaFolder, displayName);
                if (albumFolder != null)
                {
                    CacheAlbumFolder(displayName, trackId, albumFolder);
                    return albumFolder;
                }

                // 2. Track ID 기반으로 찾기
                if (!string.IsNullOrEmpty(trackId))
                {
                    albumFolder = FindAlbumByTrackId(hwaFolder, trackId);
                    if (albumFolder != null)
                    {
                        CacheAlbumFolder(displayName, trackId, albumFolder);
                        return albumFolder;
                    }
                }

                // 3. 기본 hwa 폴더 반환
                return hwaFolder;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 앨범 폴더 찾기 실패: {ex.Message}");
                string gamePath = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                return Path.Combine(gamePath, "hwa");
            }
        }

        private static string FindAlbumByDisplayName(string hwaFolder, string displayName)
        {
            foreach (var albumFolder in Directory.EnumerateDirectories(hwaFolder))
            {
                foreach (var txtFile in Directory.EnumerateFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, Path.GetFileNameWithoutExtension(txtFile));
                        if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title == displayName)
                        {
                            MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 발견 (DisplayName 매칭): {Path.GetFileName(albumFolder)}");
                            return albumFolder;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ManagerMusicSelectHook] 트랙 정보 파싱 실패 ({txtFile}): {ex.Message}");
                    }
                }
            }
            return null;
        }

        private static string FindAlbumByTrackId(string hwaFolder, string trackId)
        {
            foreach (var albumFolder in Directory.EnumerateDirectories(hwaFolder))
            {
                foreach (var extension in BmsExtensions)
                {
                    string pattern = $"{trackId}{extension.Substring(1)}";
                    foreach (var _ in Directory.EnumerateFiles(albumFolder, pattern, SearchOption.TopDirectoryOnly))
                    {
                        MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 발견 (Track ID 매칭): {Path.GetFileName(albumFolder)}");
                        return albumFolder;
                    }
                }
            }
            return null;
        }

        private static bool TryGetCachedAlbumFolder(string hwaFolder, string displayName, string trackId, out string albumFolder)
        {
            albumFolder = null;

            if (!string.IsNullOrEmpty(trackId) &&
                AlbumFolderByTrackIdCache.TryGetValue(trackId, out var byTrackId) &&
                IsValidCachedAlbumFolder(hwaFolder, byTrackId, displayName, trackId))
            {
                albumFolder = byTrackId;
                return true;
            }

            if (!string.IsNullOrEmpty(displayName) &&
                AlbumFolderByDisplayNameCache.TryGetValue(displayName, out var byDisplayName) &&
                IsValidCachedAlbumFolder(hwaFolder, byDisplayName, displayName, trackId))
            {
                albumFolder = byDisplayName;
                return true;
            }

            return false;
        }

        private static bool IsValidCachedAlbumFolder(string hwaFolder, string albumFolder, string displayName, string trackId)
        {
            if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
                return false;

            if (!albumFolder.StartsWith(hwaFolder, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrEmpty(trackId))
            {
                foreach (var extension in BmsExtensions)
                {
                    string pattern = $"{trackId}{extension.Substring(1)}";
                    foreach (var _ in Directory.EnumerateFiles(albumFolder, pattern, SearchOption.TopDirectoryOnly))
                        return true;
                }
                return false;
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                return FolderHasDisplayName(albumFolder, displayName);
            }

            return true;
        }

        private static bool FolderHasDisplayName(string albumFolder, string displayName)
        {
            foreach (var txtFile in Directory.EnumerateFiles(albumFolder, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, Path.GetFileNameWithoutExtension(txtFile));
                    if (!string.IsNullOrEmpty(trackInfo.Title) && trackInfo.Title == displayName)
                        return true;
                }
                catch
                {
                    // 캐시 검증 실패는 무효로 취급하고 재탐색한다.
                }
            }

            return false;
        }

        private static void CacheAlbumFolder(string displayName, string trackId, string albumFolder)
        {
            if (string.IsNullOrEmpty(albumFolder))
                return;

            if (!string.IsNullOrEmpty(displayName))
                AlbumFolderByDisplayNameCache[displayName] = albumFolder;

            if (!string.IsNullOrEmpty(trackId))
                AlbumFolderByTrackIdCache[trackId] = albumFolder;
        }
    }
}

