using System;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
        private static void StoreNoteTypeAndFindConstructors(Type actualNoteType)
        {
            var typeName = actualNoteType.Name;

            if (typeName.Contains("ShortNote") || typeName.Contains("Short"))
            {
                if (_shortNoteType == null)
                {
                    _shortNoteType = actualNoteType;
                    _actualNoteType = actualNoteType;
                    _shortNoteConstructor = FindShortNoteConstructor(actualNoteType);
                }
            }
            else if (typeName.Contains("HoldNote") || typeName.Contains("Hold"))
            {
                if (_holdNoteType == null)
                {
                    _holdNoteType = actualNoteType;
                    FindHoldNoteConstructors(actualNoteType);
                }
            }
            else if (_actualNoteType == null)
            {
                _actualNoteType = actualNoteType;
            }
        }

        private static ConstructorInfo FindShortNoteConstructor(Type shortNoteType)
        {
            try
            {
                var constructors = shortNoteType.GetConstructors();
                var ctor = constructors.FirstOrDefault(c =>
                {
                    var parameters = c.GetParameters();
                    return parameters.Length == 2 &&
                           parameters[0].ParameterType == typeof(float) &&
                           parameters[1].ParameterType == typeof(int);
                });

                return ctor;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] ShortNote 생성자 찾기 실패: {ex.Message}");
                return null;
            }
        }

        private static void FindHoldNoteConstructors(Type holdNoteType)
        {
            try
            {
                var constructors = holdNoteType.GetConstructors();

                foreach (var ctor in constructors)
                {
                    var parameters = ctor.GetParameters();
                    if (parameters.Length == 2 &&
                        parameters[0].ParameterType == typeof(float) &&
                        parameters[1].ParameterType == typeof(int))
                    {
                        if (_holdNoteBasicConstructor == null)
                        {
                            _holdNoteBasicConstructor = ctor;
                        }
                    }
                    else if (parameters.Length == 5 &&
                             parameters[0].ParameterType == typeof(float) &&
                             parameters[1].ParameterType.IsEnum &&
                             parameters[2].ParameterType.IsEnum &&
                             parameters[3].ParameterType == typeof(int) &&
                             parameters[4].ParameterType == typeof(float))
                    {
                        if (_holdNoteDurationConstructor == null)
                        {
                            _holdNoteDurationConstructor = ctor;
                        }
                    }
                    else if (parameters.Length == 5 &&
                             parameters[0].ParameterType == typeof(float) &&
                             parameters[1].ParameterType.IsEnum &&
                             parameters[2].ParameterType.IsEnum &&
                             parameters[3].ParameterType == typeof(int) &&
                             parameters[4].ParameterType == typeof(float[]))
                    {
                        if (_holdNoteTickTimeConstructor == null)
                        {
                            _holdNoteTickTimeConstructor = ctor;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] HoldNote 생성자 찾기 실패: {ex.Message}");
            }
        }

        private static bool IsHoldNote(object note, Type noteDataType)
        {
            try
            {
                var nTypeField = FindFieldCaseInsensitive(noteDataType, "nType");
                if (nTypeField != null)
                {
                    var nTypeValue = nTypeField.GetValue(note);
                    if (nTypeValue != null)
                    {
                        var nTypeString = nTypeValue.ToString();
                        return nTypeString.ToUpper().Contains("HOLD");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[SXGTDataHook] Hold 노트 판별 중 예외(비-Hold로 처리): {ex.Message}");
            }

            return false;
        }

        private static FieldInfo FindFieldCaseInsensitive(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrEmpty(fieldName))
                return null;

            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field;

            var allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return allFields.FirstOrDefault(f => string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
