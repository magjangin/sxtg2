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
        private static IEnumerator LoadAndPlayMusicCoroutine(AudioSource audioSource, string musicFile, float originalVolume, int requestVersion)
        {
            var fileUrl = "file://" + musicFile.Replace("\\", "/");
            var audioType = MapMusicFileExtensionToAudioType(Path.GetExtension(musicFile));
            UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, audioType);
            yield return www.SendWebRequest();

            AudioClip loadedClip = null;
            bool loadOk = false;
            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 로드 실패: {www.error}");
                }
                else if (TryGetAudioClipFromMusicDownload(www, out loadedClip))
                {
                    loadOk = true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ManagerMusicSelectHook] 음악 파일 처리 중 오류: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
            }

            if (!loadOk)
            {
                www?.Dispose();
                audioSource.volume = originalVolume;
                yield break;
            }

            if (requestVersion != _previewRequestVersion)
            {
                www?.Dispose();
                yield break;
            }

            ApplyClipAndPlayPreviewMusic(audioSource, loadedClip, musicFile);
            www?.Dispose();
        }

        private static AudioType MapMusicFileExtensionToAudioType(string extension)
        {
            if (extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase))
                return AudioType.MPEG;
            if (extension.Equals(".wav", StringComparison.OrdinalIgnoreCase))
                return AudioType.WAV;
            return AudioType.OGGVORBIS;
        }

        private static bool TryGetAudioClipFromMusicDownload(UnityWebRequest www, out AudioClip audioClip)
        {
            audioClip = null;
            if (!(www.downloadHandler is DownloadHandlerAudioClip handler))
                return false;
            audioClip = handler.audioClip;
            return audioClip != null;
        }

        private static void ApplyClipAndPlayPreviewMusic(AudioSource audioSource, AudioClip audioClip, string musicFile)
        {
            if (audioSource.isPlaying)
                audioSource.Stop();

            audioSource.clip = audioClip;
            audioSource.loop = true;
            audioSource.volume = 1f;
            audioSource.Play();

            MelonLogger.Msg($"[ManagerMusicSelectHook] 음악 재생 시작: {Path.GetFileName(musicFile)}");
        }
    }
}
