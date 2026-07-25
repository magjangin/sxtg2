using System;
using System.IO;
using MelonLoader;
using UnityEngine.Video;

namespace sxtg2.Hooks.Audio
{
    public static class BGAPlayerHook
    {
        private static bool _isReplaced = false;
        private static string _bgaFilePath = null;
        private static VideoPlayer _currentVideoPlayer = null;

        public static void ResetReplacementFlag()
        {
            _isReplaced = false;
            _currentVideoPlayer = null;
        }

        /// <summary>
        /// 현재 VideoPlayer 인스턴스를 반환합니다.
        /// </summary>
        public static VideoPlayer GetCurrentVideoPlayer()
        {
            return _currentVideoPlayer;
        }

        public static bool ReplacePlaySceneBGA(
            VideoPlayer videoPlayer,
            string customAlbumFolder)
        {
            if (_isReplaced)
            {
                return true;
            }

            try
            {
                if (string.IsNullOrEmpty(customAlbumFolder))
                {
                    MelonLogger.Msg("[BGAPlayerHook] 커스텀 앨범 폴더가 없어 BGA 교체를 건너뜁니다.");
                    return false;
                }

                MelonLogger.Msg($"[BGAPlayerHook] ReplacePlaySceneBGA 호출: albumFolder={customAlbumFolder}");

                string bgaFile = BgaFileResolver.FindForAlbum(customAlbumFolder);

                if (string.IsNullOrEmpty(bgaFile))
                {
                    return false;
                }

                if (videoPlayer == null)
                {
                    MelonLogger.Warning("[BGAPlayerHook] 대상 VideoPlayer가 없습니다.");
                    return false;
                }

                return LoadRegularBGA(videoPlayer, bgaFile);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] BGA 교체 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 일반 BGA 파일을 로드합니다.
        /// </summary>
        /// <param name="videoPlayer">VideoPlayer 인스턴스</param>
        /// <param name="bgaFilePath">BGA 파일 경로</param>
        private static bool LoadRegularBGA(VideoPlayer videoPlayer, string bgaFilePath)
        {
            try
            {
                // VideoPlayer가 존재하면 강력하게 교체 시도 (준비 여부와 관계없이)
                var videoUrl = "file://" + bgaFilePath.Replace("\\", "/");
                
                // 기존 재생 중지
                if (videoPlayer.isPlaying)
                {
                    videoPlayer.Stop();
                }
                
                videoPlayer.url = videoUrl;
                videoPlayer.source = VideoSource.Url;
                videoPlayer.clip = null; // 스트리밍 사용 시 clip은 null
                
                // 최적화 설정
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None; // 비디오 오디오 끄기 (BGM 충돌 방지)
                videoPlayer.skipOnDrop = true; // 프레임 드랍 허용 (싱크 유지 위해)
                
                // 강제로 Prepare 시도
                try
                {
                    videoPlayer.Prepare();
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BGAPlayerHook] VideoPlayer.Prepare 실패 (계속 진행): {ex.Message}");
                }

                _currentVideoPlayer = videoPlayer;
                _bgaFilePath = bgaFilePath; // 업데이트
                _isReplaced = true;
                MelonLogger.Msg($"[BGAPlayerHook] BGA 교체 완료: {Path.GetFileName(_bgaFilePath)}");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BGAPlayerHook] 일반 BGA 로드 실패: {ex.Message}");
                return false;
            }
        }

    }
}
