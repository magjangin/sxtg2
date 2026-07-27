using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using sxtg2.Features;
using sxtg2.Helpers;
using sxtg2.Hooks;
using sxtg2.Hooks.Audio;

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
                SaveCustomKeyConfig.Initialize();
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                Directory.CreateDirectory(Path.Combine(gamePath, "hwa"));

                NoteSpriteHook.Initialize();

                try
                {
                    UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
                    UpdatePlaySceneState(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[Main] 씬 감지 이벤트 등록 실패: {ex.Message}");
                }

                MelonLogger.Msg("[Main] sxtg2 모드 초기화 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Main] 초기화 실패: {ex}");
            }
        }

        private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene prev, UnityEngine.SceneManagement.Scene next)
        {
            UpdatePlaySceneState(next.name);
        }

        private static void UpdatePlaySceneState(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            string s = sceneName.ToLowerInvariant();
            AutoPlayHook.IsPlayScene = s.Contains("play") || s.Contains("rhythm") || s.Contains("game");
            if (!AutoPlayHook.IsPlayScene)
            {
                AutoPlayHook.CurrentTimeSeconds = -1f;
            }
            KeyViewer.Reset();
            NoteSwayHook.Reset();
        }

        public override void OnUpdate()
        {
            BGABGMSyncHook.CheckAndSync();

            if (AutoPlayHook.IsPlayScene)
            {
                KeyViewer.Poll();
                JudgmentBar.RefreshJudgeRange();
            }
        }

        public override void OnGUI()
        {
            if (AutoPlayHook.IsPlayScene)
            {
                JudgmentBar.DrawJudgmentBar();
                KeyViewer.Draw();
            }
        }

        public override void OnApplicationQuit()
        {
            MelonLogger.Msg("[Main] sxtg2 모드 종료됨");
        }
    }
}
