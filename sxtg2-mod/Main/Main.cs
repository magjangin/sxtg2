using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Hooks.Audio;
using sxtg2.Hooks.Note;

[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", "1.0.0", "화영왕")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)]

namespace sxtg2
{
    public class Main : MelonMod
    {
        public override void OnInitializeMelon()
        {
            try
            {
                ModLog.RegisterPreferences();
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                Directory.CreateDirectory(Path.Combine(gamePath, "hwa"));

                NoteSpriteHook.Initialize();
                MelonLogger.Msg("[Main] sxtg2 모드 초기화 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Main] 초기화 실패: {ex}");
            }
        }

        public override void OnUpdate()
        {
            BGABGMSyncHook.CheckAndSync();
        }

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("[Main] sxtg2 모드 종료됨");
        }
    }
}
