using System;
using System.Reflection;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
        internal static Type ExtractNoteDataTypeFromLaneData(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                var laneDataField = sxgtDataType.GetField(
                    ReflectionMemberNames.SXGTDataMembers.LaneData,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (laneDataField == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] laneData 필드를 찾을 수 없습니다.");
                    return TypeFinderHelper.FindType("Note");
                }

                MelonLogger.Msg($"[SXGTDataHook] laneData 필드 타입: {laneDataField.FieldType.FullName}");

                var fromFieldDecl = TryExtractNoteTypeFromLaneDictionaryType(laneDataField.FieldType, requireFloatIntCtor: true);
                if (fromFieldDecl != null)
                {
                    return fromFieldDecl;
                }

                var laneData = laneDataField.GetValue(sxgtDataInstance);
                if (laneData != null)
                {
                    MelonLogger.Msg($"[SXGTDataHook] laneData 실제 타입: {laneData.GetType().FullName}");
                    var fromRuntime = TryExtractNoteTypeFromLaneDictionaryType(laneData.GetType(), requireFloatIntCtor: false);
                    if (fromRuntime != null)
                    {
                        return fromRuntime;
                    }
                }

                var noteType = TypeFinderHelper.FindType("Note");
                if (noteType != null)
                {
                    MelonLogger.Msg($"[SXGTDataHook] Note 타입 직접 찾기 성공: {noteType.FullName}");
                    return noteType;
                }

                MelonLogger.Warning("[SXGTDataHook] 모든 방법으로 Note 타입을 찾을 수 없습니다.");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] Note 타입 추출 실패: {ex.Message}");
                MelonLogger.Warning(ex.StackTrace);
                return null;
            }
        }

        private static Type TryExtractNoteTypeFromLaneDictionaryType(Type laneDictionaryType, bool requireFloatIntCtor)
        {
            if (!laneDictionaryType.IsGenericType)
            {
                return null;
            }

            var genericArgs = laneDictionaryType.GetGenericArguments();
            MelonLogger.Msg($"[SXGTDataHook] laneData 제네릭 인자 개수: {genericArgs.Length}");
            if (genericArgs.Length < 2)
            {
                return null;
            }

            var listType = genericArgs[1];
            MelonLogger.Msg($"[SXGTDataHook] List 타입: {listType.FullName}");

            if (!listType.IsGenericType)
            {
                return null;
            }

            var listGenericArgs = listType.GetGenericArguments();
            if (listGenericArgs.Length < 1)
            {
                return null;
            }

            var noteDataType = listGenericArgs[0];
            if (requireFloatIntCtor && !HasFloatIntTwoParameterConstructor(noteDataType))
            {
                return null;
            }

            if (requireFloatIntCtor)
            {
                MelonLogger.Msg($"[SXGTDataHook] laneData 필드 타입에서 Note 타입 추출 성공: {noteDataType.FullName}");
                MelonLogger.Msg("[SXGTDataHook] Note(float, int) 생성자 확인됨");
            }
            else
            {
                MelonLogger.Msg($"[SXGTDataHook] laneData 값에서 Note 타입 추출 성공: {noteDataType.FullName}");
            }

            return noteDataType;
        }

        private static bool HasFloatIntTwoParameterConstructor(Type noteDataType)
        {
            foreach (var ctor in noteDataType.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType == typeof(float) &&
                    parameters[1].ParameterType == typeof(int))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
