using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using sxtg2.Loaders;
using sxtg2.Helpers.Track;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        public static void AnalyzeTrackData(object trackData)
        {
            try
            {
                if (trackData == null)
                    return;
                    
                Type trackDataType = trackData.GetType();
                MelonLogger.Msg($"TrackData 타입: {trackDataType.Name}");
                MelonLogger.Msg($"TrackData 전체 타입: {trackDataType.FullName}");
                
                FieldInfo[] fields = trackDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                PropertyInfo[] properties = trackDataType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                MelonLogger.Msg($"TrackData 필드 개수: {fields.Length}, 속성 개수: {properties.Length}");
                MelonLogger.Msg("=== TrackData 필드 상세 목록 ===");
                
                foreach (var field in fields)
                {
                    try
                    {
                        object value = field.GetValue(trackData);
                        string valueStr = GetValueString(value);
                        
                        // 난이도 관련 필드 강조
                        string fieldNameUpper = field.Name.ToUpper();
                        bool isLevelField = fieldNameUpper.IndexOf("LEVEL") >= 0 || 
                                          fieldNameUpper.IndexOf("DIFFICULTY") >= 0 ||
                                          fieldNameUpper.IndexOf("LITE") >= 0;
                        
                        if (isLevelField)
                        {
                            MelonLogger.Msg($"  ⭐ [난이도] {field.Name} ({field.FieldType.Name}) = {valueStr}");
                        }
                        else
                        {
                            MelonLogger.Msg($"  {field.Name} ({field.FieldType.Name}) = {valueStr}");
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"  {field.Name} ({field.FieldType.Name}) = [읽기 오류: {ex.Message}]");
                    }
                }
                
                MelonLogger.Msg("=== TrackData 속성 상세 목록 ===");
                foreach (var prop in properties)
                {
                    try
                    {
                        if (prop.CanRead)
                        {
                            object value = prop.GetValue(trackData);
                            string valueStr = GetValueString(value);
                            
                            // 난이도 관련 속성 강조
                            string propNameUpper = prop.Name.ToUpper();
                            bool isLevelProp = propNameUpper.IndexOf("LEVEL") >= 0 || 
                                             propNameUpper.IndexOf("DIFFICULTY") >= 0 ||
                                             propNameUpper.IndexOf("LITE") >= 0;
                            
                            if (isLevelProp)
                            {
                                MelonLogger.Msg($"  ⭐ [난이도] {prop.Name} ({prop.PropertyType.Name}) = {valueStr}");
                            }
                            else
                            {
                                MelonLogger.Msg($"  {prop.Name} ({prop.PropertyType.Name}) = {valueStr}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Msg($"  {prop.Name} ({prop.PropertyType.Name}) = [읽기 오류: {ex.Message}]");
                    }
                }
                
                // 생성자 정보
                ConstructorInfo[] constructors = trackDataType.GetConstructors();
                MelonLogger.Msg($"=== TrackData 생성자 목록 ({constructors.Length}개) ===");
                foreach (var ctor in constructors)
                {
                    ParameterInfo[] parameters = ctor.GetParameters();
                    string paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    MelonLogger.Msg($"  {trackDataType.Name}({paramStr})");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"TrackData 분석 중 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
        
        public static string GetValueString(object value)
        {
            if (value == null)
                return "null";
            
            try
            {
                if (value is Array arr)
                {
                    // 배열의 각 요소 출력
                    var elements = new List<string>();
                    for (int i = 0; i < Math.Min(arr.Length, 10); i++) // 최대 10개만
                    {
                        elements.Add(arr.GetValue(i)?.ToString() ?? "null");
                    }
                    string result = $"{arr.GetType().GetElementType().Name}[{arr.Length}]";
                    if (arr.Length > 0)
                    {
                        result += $" = [{string.Join(", ", elements)}";
                        if (arr.Length > 10)
                            result += $", ... ({arr.Length - 10}개 더)";
                        result += "]";
                    }
                    return result;
                }
                else if (value is System.Collections.ICollection coll && !(value is string))
                {
                    // 컬렉션의 각 요소 출력
                    var elements = new List<string>();
                    int count = 0;
                    foreach (var item in coll)
                    {
                        if (count++ >= 10) break; // 최대 10개만
                        elements.Add(item?.ToString() ?? "null");
                    }
                    string result = $"{value.GetType().Name} (Count: {coll.Count})";
                    if (coll.Count > 0)
                    {
                        result += $" = [{string.Join(", ", elements)}";
                        if (coll.Count > 10)
                            result += $", ... ({coll.Count - 10}개 더)";
                        result += "]";
                    }
                    return result;
                }
                else if (value is System.Collections.IDictionary dict)
                {
                    // Dictionary의 각 키-값 쌍 출력
                    var pairs = new List<string>();
                    int count = 0;
                    foreach (System.Collections.DictionaryEntry entry in dict)
                    {
                        if (count++ >= 10) break; // 최대 10개만
                        pairs.Add($"{entry.Key}={entry.Value}");
                    }
                    string result = $"{value.GetType().Name} (Count: {dict.Count})";
                    if (dict.Count > 0)
                    {
                        result += $" = {{{string.Join(", ", pairs)}";
                        if (dict.Count > 10)
                            result += $", ... ({dict.Count - 10}개 더)";
                        result += "}";
                    }
                    return result;
                }
                else
                {
                    return value.ToString();
                }
            }
            catch
            {
                return value.ToString();
            }
        }
        
        public static void FindTrackDataRelatedObjects(GameObject[] allObjects, Type trackDataType)
        {
            try
            {
                MelonLogger.Msg("=== TrackData 사용 컴포넌트 검색 ===");
                
                if (trackDataType == null)
                    return;
                
                // 모든 MonoBehaviour에서 TrackData 필드 찾기
                MonoBehaviour[] allMonoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mono in allMonoBehaviours)
                {
                    if (mono == null)
                        continue;
                        
                    Type monoType = mono.GetType();
                    FieldInfo[] fields = monoType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    foreach (var field in fields)
                    {
                        if (field.FieldType == trackDataType || 
                            (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>) && 
                             field.FieldType.GetGenericArguments()[0] == trackDataType))
                        {
                            try
                            {
                                object value = field.GetValue(mono);
                                string valueStr = value?.ToString() ?? "null";
                                if (value is System.Collections.ICollection coll)
                                {
                                    valueStr = $"{value.GetType().Name} (Count: {coll.Count})";
                                }
                                MelonLogger.Msg($"  TrackData 필드 발견: {monoType.FullName}.{field.Name} = {valueStr}");
                            }
                            catch (Exception ex)
                            {
                                MelonLogger.Warning($"[TrackDataAnalyzer] 필드 값 읽기 실패 {monoType.Name}.{field.Name}: {ex.Message}");
                            }
                        }
                    }
                }
                
                MelonLogger.Msg("=== 관련 GameObject 목록 ===");
                foreach (var obj in allObjects)
                {
                    if (obj == null)
                        continue;
                        
                    // TrackData와 관련된 이름 패턴 찾기
                    string objName = obj.name;
                    if (objName.Contains("Track") || objName.Contains("Music") || objName.Contains("Select"))
                    {
                        MelonLogger.Msg($"관련 GameObject 발견: {objName} (타입: {obj.GetType().Name})");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"관련 GameObject 검색 중 오류: {ex.Message}");
            }
        }
        
    }
}

