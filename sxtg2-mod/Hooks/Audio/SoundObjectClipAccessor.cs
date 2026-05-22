using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;

namespace sxtg2.Hooks.Audio
{
    internal static class SoundObjectClipAccessor
    {
        private static readonly BindingFlags InstanceMemberFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static bool TryReadClipName(object soundObjectInstance, out string clipName)
        {
            clipName = null;

            if (soundObjectInstance == null)
            {
                return false;
            }

            var soundObjectType = soundObjectInstance.GetType();
            var audioSource = GetAudioSource(soundObjectInstance, soundObjectType);
            if (audioSource != null && audioSource.clip != null)
            {
                clipName = audioSource.clip.name;
                ModLog.Verbose($"[HighscoreMeterHook] audioSource.clip에서 클립 이름 확인: {clipName}");
                return !string.IsNullOrEmpty(clipName);
            }

            var clipNameField = soundObjectType.GetField("clipName", InstanceMemberFlags);
            if (clipNameField != null)
            {
                clipName = clipNameField.GetValue(soundObjectInstance) as string;
                if (!string.IsNullOrEmpty(clipName))
                {
                    ModLog.Verbose($"[HighscoreMeterHook] clipName 필드에서 클립 이름 확인: {clipName}");
                    return true;
                }
            }

            return TryReadClearClipNameFromStringFields(soundObjectInstance, soundObjectType, out clipName);
        }

        public static bool TryApplyClip(object soundObjectInstance, AudioClip clip, string previousClipName)
        {
            if (soundObjectInstance == null || clip == null)
            {
                return false;
            }

            var soundObjectType = soundObjectInstance.GetType();
            bool applied = false;

            var audioSource = GetAudioSource(soundObjectInstance, soundObjectType);
            if (audioSource != null)
            {
                audioSource.clip = clip;
                applied = true;
                ModLog.Verbose($"[HighscoreMeterHook] audioSource.clip 교체 완료: {previousClipName} -> {clip.name}");
            }

            var clipNameField = soundObjectType.GetField("clipName", InstanceMemberFlags);
            if (clipNameField != null)
            {
                clipNameField.SetValue(soundObjectInstance, clip.name);
                applied = true;
                ModLog.Verbose($"[HighscoreMeterHook] clipName 필드 교체 완료: {previousClipName} -> {clip.name}");
            }

            return applied;
        }

        private static AudioSource GetAudioSource(object soundObjectInstance, Type soundObjectType)
        {
            var audioSourceField = soundObjectType.GetField("audioSource", InstanceMemberFlags);
            if (audioSourceField == null)
            {
                return null;
            }

            var audioSource = audioSourceField.GetValue(soundObjectInstance) as AudioSource;
            if (audioSource != null)
            {
                var mb = soundObjectInstance as MonoBehaviour;
                ModLog.Verbose($"[HighscoreMeterHook] audioSource 발견: {(mb != null ? mb.name : "null")}");
            }

            return audioSource;
        }

        private static bool TryReadClearClipNameFromStringFields(object soundObjectInstance, Type soundObjectType, out string clipName)
        {
            clipName = null;
            var fields = soundObjectType.GetFields(InstanceMemberFlags);

            foreach (var field in fields)
            {
                try
                {
                    if (field.FieldType != typeof(string))
                    {
                        continue;
                    }

                    var stringValue = field.GetValue(soundObjectInstance) as string;
                    if (!string.IsNullOrEmpty(stringValue) &&
                        stringValue.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
                    {
                        clipName = stringValue;
                        ModLog.Verbose($"[HighscoreMeterHook] string 필드에서 Clear 클립 발견: {field.Name} = {clipName}");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    ModLog.Verbose($"[HighscoreMeterHook] 필드 읽기 실패 ({field.Name}): {ex.Message}");
                }
            }

            return false;
        }
    }
}
