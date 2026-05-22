using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Audio
{
    internal static class BgmAudioSourceFinder
    {
        public static AudioSource Find(AudioSource preferredAudioSource)
        {
            if (preferredAudioSource != null)
            {
                MelonLogger.Msg("[BGMPlayerHook] ManagerPlay의 bgm 필드 사용");
                return preferredAudioSource;
            }

            var fromAudioSources = FindFromAudioSources();
            if (fromAudioSources != null)
            {
                return fromAudioSources;
            }

            return FindFromGameObjects();
        }

        private static AudioSource FindFromAudioSources()
        {
            var allAudioSources = Object.FindObjectsOfType<AudioSource>();
            MelonLogger.Msg($"[BGMPlayerHook] AudioSource 검색 중... ({allAudioSources.Length}개 발견)");

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && audioSource.isPlaying)
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 재생 중인 AudioSource 발견: {audioSource.name}");
                    return audioSource;
                }
            }

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && audioSource.clip != null)
                {
                    MelonLogger.Msg($"[BGMPlayerHook] 클립이 있는 AudioSource 발견: {audioSource.name}");
                    return audioSource;
                }
            }

            if (allAudioSources.Length > 0)
            {
                MelonLogger.Msg($"[BGMPlayerHook] 첫 번째 AudioSource 사용: {allAudioSources[0].name}");
                return allAudioSources[0];
            }

            return null;
        }

        private static AudioSource FindFromGameObjects()
        {
            var allGameObjects = Object.FindObjectsOfType<GameObject>();
            foreach (var go in allGameObjects)
            {
                if (go == null)
                {
                    continue;
                }

                var audioSource = go.GetComponent<AudioSource>();
                if (audioSource != null)
                {
                    MelonLogger.Msg($"[BGMPlayerHook] GameObject에서 AudioSource 발견: {go.name}");
                    return audioSource;
                }
            }

            return null;
        }
    }
}
