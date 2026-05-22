using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            MelonLogger.Msg("[SXGTDataHook] Initialize() 호출됨");
            
            if (_isInitialized)
            {
                MelonLogger.Msg("[SXGTDataHook] 이미 초기화됨, 리턴");
                return;
            }

            try
            {
                MelonLogger.Msg("[SXGTDataHook] 초기화 시작...");
                
                var harmony = new HarmonyLib.Harmony("sxtg2.SXGTDataHook");

                // SXGTData 타입 찾기
                var sxgtDataType = Helpers.TypeFinderHelper.FindType("SXGTData");
                if (sxgtDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 타입을 찾을 수 없습니다.");
                    return;
                }

                // 생성자 찾기
                var constructors = sxgtDataType.GetConstructors();
                if (constructors.Length == 0)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 생성자를 찾을 수 없습니다.");
                    return;
                }

                // 모든 생성자 후킹
                var postfix = new HarmonyMethod(typeof(SXGTDataHook).GetMethod(nameof(SXGTDataConstructorPostfix), BindingFlags.NonPublic | BindingFlags.Static));
                int patchedCount = 0;
                
                foreach (var constructor in constructors)
                {
                    try
                    {
                        harmony.Patch(constructor, postfix: postfix);
                        patchedCount++;
                        MelonLogger.Msg($"[SXGTDataHook] 생성자 후킹 완료: {constructor.GetParameters().Length}개 매개변수");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SXGTDataHook] 생성자 후킹 실패: {ex.Message}");
                    }
                }

                MelonLogger.Msg($"[SXGTDataHook] 초기화 완료 ({patchedCount}/{constructors.Length}개 생성자 후킹)");
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SXGTDataHook] 초기화 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void SXGTDataConstructorPostfix(ref object __instance)
        {
            try
            {
                if (__instance == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 인스턴스가 null입니다.");
                    return;
                }

                var sxgtDataType = __instance.GetType();
                if (sxgtDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 타입을 찾을 수 없습니다.");
                    return;
                }

                // 원본 노트 제거 및 커스텀 차트 주입은 ManagerPlayHook의 메서드 호출 시점으로 이동
                // 여기서는 인스턴스만 저장
                _pendingSXGTDataInstance = __instance;
                _pendingSXGTDataType = sxgtDataType;
                MelonLogger.Msg($"[SXGTDataHook] SXGTData 생성자 호출됨, 인스턴스 저장 완료 (타입: {sxgtDataType.Name})");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SXGTDataHook] 후킹 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static object _pendingSXGTDataInstance = null;
        private static Type _pendingSXGTDataType = null;
        private static Type _actualNoteType = null; // laneData에서 추출한 실제 노트 타입
        private static Type _shortNoteType = null; // ShortNote 타입
        private static Type _holdNoteType = null; // HoldNote 타입
        
        // 생성자 저장 (타입 추출 시 함께 찾아서 저장)
        private static ConstructorInfo _shortNoteConstructor = null; // ShortNote 기본 생성자 (timing, lane)
        private static ConstructorInfo _holdNoteBasicConstructor = null; // HoldNote 기본 생성자 (timing, lane)
        private static ConstructorInfo _holdNoteDurationConstructor = null; // HoldNote duration 생성자 (timing, nType, nColor, lane, duration)
        private static ConstructorInfo _holdNoteTickTimeConstructor = null; // HoldNote tickTime 생성자 (timing, nType, nColor, lane, tickTime[])

        /// <summary>
        /// 씬 재시작 시 호출하여 대기 중인 인스턴스를 리셋합니다.
        /// </summary>
        public static void ResetPendingInstance()
        {
            _pendingSXGTDataInstance = null;
            _pendingSXGTDataType = null;
            MelonLogger.Msg("[SXGTDataHook] 대기 중인 SXGTData 인스턴스 리셋 완료");
        }

        public static Type GetActualNoteType()
        {
            return _actualNoteType;
        }

        public static Type GetShortNoteType()
        {
            return _shortNoteType ?? _actualNoteType;
        }

        public static Type GetHoldNoteType()
        {
            return _holdNoteType ?? _actualNoteType;
        }

        /// <summary>
        /// ShortNote 생성자를 반환합니다. (timing, lane)
        /// </summary>
        public static ConstructorInfo GetShortNoteConstructor()
        {
            return _shortNoteConstructor;
        }

        /// <summary>
        /// HoldNote 기본 생성자를 반환합니다. (timing, lane)
        /// </summary>
        public static ConstructorInfo GetHoldNoteBasicConstructor()
        {
            return _holdNoteBasicConstructor;
        }

        /// <summary>
        /// HoldNote duration 생성자를 반환합니다. (timing, nType, nColor, lane, duration)
        /// </summary>
        public static ConstructorInfo GetHoldNoteDurationConstructor()
        {
            return _holdNoteDurationConstructor;
        }

        /// <summary>
        /// HoldNote tickTime 생성자를 반환합니다. (timing, nType, nColor, lane, tickTime[])
        /// </summary>
        public static ConstructorInfo GetHoldNoteTickTimeConstructor()
        {
            return _holdNoteTickTimeConstructor;
        }

    }
}

