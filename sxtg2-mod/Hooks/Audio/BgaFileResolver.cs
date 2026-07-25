using System;
using System.IO;
using MelonLoader;

namespace sxtg2.Hooks.Audio
{
    internal static class BgaFileResolver
    {
        public static string FindForAlbum(string albumFolder)
        {
            try
            {
                var albumFile = FindFirstMp4(albumFolder);
                if (!string.IsNullOrEmpty(albumFile))
                {
                    MelonLogger.Msg($"[BGAPlayerHook] 앨범 폴더에서 BGA 파일 발견: {Path.GetFileName(albumFile)}");
                    return albumFile;
                }

                return null;
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
