using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using MelonLoader;

namespace sxtg2.Hooks.Audio
{
    public static partial class BGMPlayerHook
    {
        private static IEnumerator LoadAndReplaceBGM(AudioSource audioSource)
        {
            if (!TryBuildBgmWebRequest(out UnityWebRequest www, out string buildError))
            {
                MelonLogger.Warning($"[BGMPlayerHook] {buildError}");
                _isLoading = false;
                yield break;
            }

            yield return www.SendWebRequest();

            try
            {
                ApplyBgmWebRequestResult(www, audioSource);
            }
            finally
            {
                www?.Dispose();
            }
        }

        private static bool TryBuildBgmWebRequest(out UnityWebRequest www, out string errorMessage)
        {
            www = null;
            errorMessage = null;

            var fileUrl = "file://" + _bgmFilePath.Replace("\\", "/");
            var extension = Path.GetExtension(_bgmFilePath).ToLower();
            var audioType = MapExtensionToAudioType(extension);
            bool useStreaming = ShouldUseStreamingForBgm(extension);

            try
            {
                www = UnityWebRequestMultimedia.GetAudioClip(fileUrl, audioType);
                var handler = new DownloadHandlerAudioClip(fileUrl, audioType);
                handler.streamAudio = useStreaming;
                www.downloadHandler = handler;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"BGM 요청 생성 실패: {ex.Message}";
                return false;
            }
        }

        private static AudioType MapExtensionToAudioType(string extension)
        {
            if (extension == ".ogg") return AudioType.OGGVORBIS;
            if (extension == ".mp3") return AudioType.MPEG;
            if (extension == ".wav") return AudioType.WAV;
            return AudioType.UNKNOWN;
        }

        private static bool ShouldUseStreamingForBgm(string extension)
        {
            if (extension == ".wav")
                return true;

            try
            {
                var fileInfo = new FileInfo(_bgmFilePath);
                if (fileInfo.Length > 5 * 1024 * 1024)
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 대용량 파일 감지({fileInfo.Length / 1024 / 1024}MB): 스트리밍 모드 사용");
                    return true;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[BGMPlayerHook] 파일 크기 확인 실패(스트리밍 기본값 사용): {ex.Message}");
            }

            return false;
        }

        private static void ApplyBgmWebRequestResult(UnityWebRequest www, AudioSource audioSource)
        {
            try
            {
                if (www.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[BGMPlayerHook] BGM 로드 실패: {www.error}");
                    _isLoading = false;
                    return;
                }

                var handler = www.downloadHandler as DownloadHandlerAudioClip;
                if (handler == null)
                {
                    MelonLogger.Warning("[BGMPlayerHook] DownloadHandler가 올바른 타입이 아닙니다.");
                    return;
                }

                var audioClip = handler.audioClip;
                if (audioClip == null)
                {
                    MelonLogger.Warning("[BGMPlayerHook] AudioClip이 null입니다.");
                    return;
                }

                if (audioSource.isPlaying)
                    audioSource.Stop();

                audioSource.clip = audioClip;
                audioSource.loop = true;
                audioSource.Play();

                _isReplaced = true;
                _isLoading = false;
                MelonLogger.Msg($"[BGMPlayerHook] BGM 교체 완료: {Path.GetFileName(_bgmFilePath)}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] BGM 처리 중 오류: {ex.Message}");
                _isLoading = false;
            }
        }
    }
}
