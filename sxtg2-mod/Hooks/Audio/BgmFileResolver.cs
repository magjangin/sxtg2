using System;
using System.IO;
using MelonLoader;

namespace sxtg2.Hooks.Audio
{
    internal static class BgmFileResolver
    {
        private static readonly string[] AudioPatterns = { "*.ogg", "*.mp3", "*.wav" };
        private static readonly string[] AudioExtensions = { ".ogg", ".mp3", ".wav" };

        public static string FindForAlbum(string albumFolder)
        {
            try
            {
                var albumFile = FindNamedAudioFile(albumFolder, "music")
                    ?? FindFirstAudioFile(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 앨범 폴더에서 BGM 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 파일 검색 실패: {ex.Message}");
                return null;
            }
        }

        public static string FindPreviewForAlbum(string albumFolder)
        {
            return FindNamedAudioFile(albumFolder, "demo")
                ?? FindNamedAudioFile(albumFolder, "music")
                ?? FindFirstAudioFile(albumFolder);
        }

        private static string FindNamedAudioFile(string folder, string baseName)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return null;

            foreach (string extension in AudioExtensions)
            {
                string path = Path.Combine(folder, baseName + extension);
                if (File.Exists(path))
                    return path;
            }

            return null;
        }

        private static string FindFirstAudioFile(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            foreach (var pattern in AudioPatterns)
            {
                var files = Directory.GetFiles(folder, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0)
                {
                    return files[0];
                }
            }

            return null;
        }
    }
}
