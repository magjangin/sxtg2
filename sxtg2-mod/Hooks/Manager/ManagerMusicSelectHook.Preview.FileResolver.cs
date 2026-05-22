using System;
using System.IO;
using MelonLoader;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static string FindMusicFile(string albumFolder)
        {
            if (string.IsNullOrEmpty(albumFolder) || !Directory.Exists(albumFolder))
            {
                return null;
            }

            var byName = TryFindMusicFileByKnownNames(albumFolder);
            if (byName != null)
            {
                return byName;
            }

            return TryFindMusicFileByAudioPrefixScan(albumFolder);
        }

        private static string TryFindMusicFileByKnownNames(string albumFolder)
        {
            string[] demoPatterns = { "demo.ogg", "demo.mp3", "demo.wav", "preview.ogg", "preview.mp3", "preview.wav" };
            foreach (var pattern in demoPatterns)
            {
                string file = Path.Combine(albumFolder, pattern);
                if (File.Exists(file))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] demo 파일 발견: {Path.GetFileName(file)}");
                    return file;
                }
            }

            string[] musicPatterns = { "music.ogg", "music.mp3", "music.wav" };
            foreach (var pattern in musicPatterns)
            {
                string file = Path.Combine(albumFolder, pattern);
                if (File.Exists(file))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] music 파일 발견: {Path.GetFileName(file)}");
                    return file;
                }
            }

            return null;
        }

        private static string TryFindMusicFileByAudioPrefixScan(string albumFolder)
        {
            try
            {
                var audioExtensions = new[] { "*.ogg", "*.mp3", "*.wav" };
                var demoOrPreview = TryFindFirstAudioMatchingPrefix(albumFolder, audioExtensions, new[] { "demo", "preview" }, "대체 demo");
                if (demoOrPreview != null)
                {
                    return demoOrPreview;
                }

                return TryFindFirstAudioMatchingPrefix(albumFolder, audioExtensions, new[] { "music" }, "대체 music");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 검색 중 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
                return null;
            }
        }

        private static string TryFindFirstAudioMatchingPrefix(string albumFolder, string[] audioExtensions, string[] namePrefixes, string logKind)
        {
            foreach (var ext in audioExtensions)
            {
                foreach (var file in Directory.EnumerateFiles(albumFolder, ext, SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(file);
                    foreach (var prefix in namePrefixes)
                    {
                        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            MelonLogger.Msg($"[ManagerMusicSelectHook] {logKind} 파일 발견: {Path.GetFileName(file)}");
                            return file;
                        }
                    }
                }
            }

            return null;
        }
    }
}
