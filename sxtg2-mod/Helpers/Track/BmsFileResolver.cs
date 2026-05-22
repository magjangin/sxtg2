using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;

namespace sxtg2.Helpers.Track
{
    public static class BmsFileResolver
    {
        public static readonly string[] BmsExtensions = { "*.bms", "*.bme", "*.bml" };

        public static List<string> FindRootAndAlbumBmsFiles(string hwaFolder, string logPrefix = null)
        {
            var allBmsFiles = new List<string>();
            if (!Directory.Exists(hwaFolder))
            {
                return allBmsFiles;
            }

            AddBmsFilesFromFolder(allBmsFiles, hwaFolder, "hwa 루트 폴더", logPrefix);

            foreach (var albumFolder in Directory.GetDirectories(hwaFolder))
            {
                AddBmsFilesFromFolder(allBmsFiles, albumFolder, $"앨범 폴더 '{Path.GetFileName(albumFolder)}'", logPrefix);
            }

            return allBmsFiles;
        }

        public static List<string> GetAlbumFoldersOrRootWithBms(string hwaFolder)
        {
            var albumFolders = new List<string>();
            if (!Directory.Exists(hwaFolder))
            {
                return albumFolders;
            }

            try
            {
                albumFolders.AddRange(Directory.GetDirectories(hwaFolder));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BmsFileResolver] 앨범 폴더 스캔 실패: {ex.Message}");
            }

            if (albumFolders.Count == 0 && FindFirstInFolder(hwaFolder) != null)
            {
                albumFolders.Add(hwaFolder);
            }

            return albumFolders;
        }

        public static List<string> FindInFolder(string folder)
        {
            var files = new List<string>();
            if (!Directory.Exists(folder))
            {
                return files;
            }

            foreach (var extension in BmsExtensions)
            {
                files.AddRange(Directory.GetFiles(folder, extension, SearchOption.TopDirectoryOnly));
            }

            return files;
        }

        public static string FindForTrack(string trackId, string hwaFolder, string albumFolder = null)
        {
            if (!string.IsNullOrEmpty(albumFolder) && Directory.Exists(albumFolder))
            {
                var albumTrackMatch = FindByTrackIdInFolder(trackId, albumFolder);
                if (albumTrackMatch != null)
                {
                    return albumTrackMatch;
                }

                var firstAlbumFile = FindFirstInFolder(albumFolder);
                if (firstAlbumFile != null)
                {
                    return firstAlbumFile;
                }
            }

            var recursiveTrackMatch = FindByTrackIdRecursive(trackId, hwaFolder);
            if (recursiveTrackMatch != null)
            {
                return recursiveTrackMatch;
            }

            var firstAlbumMatch = FindFirstInAlbumFolders(hwaFolder);
            if (firstAlbumMatch != null)
            {
                return firstAlbumMatch;
            }

            return FindFirstInFolder(hwaFolder);
        }

        public static string FindByTrackIdInFolder(string trackId, string folder)
        {
            if (string.IsNullOrEmpty(trackId) || !Directory.Exists(folder))
            {
                return null;
            }

            foreach (var extension in BmsExtensions)
            {
                string pattern = $"{trackId}{extension.Substring(1)}";
                var match = Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        public static string FindFirstInFolder(string folder)
        {
            if (!Directory.Exists(folder))
            {
                return null;
            }

            foreach (var extension in BmsExtensions)
            {
                var match = Directory.EnumerateFiles(folder, extension, SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string FindByTrackIdRecursive(string trackId, string hwaFolder)
        {
            if (string.IsNullOrEmpty(trackId) || !Directory.Exists(hwaFolder))
            {
                return null;
            }

            foreach (var extension in BmsExtensions)
            {
                string pattern = $"{trackId}{extension.Substring(1)}";
                var match = Directory.EnumerateFiles(hwaFolder, pattern, SearchOption.AllDirectories).FirstOrDefault();
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static string FindFirstInAlbumFolders(string hwaFolder)
        {
            if (!Directory.Exists(hwaFolder))
            {
                return null;
            }

            foreach (var albumFolder in Directory.GetDirectories(hwaFolder))
            {
                var match = FindFirstInFolder(albumFolder);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static void AddBmsFilesFromFolder(List<string> target, string folder, string label, string logPrefix)
        {
            foreach (var extension in BmsExtensions)
            {
                var files = Directory.GetFiles(folder, extension, SearchOption.TopDirectoryOnly);
                if (files.Length > 0 && !string.IsNullOrEmpty(logPrefix))
                {
                    var ext = extension.Substring(1);
                    MelonLogger.Msg($"{logPrefix} {label}에서 {files.Length}개의 {ext} 파일 발견");
                    foreach (var file in files)
                    {
                        MelonLogger.Msg($"{logPrefix}   - {Path.GetFileName(file)}");
                    }
                }

                target.AddRange(files);
            }
        }
    }
}
