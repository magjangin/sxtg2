using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Manager
{
    public static partial class ManagerMusicSelectHook
    {
        private static readonly string[] PreviewAudioMemberNames =
        {
            "bgmSource", "bgm", "audioSource", "audio", "previewSource", "previewAudio"
        };

        private static readonly Dictionary<Type, MemberInfo> PreviewAudioMemberCache = new Dictionary<Type, MemberInfo>();

        private static AudioSource FindBgmSourceForPreviewStrong(object managerInstance)
        {
            Type managerType = managerInstance.GetType();

            if (PreviewAudioMemberCache.TryGetValue(managerType, out var cachedMember))
            {
                var cachedValue = ReadMemberValue(cachedMember, managerInstance);
                if (cachedValue is AudioSource cachedAudioSource && cachedAudioSource != null)
                {
                    return cachedAudioSource;
                }
            }

            foreach (var name in PreviewAudioMemberNames)
            {
                FieldInfo field = managerType.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var value = field.GetValue(managerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        PreviewAudioMemberCache[managerType] = field;
                        MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견 (필드): {name}");
                        return audioSource;
                    }
                }

                PropertyInfo prop = managerType.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                {
                    var value = prop.GetValue(managerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        PreviewAudioMemberCache[managerType] = prop;
                        MelonLogger.Msg($"[ManagerMusicSelectHook] BGM 소스 발견 (프로퍼티): {name}");
                        return audioSource;
                    }
                }
            }

            return FindFallbackPreviewAudioSource();
        }

        private static object ReadMemberValue(MemberInfo memberInfo, object instance)
        {
            if (memberInfo is FieldInfo fieldInfo)
            {
                return fieldInfo.GetValue(instance);
            }

            if (memberInfo is PropertyInfo propertyInfo && propertyInfo.CanRead)
            {
                return propertyInfo.GetValue(instance);
            }

            return null;
        }

        private static AudioSource FindFallbackPreviewAudioSource()
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            MelonLogger.Msg($"[ManagerMusicSelectHook] 전체 AudioSource 개수: {allAudioSources.Length}");

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && (audioSource.isPlaying || audioSource.clip != null))
                {
                    MelonLogger.Msg($"[ManagerMusicSelectHook] 재생 중인 AudioSource 발견: {audioSource.name}, clip={audioSource.clip?.name ?? "null"}");
                    return audioSource;
                }
            }

            if (allAudioSources.Length > 0 && allAudioSources[0] != null)
            {
                MelonLogger.Msg($"[ManagerMusicSelectHook] 첫 번째 AudioSource 사용: {allAudioSources[0].name}");
                return allAudioSources[0];
            }

            return null;
        }

        private static void StopAndMuteAllAudioSources(AudioSource primarySource)
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            int mutedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null && audioSource != primarySource)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                        mutedCount++;
                    }

                    audioSource.volume = 0f;
                }
            }

            if (primarySource != null)
            {
                if (primarySource.isPlaying)
                {
                    primarySource.Stop();
                }

                primarySource.volume = 0f;
            }

            MelonLogger.Msg($"[ManagerMusicSelectHook] {mutedCount + (primarySource != null ? 1 : 0)}개의 AudioSource 뮤트 완료");
        }

        private static void MuteAllAudioSources()
        {
            var allAudioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            int mutedCount = 0;

            foreach (var audioSource in allAudioSources)
            {
                if (audioSource != null)
                {
                    if (audioSource.isPlaying)
                    {
                        audioSource.Stop();
                    }

                    audioSource.volume = 0f;
                    mutedCount++;
                }
            }

            MelonLogger.Msg($"[ManagerMusicSelectHook] {mutedCount}개의 AudioSource 뮤트 완료 (전체 뮤트)");
        }
    }
}
