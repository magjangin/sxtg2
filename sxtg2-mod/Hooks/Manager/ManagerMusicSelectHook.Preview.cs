using System;
using System.IO;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static MusicLoaderCoroutineRunner _musicLoaderRunner;
        private static int _previewRequestVersion;
        private static string _lastPreviewTrackId;
        private static string _lastPreviewMusicFile;
        private static AudioSource _lastPreviewAudioSource;

        /// <summary>
        /// PlayPreview 메서드 prefix 후킹.
        /// 커스텀 트랙인 경우 원래 preview 재생을 막고 커스텀 음악을 재생합니다.
        /// </summary>
        private static bool PlayPreviewPrefix(object __instance, object t)
        {
            try
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] PlayPreview 호출 감지!");
                
                // TrackData가 null이면 원래 동작 수행
                if (t == null)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] PlayPreview: TrackData가 null입니다.");
                    return true;
                }

                ManagerMusicSelectBridge.ReadTrackIdentity(t, out string trackId, out string displayName);

                MelonLogger.Msg($"[ManagerMusicSelectHook] PlayPreview: Track ID={trackId}, DisplayName={displayName}");

                // 커스텀 트랙인지 확인
                bool isCustomTrack = CustomTrackHelper.IsCustomTrack(t);
                if (isCustomTrack)
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 커스텀 트랙의 원래 preview 재생 차단 - 커스텀 음악 재생 시도");
                    
                    // BGM 뮤트하고 demo/music.ogg 재생
                    PlayCustomMusic(__instance, trackId, displayName);
                    
                    return false; // 원래 메서드 실행 방지
                }

                // 일반 트랙이면 원래 동작 수행
                CustomTrackHelper.ClearSelectedTrack();
                MelonLogger.Msg("[ManagerMusicSelectHook] 일반 트랙 - 원래 preview 재생 허용");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] PlayPreviewPrefix 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
                // 오류 발생 시 원래 동작 수행
                return true;
            }
        }

        /// <summary>
        /// 커스텀 트랙의 demo/music 파일을 재생합니다 (강화된 로직).
        /// demo.ogg가 있으면 우선 재생, 없으면 music.ogg를 재생합니다.
        /// </summary>
        private static void PlayCustomMusic(object managerInstance, string trackId, string displayName)
        {
            AudioSource bgmSource = null;
            float originalVolume = 1f;
            
            try
            {
                MelonLogger.Msg("[ManagerMusicSelectHook] === PlayCustomMusic 시작 ===");
                
                // 강화된 BGM 소스 찾기
                bgmSource = FindBgmSourceForPreviewStrong(managerInstance);
                if (bgmSource == null)
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] BGM 소스를 찾을 수 없습니다. 모든 AudioSource 뮤트 시도");
                    // 모든 AudioSource를 찾아서 뮤트
                    MuteAllAudioSources();
                    bgmSource = UnityEngine.Object.FindObjectOfType<AudioSource>();
                    if (bgmSource == null)
                    {
                        MelonLogger.Error("[ManagerMusicSelectHook] AudioSource를 전혀 찾을 수 없습니다!");
                        return;
                    }
                }

                // 원래 볼륨 저장
                originalVolume = bgmSource.volume;
                MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견: volume={originalVolume}, isPlaying={bgmSource.isPlaying}, clip={bgmSource.clip?.name ?? "null"}");

                // 앨범 폴더 찾기
                string albumFolder = FindAlbumFolderByDisplayName(displayName, trackId);
                if (!Directory.Exists(albumFolder))
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 앨범 폴더가 존재하지 않습니다: {albumFolder}");
                    bgmSource.volume = originalVolume;
                    return;
                }
                MelonLogger.Msg($"[ManagerMusicSelectHook] 앨범 폴더 확인: {Path.GetFileName(albumFolder)}");

                // demo/music 파일 찾기 (demo 우선)
                string musicFile = FindMusicFile(albumFolder);
                if (musicFile == null)
                {
                    MelonLogger.Warning("[ManagerMusicSelectHook] 음악 파일을 찾을 수 없습니다. 원래 BGM 볼륨 복원");
                    bgmSource.volume = originalVolume;
                    return;
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] 음악 파일 발견: {Path.GetFileName(musicFile)} ({new FileInfo(musicFile).Length / 1024}KB)");

                // 동일한 트랙/파일이 이미 재생 중이면 불필요한 재로드를 건너뜀
                if (bgmSource == _lastPreviewAudioSource &&
                    bgmSource.isPlaying &&
                    string.Equals(_lastPreviewTrackId, trackId, StringComparison.Ordinal) &&
                    string.Equals(_lastPreviewMusicFile, musicFile, StringComparison.OrdinalIgnoreCase))
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] 동일한 preview 요청 감지 - 재로드 생략");
                    return;
                }

                // 모든 재생 중인 AudioSource 중지 및 뮤트
                StopAndMuteAllAudioSources(bgmSource);
                MelonLogger.Msg("[ManagerMusicSelectHook] 모든 BGM 뮤트 완료");

                // 코루틴으로 비동기 로드
                var runner = GetOrCreateMusicLoaderRunner();
                int requestVersion = ++_previewRequestVersion;
                _lastPreviewTrackId = trackId;
                _lastPreviewMusicFile = musicFile;
                _lastPreviewAudioSource = bgmSource;
                runner.StartCoroutine(LoadAndPlayMusicCoroutine(bgmSource, musicFile, originalVolume, requestVersion));
                
                MelonLogger.Msg("[ManagerMusicSelectHook] === PlayCustomMusic 코루틴 시작 ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[ManagerMusicSelectHook] PlayCustomMusic 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                // 예외 발생 시 원래 볼륨 복원
                if (bgmSource != null)
                {
                    bgmSource.volume = originalVolume;
                }
            }
        }

        private static MusicLoaderCoroutineRunner GetOrCreateMusicLoaderRunner()
        {
            if (_musicLoaderRunner != null)
                return _musicLoaderRunner;

            var coroutineRunner = new GameObject("ManagerMusicSelectHook_MusicLoader");
            UnityEngine.Object.DontDestroyOnLoad(coroutineRunner);
            _musicLoaderRunner = coroutineRunner.AddComponent<MusicLoaderCoroutineRunner>();
            return _musicLoaderRunner;
        }

        // 코루틴 실행용 MonoBehaviour
        private class MusicLoaderCoroutineRunner : MonoBehaviour { }
    }
}








































