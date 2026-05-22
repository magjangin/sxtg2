using System;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Audio
{
    internal static class BgmFileResolver
    {
        private static readonly string[] AudioPatterns = { "*.ogg", "*.mp3", "*.wav" };

        public static string FindForAlbum(string albumFolder, bool allowRootFallback)
        {
            try
            {
                var albumFile = FindFirstAudioFile(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 앨범 폴더에서 BGM 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                if (!allowRootFallback)
                {
                    return null;
                }

                var gamePath = Path.GetDirectoryName(Application.dataPath);
                var hwaFolder = Path.Combine(gamePath, "hwa");
                return FindFirstAudioFile(hwaFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 파일 검색 실패: {ex.Message}");
                return null;
            }
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
