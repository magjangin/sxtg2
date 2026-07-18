using System;
using MelonLoader;
using sxtg2.Features;
using sxtg2.Helpers;
using sxtg2.Hooks.Audio;
using sxtg2.Hooks.Manager;
using sxtg2.Hooks.SXGT;
using sxtg2.Hooks.Text;

namespace sxtg2
{
    public partial class Main
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("========================================");
            MelonLogger.Msg("[Main] OnInitializeMelon() 호출됨!");
            MelonLogger.Msg("========================================");
            MelonLogger.Msg("=== sxtg2 모드 초기화 시작 ===");

            try
            {
                ModLog.RegisterPreferences();

                // TextHook 초기화
                MelonLogger.Msg("[Main] TextHook 초기화 시작...");
                TextHook.Initialize();
                MelonLogger.Msg("[Main] TextHook 초기화 완료");

                // 예전 버전에서 잠갔던 appmanifest .acf 읽기 전용 해제
                MelonLogger.Msg("[Main] Steam 매니페스트 읽기 전용 해제 시도...");
                SteamManifestLock.Unlock();
                MelonLogger.Msg("[Main] Steam 매니페스트 처리 완료");

                // BGAPlayerHook 초기화
                BGAPlayerHook.Initialize();

                // BGMPlayerHook 초기화
                BGMPlayerHook.Initialize();

                // HighscoreMeterHook 초기화
                MelonLogger.Msg("[Main] HighscoreMeterHook 초기화 시작...");
                HighscoreMeterHook.Initialize();
                MelonLogger.Msg("[Main] HighscoreMeterHook 초기화 완료");

                // ManagerMusicSelectHook 초기화
                MelonLogger.Msg("[Main] ManagerMusicSelectHook 초기화 시작...");
                ManagerMusicSelectHook.Initialize();
                MelonLogger.Msg("[Main] ManagerMusicSelectHook 초기화 완료");

                // ManagerPlayHook 초기화
                MelonLogger.Msg("[Main] ManagerPlayHook 초기화 시작...");
                ManagerPlayHook.Initialize();
                MelonLogger.Msg("[Main] ManagerPlayHook 초기화 완료");

                // SXGTDataHook 초기화
                MelonLogger.Msg("[Main] SXGTDataHook 초기화 시작...");
                SXGTDataHook.Initialize();
                MelonLogger.Msg("[Main] SXGTDataHook 초기화 완료");

                // BMS 파일 스캔 및 파싱
                MelonLogger.Msg("[Main] BMS 파일 스캔 및 파싱 시작...");
                ScanAndParseBmsFiles();
                MelonLogger.Msg("[Main] BMS 파일 스캔 및 파싱 완료");

                // SceneDetector 초기화
                MelonLogger.Msg("[Main] SceneDetector 초기화 시작...");
                _sceneDetector = new SceneDetector();
                _sceneDetector.Initialize();
                MelonLogger.Msg("[Main] SceneDetector 초기화 완료");

                MelonLogger.Msg("=== sxtg2 모드 초기화 완료 ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[Main] 초기화 실패: {ex.Message}");
                MelonLogger.Error($"[Main] 스택 트레이스: {ex.StackTrace}");
            }
        }
    }
}
