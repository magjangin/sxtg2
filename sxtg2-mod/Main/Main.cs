using System;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;
using sxtg2.Features;
using sxtg2.Helpers;
using sxtg2.Hooks;
using sxtg2.Hooks.Audio;

[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", sxtg2.ModInfo.Version, "화영왕")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)]

namespace sxtg2
{
    public class Main : MelonMod
    {
        /// <summary>
        /// 설정(config.txt)을 다시 읽을 씬인지. 이름에 play/rhythm/game이 들어가면 참이라 로딩 씬(PlayLoading)도 포함한다.
        /// 로딩 씬에서 미리 읽어 두면 Play 씬의 Awake/Start가 항상 최신 값을 본다.
        /// </summary>
        public static bool IsPlayScene { get; private set; }

        /// <summary>판정바/키뷰어 같은 오버레이를 그릴 씬인지. 실제 플레이 씬(`Play`)만 참이고 로딩 씬은 제외한다.</summary>
        public static bool IsGameplayScene { get; private set; }

        public override void OnInitializeMelon()
        {
            try
            {
                ModLog.RegisterPreferences();
                // JudgeScoreMaxHook.Prepare가 패치 시점에 이미 읽었을 수 있다. 다시 읽지 않고 안 읽었을 때만 읽는다.
                SaveCustomKeyConfig.EnsureInitialized();
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                Directory.CreateDirectory(Path.Combine(gamePath, "hwa"));

                // 씬 감지부터 등록한다. 아래 노트 스킨 같은 선택 기능의 초기화가 실패해도
                // 오버레이와 설정 재로드는 계속 동작해야 한다.
                try
                {
                    UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
                    // 방금 읽었으므로 여기서는 다시 읽지 않는다.
                    UpdatePlaySceneState(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, allowConfigReload: false);
                }
                catch (Exception ex)
                {
                    ModLog.Warning($"[Main] 씬 감지 이벤트 등록 실패: {ex.Message}");
                }

                try
                {
                    NoteSpriteHook.Initialize();
                }
                catch (Exception ex)
                {
                    ModLog.Warning($"[Main] 노트 스킨 초기화 실패(스킨 없이 계속합니다): {ex.Message}");
                }

                LogGameVersion();
                ModLog.Msg("[Main] sxtg2 모드 초기화 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Main] 초기화 실패: {ex}");
            }
        }

        /// <summary>
        /// 게임 버전, Assembly-CSharp.dll의 SHA-256, Harmony로 패치된 메서드 수를 로그로 남긴다. 게임이 업데이트되어
        /// 훅이 어긋났을 때 로그만 보고 원인을 좁히려는 것이다(알려진 문제: 게임 버전 가드 없음). 실패해도 초기화는 계속한다.
        /// </summary>
        private static void LogGameVersion()
        {
            try
            {
                string assemblyPath = Path.Combine(Application.dataPath, "Managed", "Assembly-CSharp.dll");
                string hash = File.Exists(assemblyPath) ? ComputeSha256(assemblyPath) : "(파일 없음)";
                int patchedMethods = HarmonyLib.Harmony.GetAllPatchedMethods().Count();
                ModLog.Msg($"[Main] 게임 버전 {Application.version}, Assembly-CSharp SHA-256 {hash}, Harmony 패치 메서드 {patchedMethods}개");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[Main] 게임 버전 정보를 읽지 못했습니다: {ex.Message}");
            }
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }

        private static void OnActiveSceneChanged(UnityEngine.SceneManagement.Scene prev, UnityEngine.SceneManagement.Scene next)
        {
            UpdatePlaySceneState(next.name);
        }

        private static void UpdatePlaySceneState(string sceneName, bool allowConfigReload = true)
        {
            if (string.IsNullOrEmpty(sceneName)) return;
            string s = sceneName.ToLowerInvariant();
            IsPlayScene = s.Contains("play") || s.Contains("rhythm") || s.Contains("game");
            IsGameplayScene = string.Equals(sceneName, "Play", StringComparison.OrdinalIgnoreCase);

            // 플레이 씬으로 넘어가는 이 순간이 "다음 플레이"의 시작점이다. 여기서 설정 파일을 다시 읽으면
            // 게임을 재시작하지 않아도 수정이 반영되면서, 한 판이 도는 동안에는 값이 절대 바뀌지 않는다.
            // 전환마다 무조건 읽는 이유: 리트라이는 ManagerPlay.RestartGame()이 Play 씬을 다시 로드하는데
            // 씬 이름이 그대로라, 이전 상태와 비교하는(rising edge) 방식으로는 리트라이를 놓친다.
            // activeSceneChanged는 플레이 도중에는 발생하지 않으므로 이걸로 충분하다.
            if (allowConfigReload && IsPlayScene)
            {
                SaveCustomKeyConfig.Reload($"플레이 씬 진입: {sceneName}");
            }

            KeyViewer.Reset();
            NoteSwayHook.Reset();
            NoteSpeedChaosHook.Reset();
        }

        public override void OnUpdate()
        {
            BGABGMSyncHook.CheckAndSync();

            if (IsGameplayScene)
            {
                KeyViewer.Poll();
                JudgmentBar.RefreshJudgeRange();
            }
        }

        public override void OnGUI()
        {
            if (IsGameplayScene)
            {
                JudgmentBar.DrawJudgmentBar();
                KeyViewer.Draw();
            }
        }

        public override void OnApplicationQuit()
        {
            ModLog.Msg("[Main] sxtg2 모드 종료됨");
        }
    }
}
