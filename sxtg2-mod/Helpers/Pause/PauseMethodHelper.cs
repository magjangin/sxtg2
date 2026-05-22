using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using sxtg2.Loaders;
using sxtg2.Helpers.Track;

namespace sxtg2.Helpers
{
    public static partial class PauseMethodHelper
    {
        private const string LogPrefix = "[PauseMethodHelper]";
        private static Type _cachedPauseType;
        private static MethodInfo _cachedShowMethod;
        private static MethodInfo _cachedStartMethod;

        public static void CallPauseMenu()
        {
            try
            {
                // ESC 키 입력 시 이미지 열거
                LogSXGTDataDetails();
                
                // ESC 키 입력 시 Eyecatch Image 설정 (playTrack -> trackData -> bms 순서로 찾기)
                SetEyecatchImage();

                var pauseType = GetPauseTypeCached();
                if (pauseType == null)
                {
                    MelonLogger.Warning($"{LogPrefix} RG_PS_Pause 타입을 찾을 수 없습니다.");
                    return;
                }

                object pauseInstance = FindPauseInstance(pauseType);
                EnsurePauseMethodsCached(pauseType);

                if (_cachedShowMethod != null)
                {
                    var showParameters = _cachedShowMethod.GetParameters();
                    if (showParameters.Length >= 2)
                    {
                        object curScoreValue = GetCurrentScoreOrDefault();
                        object startFinishRatioValue = 0.01f;

                        curScoreValue = ConvertValueForParameter(curScoreValue, showParameters[0].ParameterType);
                        startFinishRatioValue = ConvertValueForParameter(startFinishRatioValue, showParameters[1].ParameterType);

                        MelonLogger.Msg($"{LogPrefix} [ESC] Show({curScoreValue}, {startFinishRatioValue}) 호출 시도 (타입: {showParameters[0].ParameterType.Name}, {showParameters[1].ParameterType.Name})");

                        if (_cachedShowMethod.IsStatic)
                        {
                            _cachedShowMethod.Invoke(null, new object[] { curScoreValue, startFinishRatioValue });
                        }
                        else if (pauseInstance != null)
                        {
                            _cachedShowMethod.Invoke(pauseInstance, new object[] { curScoreValue, startFinishRatioValue });
                        }
                        else
                        {
                            MelonLogger.Warning($"{LogPrefix} RG_PS_Pause 인스턴스가 없어 Show 인스턴스 메서드를 호출할 수 없습니다.");
                            return;
                        }

                        MelonLogger.Msg($"{LogPrefix} [ESC] Show({curScoreValue}, {startFinishRatioValue}) 호출 완료");
                    }
                }

                if (_cachedStartMethod != null)
                {
                    MelonLogger.Msg($"{LogPrefix} [ESC] Start() 호출 시도");

                    if (_cachedStartMethod.IsStatic)
                    {
                        _cachedStartMethod.Invoke(null, null);
                    }
                    else if (pauseInstance != null)
                    {
                        _cachedStartMethod.Invoke(pauseInstance, null);
                    }

                    MelonLogger.Msg($"{LogPrefix} [ESC] Start() 호출 완료");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[PauseMethodHelper] ESC 일시정지 메뉴 호출 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static Type GetPauseTypeCached()
        {
            if (_cachedPauseType != null)
                return _cachedPauseType;

            _cachedPauseType = TypeFinderHelper.FindType("RG_PS_Pause");
            return _cachedPauseType;
        }

        private static void EnsurePauseMethodsCached(Type pauseType)
        {
            if (pauseType == null)
                return;

            if (_cachedShowMethod == null)
            {
                _cachedShowMethod = pauseType.GetMethod("Show", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }

            if (_cachedStartMethod == null)
            {
                _cachedStartMethod = pauseType.GetMethod("Start", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            }
        }

        private static object GetCurrentScoreOrDefault()
        {
            object curScoreValue = 0;
            var managerPlayType = TypeFinderHelper.FindType("ManagerPlay");
            if (managerPlayType == null)
                return curScoreValue;

            var managerPlayInstance = UnityEngine.Object.FindObjectOfType(managerPlayType);
            if (managerPlayInstance == null)
                return curScoreValue;

            var curScoreField = managerPlayType.GetField("curScore", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (curScoreField == null)
                return curScoreValue;

            var value = curScoreField.GetValue(managerPlayInstance);
            return value ?? curScoreValue;
        }

        private static object ConvertValueForParameter(object value, Type parameterType)
        {
            if (parameterType == typeof(int))
                return Convert.ToInt32(value);
            if (parameterType == typeof(float))
                return Convert.ToSingle(value);
            return value;
        }

    }
}
