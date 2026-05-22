using System;
using System.Reflection;
using MelonLoader;

namespace sxtg2.Helpers.Track
{
    /// <summary>
    /// TrackData 복사를 담당하는 헬퍼 클래스입니다.
    /// </summary>
    public static class TrackDataCloner
    {
        /// <summary>
        /// TrackData를 복사합니다.
        /// </summary>
        public static object CloneTrackData(object originalTrack)
        {
            if (originalTrack == null)
            {
                MelonLogger.Error("[TrackDataCloner] 원본 TrackData가 null입니다.");
                return null;
            }

            try
            {
                Type trackDataType = originalTrack.GetType();
                
                // 기본 생성자로 인스턴스 생성
                ConstructorInfo defaultCtor = trackDataType.GetConstructor(Type.EmptyTypes);
                if (defaultCtor == null)
                {
                    MelonLogger.Error("[TrackDataCloner] TrackData 기본 생성자를 찾을 수 없습니다.");
                    return null;
                }

                object clonedTrack = defaultCtor.Invoke(null);
                
                // 모든 필드 복사
                CopyAllFields(originalTrack, clonedTrack, trackDataType);
                
                return clonedTrack;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TrackDataCloner] TrackData 복사 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 모든 필드를 복사합니다.
        /// </summary>
        private static void CopyAllFields(object source, object target, Type trackDataType)
        {
            FieldInfo[] fields = trackDataType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                try
                {
                    object originalValue = field.GetValue(source);
                    
                    if (originalValue == null)
                    {
                        field.SetValue(target, null);
                        continue;
                    }
                    
                    // 배열 복사
                    if (originalValue is Array originalArray)
                    {
                        Array clonedArray = Array.CreateInstance(originalArray.GetType().GetElementType(), originalArray.Length);
                        Array.Copy(originalArray, clonedArray, originalArray.Length);
                        field.SetValue(target, clonedArray);
                    }
                    // Dictionary 복사
                    else if (originalValue is System.Collections.IDictionary originalDict)
                    {
                        Type dictType = originalValue.GetType();
                        object clonedDict = Activator.CreateInstance(dictType);
                        System.Collections.IDictionary clonedDictTyped = clonedDict as System.Collections.IDictionary;
                        
                        foreach (System.Collections.DictionaryEntry entry in originalDict)
                        {
                            clonedDictTyped.Add(entry.Key, entry.Value);
                        }
                        
                        field.SetValue(target, clonedDict);
                    }
                    // 기본 타입 또는 참조 타입 복사
                    else
                    {
                        field.SetValue(target, originalValue);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[TrackDataCloner] 필드 복사 실패: {field.Name} - {ex.Message}");
                }
            }
        }
    }
}

















