using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        private static void SetFieldValue(object instance, Type type, string[] fieldNames, object value)
        {
            if (instance == null || type == null || fieldNames == null || fieldNames.Length == 0)
                return;

            foreach (var fieldName in fieldNames)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    continue;

                try
                {
                    field.SetValue(instance, value);
                    MelonLogger.Msg($"  필드 설정: {field.Name} = {value}");
                    return;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"  필드 설정 실패: {type.Name}.{fieldName} = {ex.Message}");
                }
            }
        }

        private static void SetDifficultyFields(object trackData, Type trackDataType, List<int> parsedDifficulties)
        {
            if (trackData == null || trackDataType == null || parsedDifficulties == null || parsedDifficulties.Count == 0)
            {
                MelonLogger.Warning("[SetDifficultyFields] 입력값이 유효하지 않습니다.");
                return;
            }

            MelonLogger.Msg($"[SetDifficultyFields] 난이도 배열 할당 시작: [{string.Join(", ", parsedDifficulties)}]");

            FieldInfo[] fields = trackDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            string[] targetFieldNames = { "Level", "Level_LITE" };
            bool hasAssigned = false;

            AssignToKnownLevelFields(trackData, trackDataType, parsedDifficulties, targetFieldNames, ref hasAssigned);
            AssignToHeuristicDifficultyFields(trackData, fields, parsedDifficulties, targetFieldNames, ref hasAssigned);

            if (hasAssigned)
                MelonLogger.Msg($"[SetDifficultyFields] 난이도 덮어쓰기 완료! TrackData에 새 난이도 배열 [{string.Join(", ", parsedDifficulties)}] 할당됨");
            else
                MelonLogger.Warning("[SetDifficultyFields] 난이도 필드를 찾을 수 없어 할당하지 못했습니다.");
        }

        private static void AssignToKnownLevelFields(object trackData, Type trackDataType, List<int> parsedDifficulties, string[] targetFieldNames, ref bool hasAssigned)
        {
            foreach (var fieldName in targetFieldNames)
            {
                FieldInfo field = trackDataType.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                    continue;

                try
                {
                    if (!field.FieldType.IsArray)
                        continue;

                    Type elementType = field.FieldType.GetElementType();
                    if (TryAssignStringDifficultyArray(trackData, field, parsedDifficulties, ref hasAssigned))
                        continue;
                    if (TryAssignIntegralDifficultyArray(trackData, field, parsedDifficulties, elementType, includeFloat: false, ref hasAssigned))
                        continue;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"  난이도 필드 설정 실패: {field.Name} - {ex.Message}");
                }
            }
        }

        private static void AssignToHeuristicDifficultyFields(object trackData, FieldInfo[] fields, List<int> parsedDifficulties, string[] targetFieldNames, ref bool hasAssigned)
        {
            foreach (var field in fields)
            {
                string fieldName = field.Name.ToLower();
                if (!(fieldName.Contains("level") || fieldName.Contains("difficulty")) ||
                    targetFieldNames.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    if (field.FieldType.IsArray)
                    {
                        Type elementType = field.FieldType.GetElementType();
                        if (TryAssignStringDifficultyArray(trackData, field, parsedDifficulties, ref hasAssigned))
                            continue;
                        TryAssignIntegralDifficultyArray(trackData, field, parsedDifficulties, elementType, includeFloat: true, ref hasAssigned);
                    }
                    else if (field.FieldType == typeof(int) || field.FieldType == typeof(float) || field.FieldType == typeof(byte) || field.FieldType == typeof(short))
                    {
                        object value = Convert.ChangeType(parsedDifficulties[0], field.FieldType);
                        field.SetValue(trackData, value);
                        hasAssigned = true;
                        MelonLogger.Msg($"  난이도 설정: {field.Name} = {parsedDifficulties[0]}");
                    }
                    else if (field.FieldType == typeof(string))
                    {
                        string value = parsedDifficulties[0].ToString("D2");
                        field.SetValue(trackData, value);
                        hasAssigned = true;
                        MelonLogger.Msg($"  난이도 설정: {field.Name} = {value}");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"  난이도 필드 설정 실패: {field.Name} - {ex.Message}");
                }
            }
        }

        private static string[] BuildDifficultyStringArray(List<int> parsedDifficulties)
        {
            var difficultyArray = new string[parsedDifficulties.Count];
            for (int i = 0; i < parsedDifficulties.Count; i++)
                difficultyArray[i] = parsedDifficulties[i].ToString("D2");
            return difficultyArray;
        }

        private static bool TryAssignStringDifficultyArray(object trackData, FieldInfo field, List<int> parsedDifficulties, ref bool hasAssigned)
        {
            Type elementType = field.FieldType.GetElementType();
            if (elementType != typeof(string))
                return false;

            string[] difficultyArray = BuildDifficultyStringArray(parsedDifficulties);
            field.SetValue(trackData, difficultyArray);
            hasAssigned = true;
            MelonLogger.Msg($"  난이도 배열 설정: {field.Name} (String[]) = [{string.Join(", ", difficultyArray)}]");
            return true;
        }

        private static bool TryAssignIntegralDifficultyArray(object trackData, FieldInfo field, List<int> parsedDifficulties, Type elementType, bool includeFloat, ref bool hasAssigned)
        {
            if (elementType == null)
                return false;

            bool ok = elementType == typeof(int) || elementType == typeof(byte) || elementType == typeof(short);
            if (includeFloat)
                ok = ok || elementType == typeof(float);
            if (!ok)
                return false;

            Array difficultyArray = Array.CreateInstance(elementType, parsedDifficulties.Count);
            for (int i = 0; i < parsedDifficulties.Count; i++)
                difficultyArray.SetValue(Convert.ChangeType(parsedDifficulties[i], elementType), i);

            field.SetValue(trackData, difficultyArray);
            hasAssigned = true;
            MelonLogger.Msg($"  난이도 배열 설정: {field.Name} ({elementType.Name}[]) = [{string.Join(", ", parsedDifficulties)}]");
            return true;
        }
    }
}
