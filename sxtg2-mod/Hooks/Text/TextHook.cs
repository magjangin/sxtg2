using System;
using MelonLoader;
using UnityEngine.SceneManagement;

namespace sxtg2.Hooks.Text
{
    public static partial class TextHook
    {
        private static bool _isInitialized = false;
        private static bool _bmsInjected = false; // 중복 주입 방지

        /// <summary>
        /// 텍스트 설정 시 호출되는 Prefix 메서드
        /// </summary>
        private static bool TextSetterPrefix(ref object __instance, ref string __0)
        {
            try
            {
                // "커스텀 차트" 텍스트만 로그 출력 (디버깅용)
                if (__0 != null && __0.Contains("커스텀 차트"))
                {
                    string currentScene = SceneManager.GetActiveScene().name;
                    MelonLogger.Msg($"[TextHook] '커스텀 차트' 텍스트 감지! 씬: {currentScene}, 전체 텍스트: {__0}");
                }

                // 플레이 로딩 씬에서만 처리
                string currentScene2 = SceneManager.GetActiveScene().name;
                bool isPlayLoadingScene = currentScene2.Contains("Play") || 
                                         currentScene2.Contains("Loading");

                if (!isPlayLoadingScene)
                {
                    return true; // 다른 씬에서는 그대로 진행
                }

                // "커스텀 차트" 텍스트 감지
                if (__0 != null && __0.Contains("커스텀 차트"))
                {
                    MelonLogger.Msg($"[TextHook] '커스텀 차트' 텍스트 감지! 씬: {currentScene2}");
                    
                    // 중복 주입 방지
                    if (_bmsInjected)
                    {
                        MelonLogger.Msg("[TextHook] 이미 BMS가 주입되었습니다. 건너뜁니다.");
                        return true;
                    }

                    // Track ID 가져오기
                    string trackId = FindCurrentTrackId();
                    if (!string.IsNullOrEmpty(trackId))
                    {
                        MelonLogger.Msg($"[TextHook] 현재 Track ID: {trackId}");
                        LoadAndInjectBmsForTrack(trackId);
                        _bmsInjected = true; // 주입 완료 플래그 설정
                    }
                    else
                    {
                        MelonLogger.Warning("[TextHook] Track ID를 찾을 수 없습니다.");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextHook] 텍스트 설정 후킹 중 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }

            return true; // 원본 메서드 계속 실행
        }


        /// <summary>
        /// 씬 변경 시 주입 플래그를 리셋합니다.
        /// </summary>
        public static void ResetInjectionFlag()
        {
            _bmsInjected = false;
            MelonLogger.Msg("[TextHook] 주입 플래그 리셋");
        }
    }
}

















