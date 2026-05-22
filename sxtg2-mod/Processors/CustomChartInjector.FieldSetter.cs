using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static partial class CustomChartInjector
    {
        /// <summary>
        /// 노트 인스턴스의 필드를 설정합니다.
        /// </summary>
        private static void SetNoteFields(object noteInstance, BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            // 필드 설정 (여러 가능한 이름 중 하나만 찾아서 설정)
            SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "timing", "Time" }, parsedNote.Time);
            
            if (nTypeValue != null)
            {
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "nType", "Type" }, nTypeValue);
            }
            
            if (nColorValue != null)
            {
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "nColor", "Color" }, nColorValue);
            }
            
            // 홀드 노트의 경우 추가 필드 설정
            if (parsedNote.Length > 0f)
            {
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "duration", "Duration" }, parsedNote.Length);
                
                var tickTimeArray = GenerateTickTimeArray(parsedNote.Time, parsedNote.Length);
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "tickTime", "TickTime" }, tickTimeArray);
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "tickLength", "TickLength" }, tickTimeArray.Length);
                
                // tickJudge 배열 초기화
                var tickJudgeArray = new bool[tickTimeArray.Length];
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "tickJudge", "TickJudge" }, tickJudgeArray);
                
                // 홀드 노트 상태 필드 초기화
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "isFinished", "IsFinished" }, false);
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "isHeadJudged", "IsHeadJudged" }, false);
                SetNoteFieldOnce(noteInstance, actualNoteType, new[] { "elapsedTick", "ElapsedTick" }, 0);
            }
        }

        /// <summary>
        /// 여러 가능한 필드 이름 중 하나를 찾아서 설정합니다. (첫 번째로 찾은 필드만 설정)
        /// </summary>
        private static void SetNoteFieldOnce(object instance, Type type, string[] fieldNames, object value)
        {
            if (instance == null || type == null || fieldNames == null || fieldNames.Length == 0)
                return;

            foreach (var fieldName in fieldNames)
            {
                var field = FindFieldCaseInsensitive(type, fieldName);
                if (field != null)
                {
                    try
                    {
                        field.SetValue(instance, value);
                        return; // 성공하면 바로 종료
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[CustomChartInjector] 필드 설정 실패: {type.Name}.{fieldName} = {ex.Message}");
                        // 다음 이름 시도
                    }
                }
            }
        }

        /// <summary>
        /// 단일 필드 이름으로 필드를 설정합니다.
        /// </summary>
        private static void SetNoteField(object instance, Type type, string fieldName, object value)
        {
            if (instance == null || type == null || string.IsNullOrEmpty(fieldName))
                return;

            try
            {
                var field = FindFieldCaseInsensitive(type, fieldName);
                if (field != null)
                {
                    field.SetValue(instance, value);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartInjector] 필드 설정 실패: {type.Name}.{fieldName} = {ex.Message}");
            }
        }

        // 필드 검색 결과 캐시 (타입별, 필드명별)
        private static readonly Dictionary<string, FieldInfo> _fieldCache = new Dictionary<string, FieldInfo>();

        private static FieldInfo FindFieldCaseInsensitive(Type type, string fieldName)
        {
            if (type == null || string.IsNullOrEmpty(fieldName))
                return null;

            // 캐시 키 생성 (타입명 + 필드명)
            var cacheKey = $"{type.FullName}.{fieldName}";
            
            // 캐시에서 먼저 확인
            if (_fieldCache.TryGetValue(cacheKey, out var cachedField))
            {
                return cachedField;
            }

            FieldInfo field = null;

            // 먼저 정확한 이름으로 찾기
            field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            // 대소문자 무시하고 찾기
            if (field == null)
            {
                var allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                field = allFields.FirstOrDefault(f => 
                    string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
            }

            // 캐시에 저장 (null이어도 저장하여 불필요한 재검색 방지)
            if (field != null)
            {
                _fieldCache[cacheKey] = field;
            }

            return field;
        }
    }
}









































