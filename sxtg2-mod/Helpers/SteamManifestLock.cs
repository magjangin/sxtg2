using System;
using System.IO;
using Microsoft.Win32;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Helpers
{
    /// <summary>
    /// Steam appmanifest(.acf) 읽기 전용 해제. (과거 버전에서 잠근 속성 복구)
    /// </summary>
    public static class SteamManifestLock
    {
        private const string AppId = "1802720";
        private const string ManifestFileName = "appmanifest_" + AppId + ".acf";

        public static void Unlock()
        {
            try
            {
                string steamappsPath = null;

                MelonLogger.Msg("[SteamManifestLock] 레지스트리에서 Steam 경로를 찾는 중...");
                steamappsPath = FindSteamPathFromRegistry();

                if (string.IsNullOrEmpty(steamappsPath))
                {
                    MelonLogger.Msg("[SteamManifestLock] 레지스트리에서 Steam 경로를 찾지 못했습니다. 게임 경로에서 역추적합니다...");
                    steamappsPath = FindSteamPathFromGamePath();
                }

                if (!string.IsNullOrEmpty(steamappsPath))
                {
                    string manifestPath = Path.Combine(steamappsPath, ManifestFileName);
                    UnlockManifestFile(manifestPath);
                }
                else
                {
                    MelonLogger.Warning("[SteamManifestLock] Steam 경로를 찾을 수 없습니다. 매니페스트를 해제할 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SteamManifestLock] 매니페스트 해제 중 오류 발생: {ex.Message}");
            }
        }

        private static string FindSteamPathFromRegistry()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object installPath = key.GetValue("InstallPath");
                        if (installPath != null && !string.IsNullOrEmpty(installPath.ToString()))
                        {
                            string steamPath = installPath.ToString();
                            string steamappsPath = Path.Combine(steamPath, "steamapps");
                            if (Directory.Exists(steamappsPath))
                            {
                                MelonLogger.Msg($"[SteamManifestLock] 레지스트리(64비트)에서 Steam 경로 발견: {steamappsPath}");
                                return steamappsPath;
                            }
                        }
                    }
                }

                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        object installPath = key.GetValue("InstallPath");
                        if (installPath != null && !string.IsNullOrEmpty(installPath.ToString()))
                        {
                            string steamPath = installPath.ToString();
                            string steamappsPath = Path.Combine(steamPath, "steamapps");
                            if (Directory.Exists(steamappsPath))
                            {
                                MelonLogger.Msg($"[SteamManifestLock] 레지스트리(32비트)에서 Steam 경로 발견: {steamappsPath}");
                                return steamappsPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SteamManifestLock] 레지스트리 읽기 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// steamapps\common\… 게임 루트 또는 그 외 경로에서 steamapps 폴더를 찾습니다.
        /// </summary>
        private static string FindSteamPathFromGamePath()
        {
            try
            {
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(gamePath))
                {
                    MelonLogger.Warning("[SteamManifestLock] 게임 경로를 가져올 수 없습니다.");
                    return null;
                }

                DirectoryInfo commonDir = Directory.GetParent(gamePath);
                if (commonDir != null && commonDir.Name == "common")
                {
                    DirectoryInfo steamappsDir = commonDir.Parent;
                    if (steamappsDir != null && Directory.Exists(steamappsDir.FullName))
                    {
                        MelonLogger.Msg($"[SteamManifestLock] steamapps\\common 역추적: {steamappsDir.FullName}");
                        return steamappsDir.FullName;
                    }
                }

                for (DirectoryInfo dir = new DirectoryInfo(gamePath); dir != null; dir = dir.Parent)
                {
                    if (dir.Name == "steamapps" && Directory.Exists(dir.FullName))
                    {
                        MelonLogger.Msg($"[SteamManifestLock] 상위 디렉터리에서 steamapps 발견: {dir.FullName}");
                        return dir.FullName;
                    }

                    string nestedSteamapps = Path.Combine(dir.FullName, "steamapps");
                    if (Directory.Exists(nestedSteamapps))
                    {
                        MelonLogger.Msg($"[SteamManifestLock] 하위 steamapps 발견: {nestedSteamapps}");
                        return nestedSteamapps;
                    }
                }

                MelonLogger.Warning("[SteamManifestLock] 게임 경로에서 steamapps를 찾지 못했습니다.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SteamManifestLock] 게임 경로 역추적 실패: {ex.Message}");
            }

            return null;
        }

        private static void UnlockManifestFile(string manifestPath)
        {
            try
            {
                if (!File.Exists(manifestPath))
                {
                    MelonLogger.Warning($"[SteamManifestLock] 매니페스트 파일이 없습니다: {manifestPath}");
                    return;
                }

                FileAttributes attributes = File.GetAttributes(manifestPath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(manifestPath, attributes & ~FileAttributes.ReadOnly);
                    MelonLogger.Msg($"[SteamManifestLock] 매니페스트 읽기 전용 해제: {manifestPath}");
                }
                else
                {
                    MelonLogger.Msg($"[SteamManifestLock] 매니페스트가 이미 쓰기 가능입니다: {manifestPath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SteamManifestLock] 매니페스트 해제 실패: {ex.Message}");
            }
        }
    }
}
