using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        /// <summary>
        /// demo.ogg 파일을 로드하고 재생합니다.
        /// </summary>
        private static void LoadAndPlayDemo(object managerInstance, string trackId, string albumFolder = null)
        {
            AudioSource bgmSource = null;
            float originalVolume = 1f;
            
            try
            {
                bgmSource = FindBgmSource(managerInstance);
                if (bgmSource == null)
                {
                    return;
                }

                // 원래 볼륨 저장
                originalVolume = bgmSource.volume;

                // 기존 BGM 중지 및 뮤트
                if (bgmSource.isPlaying)
                {
                    bgmSource.Stop();
                }
                bgmSource.volume = 0f;

                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaRootFolder = Path.Combine(gamePath, "hwa");
                
                if (string.IsNullOrEmpty(albumFolder))
                {
                    albumFolder = hwaRootFolder;
                }

                if (!Directory.Exists(hwaRootFolder))
                {
                    // hwa 폴더가 없으면 원래 볼륨 복원
                    if (bgmSource != null)
                    {
                        bgmSource.volume = originalVolume;
                    }
                    return;
                }

                MelonLogger.Msg($"[ManagerMusicSelectHook] demo.ogg 검색 시작: Track ID={trackId}, 앨범 폴더={Path.GetFileName(albumFolder)}");

                // demo.ogg 파일 찾기
                string demoFile = FindDemoFile(trackId, albumFolder, hwaRootFolder);
                if (demoFile == null || !File.Exists(demoFile))
                {
                    MelonLogger.Msg("[ManagerMusicSelectHook] demo.ogg 파일을 찾을 수 없습니다. 원래 BGM 볼륨 복원");
                    // demo.ogg를 찾지 못했으면 원래 볼륨 복원
                    if (bgmSource != null)
                    {
                        bgmSource.volume = originalVolume;
                    }
                    return;
                }

                // 코루틴으로 비동기 로드
                var coroutineRunner = new GameObject("ManagerMusicSelectHook_DemoLoader");
                var runner = coroutineRunner.AddComponent<DemoLoaderCoroutineRunner>();
                runner.StartCoroutine(LoadAndPlayDemoCoroutine(bgmSource, demoFile, originalVolume));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] demo.ogg 로드 실패: {ex.Message}");
                // 예외 발생 시에도 원래 볼륨 복원
                if (bgmSource != null)
                {
                    bgmSource.volume = originalVolume;
                }
            }
        }

        private static IEnumerator LoadAndPlayDemoCoroutine(AudioSource audioSource, string demoFile, float originalVolume)
        {
            var fileUrl = "file://" + demoFile.Replace("\\", "/");
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.OGGVORBIS);
            yield return www.SendWebRequest();

            try
            {
                if (!TryGetDemoClip(www, out var audioClip))
                {
                    audioSource.volume = originalVolume;
                    yield break;
                }

                ApplyDemoClip(audioSource, audioClip, demoFile);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] demo.ogg 처리 중 오류: {ex.Message}");
                audioSource.volume = originalVolume;
            }
            finally
            {
                if (www != null)
                {
                    www.Dispose();
                }
            }
        }

        private static bool TryGetDemoClip(UnityWebRequest www, out AudioClip audioClip)
        {
            audioClip = null;

            if (www.result != UnityWebRequest.Result.Success)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] demo.ogg 로드 실패: {www.error}");
                return false;
            }

            var handler = www.downloadHandler as DownloadHandlerAudioClip;
            if (handler == null)
            {
                return false;
            }

            audioClip = handler.audioClip;
            return audioClip != null;
        }

        private static void ApplyDemoClip(AudioSource audioSource, AudioClip audioClip, string demoFile)
        {
            if (audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            audioSource.clip = audioClip;
            audioSource.loop = true;
            audioSource.volume = 1f;
            audioSource.Play();

            MelonLogger.Msg($"[ManagerMusicSelectHook] demo.ogg 재생 시작: {Path.GetFileName(demoFile)}");
        }

        // 코루틴 실행용 MonoBehaviour
        private class DemoLoaderCoroutineRunner : MonoBehaviour { }
    }
}
