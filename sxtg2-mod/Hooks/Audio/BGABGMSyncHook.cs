using MelonLoader;
using UnityEngine;
using UnityEngine.Video;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Audio
{
    /// <summary>
    /// BGA와 BGM의 동기화를 담당하는 Hook 클래스입니다.
    /// </summary>
    public static class BGABGMSyncHook
    {
        private static float _lastSyncCheckTime = 0f;

        /// <summary>예전 API 호환. 실제 상세 로그는 MelonPreferences sxtg2 → LogLevel = Verbose 로 설정하세요.</summary>
        public static void SetDebugMode(bool enabled)
        {
            MelonLogger.Msg($"[BGABGMSyncHook] SetDebugMode({enabled}) 호출됨 — 상세 로그는 모드 설정의 LogLevel(Verbose)을 사용합니다.");
        }

        /// <summary>
        /// BGA와 BGM의 동기화를 체크하고 필요시 재동기화합니다.
        /// </summary>
        /// <summary>
        /// BGA와 BGM의 동기화를 체크하고 필요시 재동기화합니다.
        /// </summary>
        public static void CheckAndSync()
        {
            try
            {
                // BGA VideoPlayer 찾기
                var videoPlayer = BGAPlayerHook.GetCurrentVideoPlayer();
                if (videoPlayer == null || !videoPlayer.isPlaying)
                    return;

                // BGM AudioSource 찾기
                var bgmAudioSource = BGMPlayerHook.GetCurrentAudioSource();
                if (bgmAudioSource == null || !bgmAudioSource.isPlaying)
                {
                    // BGM이 멈췄으면 비디오 속도 정상화
                    if (videoPlayer.playbackSpeed != 1.0f)
                        videoPlayer.playbackSpeed = 1.0f;
                    return;
                }

                // 체크 주기 (하드 씽크가 아니므로 더 자주 체크해도 됨)
                if (Time.time - _lastSyncCheckTime < 0.1f)
                    return;
                _lastSyncCheckTime = Time.time;

                // BGA와 BGM의 재생 시간 비교
                var bgaTime = (float)videoPlayer.time;
                var bgmTime = bgmAudioSource.time;
                
                // 유효성 체크
                if (bgaTime <= 0 || bgmTime <= 0) return;

                var timeDifference = bgaTime - bgmTime; // 양수면 비디오가 빠름, 음수면 비디오가 느림
                var absDifference = Mathf.Abs(timeDifference);

                // 1. 큰 차이 (0.5초 이상) -> 하드 씽크 (Seek)
                if (absDifference > 0.5f)
                {
                    videoPlayer.time = bgmTime;
                    if (videoPlayer.canSetPlaybackSpeed) videoPlayer.playbackSpeed = 1.0f;
                    MelonLogger.Msg($"[BGABGMSyncHook] 하드 재동기화: BGA {bgaTime:F3} -> BGM {bgmTime:F3} (차이 {timeDifference:F3})");
                }
                // 2. 소프트 씽크 (Speed Adjust) - 히스테리시스 적용
                else
                {
                    bool isAdjusting = Mathf.Abs(videoPlayer.playbackSpeed - 1.0f) > 0.001f;
                    // 이미 조정 중이면 0.01초(10ms) 이내로 들어올 때까지 유지
                    // 조정 중이 아니면 0.05초(50ms) 이상 차이날 때 시작
                    float threshold = isAdjusting ? 0.01f : 0.05f;

                    if (absDifference > threshold)
                    {
                        if (videoPlayer.canSetPlaybackSpeed)
                        {
                            // 차이에 따라 속도 조절 폭 가변 (더 부드럽게)
                            // 0.1초 이상 차이나면 5% 조절, 0.1초 미만이면 2% 조절
                            float adjustmentFactor = (absDifference > 0.1f) ? 0.05f : 0.02f;
                            float targetSpeed = (timeDifference > 0) ? (1.0f - adjustmentFactor) : (1.0f + adjustmentFactor);
                        
                            if (Mathf.Abs(videoPlayer.playbackSpeed - targetSpeed) > 0.001f)
                            {
                                videoPlayer.playbackSpeed = targetSpeed;
                                if (ModLog.IsVerbose)
                                {
                                    MelonLogger.Msg($"[BGABGMSyncHook] 소프트 동기화: 속도 {targetSpeed:F3} (차이 {timeDifference:F4})");
                                }
                            }
                        }
                    }
                    else
                    {
                        // 안정권 진입 시 속도 복구
                        if (videoPlayer.canSetPlaybackSpeed && Mathf.Abs(videoPlayer.playbackSpeed - 1.0f) > 0.001f)
                        {
                            videoPlayer.playbackSpeed = 1.0f;
                            if (ModLog.IsVerbose) MelonLogger.Msg($"[BGABGMSyncHook] 동기화 안정: 속도 1.0 복귀");
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[BGABGMSyncHook] 동기화 체크 실패: {ex.Message}");
            }
        }
    }
}













