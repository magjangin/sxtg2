using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.Audio
{
    public static partial class HighscoreMeterHook
    {
        private static Dictionary<string, AudioClip> _audioClipCache = new Dictionary<string, AudioClip>();

        /// <summary>
        /// 오디오 클립을 캐시에서 가져오거나 로드합니다.
        /// </summary>
        public static AudioClip GetCachedAudioClip(string clipName)
        {
            try
            {
                if (string.IsNullOrEmpty(clipName))
                    return null;

                if (_audioClipCache.ContainsKey(clipName))
                {
                    return _audioClipCache[clipName];
                }

                // 오디오 클립 로드 시도
                var audioClip = LoadAudioClip(clipName);
                if (audioClip != null)
                {
                    _audioClipCache[clipName] = audioClip;
                }

                return audioClip;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] 오디오 클립 캐시 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 오디오 클립을 로드합니다.
        /// </summary>
        private static AudioClip LoadAudioClip(string clipName)
        {
            try
            {
                // Resources에서 로드 시도
                var audioClip = Resources.Load<AudioClip>(clipName);
                if (audioClip != null)
                {
                    return audioClip;
                }

                MelonLogger.Warning($"[HighscoreMeterHook] 오디오 클립을 찾을 수 없습니다: {clipName}");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] 오디오 클립 로드 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 오디오 클립 캐시를 초기화합니다.
        /// </summary>
        public static void ClearAudioClipCache()
        {
            try
            {
                foreach (var clip in _audioClipCache.Values)
                {
                    if (clip != null)
                    {
                        Resources.UnloadAsset(clip);
                    }
                }
                _audioClipCache.Clear();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[HighscoreMeterHook] 오디오 클립 캐시 초기화 실패: {ex.Message}");
            }
        }
    }
}



















