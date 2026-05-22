using System;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Audio
{
    internal static class BgaFileResolver
    {
        public static string FindForAlbum(string albumFolder, bool allowRootFallback)
        {
            try
            {
                var albumFile = FindFirstMp4(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGAPlayerHook] 앨범 폴더에서 BGA 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                if (!allowRootFallback)
                {
                    return null;
                }

                var gamePath = Path.GetDirectoryName(Application.dataPath);
                var hwaFolder = Path.Combine(gamePath, "hwa");
                return FindFirstMp4(hwaFolder);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] BGA 파일 검색 실패: {ex.Message}");
                return null;
            }
        }

        private static string FindFirstMp4(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return null;
            }

            var files = Directory.GetFiles(folder, "*.mp4", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0] : null;
        }
    }
}
