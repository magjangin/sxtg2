using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Hooks.Audio
{
    public static partial class HighscoreMeterHook
    {
        /// <summary>
        /// SoundObject의 클립을 교체합니다.
        /// </summary>
        public static void ReplaceSoundObjectClip(object soundObjectInstance, string newClipName)
        {
            try
            {
                if (soundObjectInstance == null || string.IsNullOrEmpty(newClipName))
                    return;

                var soundObjectType = soundObjectInstance.GetType();
                var clipNameField = soundObjectType.GetField("clipName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (clipNameField != null)
                {
                    clipNameField.SetValue(soundObjectInstance, newClipName);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] SoundObject 클립 교체 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// SoundObject의 오디오 소스를 교체합니다.
        /// </summary>
        public static void ReplaceSoundObjectAudioSource(object soundObjectInstance, UnityEngine.AudioClip newClip)
        {
            try
            {
                if (soundObjectInstance == null || newClip == null)
                    return;

                var soundObjectType = soundObjectInstance.GetType();
                var audioSourceField = soundObjectType.GetField("audioSource", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (audioSourceField != null)
                {
                    var audioSource = audioSourceField.GetValue(soundObjectInstance) as UnityEngine.AudioSource;
                    if (audioSource != null)
                    {
                        audioSource.clip = newClip;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] SoundObject AudioSource 교체 실패: {ex.Message}");
            }
        }
    }
}



















