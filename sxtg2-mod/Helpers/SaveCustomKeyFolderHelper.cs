using System;
using System.IO;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Helpers
{
    /// <summary>
    /// 커스텀 키 설정 저장용 폴더({게임 설치 폴더}\SaveCustomKey)를 생성합니다.
    /// </summary>
    public static class SaveCustomKeyFolderHelper
    {
        private const string FolderName = "SaveCustomKey";

        public static void EnsureFolderExists()
        {
            try
            {
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string folder = Path.Combine(gamePath, FolderName);

                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    MelonLogger.Msg($"[SaveCustomKeyFolderHelper] 폴더 생성: {folder}");
                }
                else
                {
                    MelonLogger.Msg($"[SaveCustomKeyFolderHelper] 폴더 이미 존재: {folder}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SaveCustomKeyFolderHelper] 폴더 생성 실패: {ex.Message}");
            }
        }
    }
}
