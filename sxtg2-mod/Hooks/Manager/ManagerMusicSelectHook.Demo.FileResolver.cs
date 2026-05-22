using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static string FindDemoFile(string trackId, string albumFolder, string hwaRootFolder)
        {
            string demoFile = null;
            bool isAlbumFolder = !albumFolder.Equals(hwaRootFolder, StringComparison.OrdinalIgnoreCase);

            if (isAlbumFolder && Directory.Exists(albumFolder))
            {
                demoFile = FindDemoInAlbumFolder(albumFolder);
                if (demoFile != null)
                {
                    return demoFile;
                }
            }

            demoFile = Path.Combine(hwaRootFolder, "demo.ogg");
            if (File.Exists(demoFile))
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] hwa 루트 폴더에서 demo.ogg 발견");
                return demoFile;
            }

            if (!isAlbumFolder)
            {
                return FindDemoInSubFolders(hwaRootFolder);
            }

            return null;
        }

        private static string FindDemoInAlbumFolder(string albumFolder)
        {
            var demoPatterns = new[] { "demo.ogg", "preview.ogg", "sample.ogg" };
            foreach (var pattern in demoPatterns)
            {
                string demoFile = Path.Combine(albumFolder, pattern);
                if (File.Exists(demoFile))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더에서 {pattern} 발견: {Path.GetFileName(demoFile)}");
                    return demoFile;
                }
            }

            LogAudioFilesInFolder(albumFolder);
            return null;
        }

        private static void LogAudioFilesInFolder(string folder)
        {
            try
            {
                var audioExtensions = new[] { "*.ogg", "*.mp3", "*.wav" };
                var allAudioFiles = new List<string>();
                foreach (var ext in audioExtensions)
                {
                    allAudioFiles.AddRange(Directory.GetFiles(folder, ext, SearchOption.TopDirectoryOnly));
                }

                if (allAudioFiles.Count > 0)
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더의 오디오 파일 목록 ({allAudioFiles.Count}개):");
                    foreach (var file in allAudioFiles)
                    {
                        MelonLogger.Msg($"  - {Path.GetFileName(file)}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 앨범 폴더 오디오 목록 로그 실패 ({folder}): {ex.Message}");
            }
        }

        private static string FindDemoInSubFolders(string hwaRootFolder)
        {
            var albumFolders = Directory.GetDirectories(hwaRootFolder);
            MelonLogger.Msg($"[ManagerMusicSelectHook] {albumFolders.Length}개의 앨범 폴더에서 demo.ogg 검색");

            foreach (var folder in albumFolders)
            {
                string file = Path.Combine(folder, "demo.ogg");
                if (File.Exists(file))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 '{Path.GetFileName(folder)}'에서 demo.ogg 발견");
                    return file;
                }
            }

            return null;
        }
    }
}
