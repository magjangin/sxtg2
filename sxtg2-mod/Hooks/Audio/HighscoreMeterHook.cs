using System;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using sxtg2.Helpers;
using sxtg2.Helpers.Track;

namespace sxtg2.Hooks.Audio
{
    public static partial class HighscoreMeterHook
    {
        private static bool _isInitialized = false;
        
        // 디버그 모드: true로 설정하면 모든 디버깅 로그가 출력됩니다

        public static void Initialize()
        {
            MelonLogger.Msg("[HighscoreMeterHook] Initialize() 호출됨");
            
            if (_isInitialized)
            {
                MelonLogger.Msg("[HighscoreMeterHook] 이미 초기화됨, 리턴");
                return;
            }

            try
            {
                MelonLogger.Msg("[HighscoreMeterHook] 초기화 시작...");
                
                var harmony = new HarmonyLib.Harmony("sxtg2.HighscoreMeterHook");

                // SoundObject.Play 후킹
                HookSoundObjectPlay(harmony);

                // SoundObject.PlayAndDestroy 후킹
                HookSoundObjectPlayAndDestroy(harmony);

                MelonLogger.Msg("[HighscoreMeterHook] 초기화 완료");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HighscoreMeterHook] 초기화 실패: {ex.Message}");
            }
        }

        private static void HookSoundObjectPlay(HarmonyLib.Harmony harmony)
        {
            try
            {
                var soundObjectType = Helpers.TypeFinderHelper.FindType("SoundObject");
                if (soundObjectType == null)
                    return;

                var playMethod = soundObjectType.GetMethod("Play", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (playMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(HighscoreMeterHook).GetMethod(nameof(SoundObjectPlayPrefix), BindingFlags.NonPublic | BindingFlags.Static));
                    var postfix = new HarmonyMethod(typeof(HighscoreMeterHook).GetMethod(nameof(SoundObjectPlayPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                    harmony.Patch(playMethod, prefix: prefix, postfix: postfix);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HighscoreMeterHook] SoundObject.Play 후킹 실패: {ex.Message}");
            }
        }

        private static void HookSoundObjectPlayAndDestroy(HarmonyLib.Harmony harmony)
        {
            try
            {
                var soundObjectType = Helpers.TypeFinderHelper.FindType("SoundObject");
                if (soundObjectType == null)
                    return;

                var playAndDestroyMethod = soundObjectType.GetMethod("PlayAndDestroy", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (playAndDestroyMethod != null)
                {
                    var prefix = new HarmonyMethod(typeof(HighscoreMeterHook).GetMethod(nameof(SoundObjectPlayAndDestroyPrefix), BindingFlags.NonPublic | BindingFlags.Static));
                    harmony.Patch(playAndDestroyMethod, prefix: prefix);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HighscoreMeterHook] SoundObject.PlayAndDestroy 후킹 실패: {ex.Message}");
            }
        }

        private static bool SoundObjectPlayPrefix(object __instance)
        {
            try
            {
                // 1. __instance를 유니티의 MonoBehaviour로 변환 (이게 핵심!)
                var myObject = __instance as MonoBehaviour;
                
                if (myObject != null)
                {
                    // 2. 이 게임 오브젝트에 붙어있는 'AudioSource' 컴포넌트를 직접 가져옴
                    var audioSource = myObject.GetComponent<AudioSource>();
                    
                    if (audioSource != null && audioSource.clip != null)
                    {
                        string realClipName = audioSource.clip.name;
                        
                        // 3. 드디어 이름 확인!
                        if (realClipName.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
                        {
                            ModLog.Verbose($"[HighscoreMeterHook] Clear 클립 감지(SoundObject.Play Prefix): {myObject.name} / 클립: {realClipName}");
                        }
                        else
                        {
                            ModLog.Verbose($"[HighscoreMeterHook] SoundObject.Play Prefix: {myObject.name} / 클립: {realClipName}");
                        }
                    }
                    else
                    {
                        // 소리 파일이 없는 껍데기 SoundObject일 경우
                        // 로그 제거
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Exception("HighscoreMeterHook.SoundObjectPlayPrefix", ex);
            }
            return true; 
        }

        private static void SoundObjectPlayPostfix(ref object __instance)
        {
            try
            {
                ReplaceClearSound(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[HighscoreMeterHook] SoundObjectPlayPostfix: {ex.Message}");
            }
        }

        private static bool SoundObjectPlayAndDestroyPrefix(ref object __instance)
        {
            try
            {
                ReplaceClearSound(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Warning($"[HighscoreMeterHook] SoundObjectPlayAndDestroyPrefix: {ex.Message}");
            }
            return true; // 원본 메서드 실행
        }

        private static void ReplaceClearSound(object soundObjectInstance)
        {
            try
            {
                if (!CustomTrackHelper.IsCustomPlayActive())
                    return;

                if (ModLog.IsVerbose) MelonLogger.Msg("[HighscoreMeterHook] ReplaceClearSound() 시작");
                
                if (soundObjectInstance == null)
                    return;

                var soundObjectType = soundObjectInstance.GetType();
                if (ModLog.IsVerbose) MelonLogger.Msg($"[HighscoreMeterHook] SoundObject 타입: {soundObjectType.Name}");

                if (!SoundObjectClipAccessor.TryReadClipName(soundObjectInstance, out var clipName))
                {
                    if (ModLog.IsVerbose) MelonLogger.Msg("[HighscoreMeterHook] 클립 이름을 확인할 수 없습니다.");
                    return;
                }
                
                if (ModLog.IsVerbose) MelonLogger.Msg($"[HighscoreMeterHook] 현재 오디오 클립: {clipName}");
                
                // 3. Clear 사운드 확인 및 교체
                if (clipName.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
                {
                    ModLog.Verbose($"[HighscoreMeterHook] Clear 사운드 감지: {clipName}");

                    var keyBlueTamClip = GetCachedKeyBlueTamClip();
                    if (keyBlueTamClip == null)
                    {
                        ModLog.Verbose("[HighscoreMeterHook] KeyBlue_Tam 클립을 찾을 수 없습니다.");
                        return;
                    }

                    SoundObjectClipAccessor.TryApplyClip(soundObjectInstance, keyBlueTamClip, clipName);
                }
                else
                {
                    if (ModLog.IsVerbose) MelonLogger.Msg($"[HighscoreMeterHook] Clear 사운드가 아닙니다: {clipName}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HighscoreMeterHook] ReplaceClearSound 오류: {ex.Message}");
            }
        }

        private static AudioClip GetCachedKeyBlueTamClip()
        {
            try
            {
                // AudioSourceHook에서 캐시된 KeyBlue_Tam 가져오기
                var clip = AudioSourceHook.GetCachedKeyBlueTamClip();
                if (clip != null)
                    return clip;
                
                // 캐시가 없으면 Resources에서 직접 로드
                clip = Resources.Load<AudioClip>("KeyBlue_Tam");
                return clip;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[HighscoreMeterHook] KeyBlue_Tam 오디오 클립 가져오기 실패: {ex.Message}");
                return null;
            }
        }
    }
}
