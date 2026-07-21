using MelonLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System;
using UnityEngine;
using sxtg2.Helpers.Track;
using sxtg2.Helpers;
using sxtg2.Loaders;

namespace sxtg2.Features
{
    public static partial class TrackDataAnalyzer
    {
        public static void AnalyzeTrackData(object trackData)
        {
            // 리플렉션 로깅 오버헤드를 막기 위해 기능 비활성화
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
        
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static List<string> GetAlbumFolders(string hwaFolder)
        {
            return BmsFileResolver.GetAlbumFoldersOrRootWithBms(hwaFolder);
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private const string LogPrefix = "[TrackDataAnalyzer]";

        /// <summary>
        /// 첫 번째 TrackData를 복사하여 주입합니다. (단일 앨범 지원)
        /// </summary>
        public static void InjectFirstTrackData(object firstTrack, object trackDatasList, FieldInfo trackDatasField, object managerInstance)
        {
            try
            {
                MelonLogger.Msg($"{LogPrefix} === 첫 번째 TrackData 복사 및 주입 ===");

                if (firstTrack == null || trackDatasList == null)
                {
                    MelonLogger.Error($"{LogPrefix} 첫 번째 TrackData 또는 trackDatas 리스트가 null입니다.");
                    return;
                }

                object clonedTrack = TrackDataCloner.CloneTrackData(firstTrack);
                if (clonedTrack == null)
                {
                    MelonLogger.Error($"{LogPrefix} TrackData 복사 실패");
                    return;
                }

                ApplyTrackInfo(clonedTrack, firstTrack.GetType());
                AddToTrackDataList(clonedTrack, trackDatasList);

                MelonLogger.Msg($"{LogPrefix} === TrackData 주입 완료 ===");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"{LogPrefix} TrackData 주입 중 오류 발생: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// 여러 커스텀 앨범을 주입합니다. (다중 앨범 지원)
        /// </summary>
        public static void InjectMultipleTrackData(object firstTrack, object trackDatasList, FieldInfo trackDatasField, object managerInstance)
        {
            try
            {
                MelonLogger.Msg($"{LogPrefix} === 다중 커스텀 앨범 주입 시작 ===");

                if (firstTrack == null || trackDatasList == null)
                {
                    MelonLogger.Error($"{LogPrefix} 첫 번째 TrackData 또는 trackDatas 리스트가 null입니다.");
                    return;
                }

                Type trackDataType = firstTrack.GetType();
                string gamePath = Path.GetDirectoryName(Application.dataPath);
                string hwaFolder = Path.Combine(gamePath, "hwa");

                MelonLogger.Msg($"{LogPrefix} hwa 폴더 경로: {hwaFolder}");

                if (!Directory.Exists(hwaFolder))
                {
                    MelonLogger.Warning($"{LogPrefix} hwa 폴더가 존재하지 않습니다: {hwaFolder}");
                    return;
                }

                MelonLogger.Msg($"{LogPrefix} hwa 폴더 발견!");

                var albumFolders = GetAlbumFolders(hwaFolder);
                int folderSuccessCount = 0;
                int allBmsFileCount = 0;

                foreach (var albumFolder in albumFolders)
                {
                    if (TryInjectTrackFromAlbumFolder(firstTrack, trackDataType, trackDatasList, albumFolder, out var bmsFileCount))
                    {
                        folderSuccessCount++;
                    }
                    allBmsFileCount += bmsFileCount;
                }

                MelonLogger.Msg($"{LogPrefix} === 다중 앨범 주입 완료 ===");
                MelonLogger.Msg($"  폴더 트랙: {folderSuccessCount}/{allBmsFileCount}개 추가");
                MelonLogger.Msg($"  총 추가된 트랙: {folderSuccessCount}개");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"{LogPrefix} 다중 앨범 주입 중 오류 발생: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static bool TryInjectTrackFromAlbumFolder(
            object firstTrack,
            Type trackDataType,
            object trackDatasList,
            string albumFolder,
            out int bmsFileCount)
        {
            bmsFileCount = 0;
            string albumName = Path.GetFileName(albumFolder);
            if (string.IsNullOrEmpty(albumName))
                albumName = "Root";

            MelonLogger.Msg($"  앨범 처리 중: {albumName}");

            try
            {
                var bmsFiles = BmsFileResolver.FindInFolder(albumFolder);

                bmsFileCount = bmsFiles.Count;
                if (bmsFiles.Count == 0)
                {
                    MelonLogger.Msg("    BMS 파일 없음, 건너뜀");
                    return false;
                }

                var bmsFile = bmsFiles[0];
                string fileName = Path.GetFileNameWithoutExtension(bmsFile);
                MelonLogger.Msg($"    BMS 파일: {Path.GetFileName(bmsFile)} (앨범: {albumName})");

                object clonedTrack = TrackDataCloner.CloneTrackData(firstTrack);
                if (clonedTrack == null)
                {
                    MelonLogger.Warning($"    TrackData 복사 실패: {albumName}");
                    return false;
                }

                FieldInfo idField = trackDataType.GetField("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                string originalTrackId = idField?.GetValue(clonedTrack) as string ?? "";
                MelonLogger.Msg($"    원본 Track ID 유지: {originalTrackId}");

                ApplyTrackInfo(clonedTrack, trackDataType, originalTrackId, albumFolder, fileName);
                return AddToTrackDataList(clonedTrack, trackDatasList);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 앨범 폴더 처리 실패 ({albumName}): {ex.Message}");
                return false;
            }
        }

    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static void ApplyTrackInfo(object clonedTrack, Type trackDataType, string trackId = null, string albumFolder = null, string bmsFileName = null)
        {
            try
            {
                FieldInfo idField = trackDataType.GetField("ID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (trackId == null)
                {
                    trackId = idField?.GetValue(clonedTrack) as string ?? "";
                }

                if (albumFolder == null)
                {
                    string gamePath = Path.GetDirectoryName(Application.dataPath);
                    albumFolder = Path.Combine(gamePath, "hwa");
                }

                string searchKey = bmsFileName ?? trackId;
                TrackInfoParser.TrackInfo trackInfo = TrackInfoParser.ParseTrackInfo(albumFolder, searchKey);

                FieldInfo displayNameField = trackDataType.GetField("DisplayName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (displayNameField != null)
                {
                    string displayName = !string.IsNullOrEmpty(trackInfo.Title) ? trackInfo.Title : ReflectionMemberNames.TextValues.DefaultCustomChartTitle;
                    displayNameField.SetValue(clonedTrack, displayName);
                }

                if (!string.IsNullOrEmpty(trackInfo.Artist))
                {
                    SetFieldValue(clonedTrack, trackDataType, new[] { "Artist", "artist", "Composer", "composer", "작곡가" }, trackInfo.Artist);
                }

                if (trackInfo.Difficulties.Count > 0)
                {
                    SetDifficultyFields(clonedTrack, trackDataType, trackInfo.Difficulties);
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"{LogPrefix} 곡 정보 적용 실패: {ex.Message}");
            }
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static bool AddToTrackDataList(object clonedTrack, object trackDatasList)
        {
            try
            {
                MelonLogger.Msg($"[AddToTrackDataList] 트랙 추가 시도 중... trackDatasList 타입: {trackDatasList?.GetType().Name}");

                if (trackDatasList is System.Collections.IList list)
                {
                    MelonLogger.Msg($"[AddToTrackDataList] IList로 캐스팅 성공, 현재 크기: {list.Count}");
                    list.Add(clonedTrack);
                    MelonLogger.Msg($"[AddToTrackDataList] IList.Add() 성공, 새 크기: {list.Count}");
                    return true;
                }

                MelonLogger.Msg("[AddToTrackDataList] IList가 아님, Add 메서드 찾기 시도");
                Type listType = trackDatasList.GetType();
                MelonLogger.Msg($"[AddToTrackDataList] 리스트 타입: {listType.FullName}");

                MethodInfo addMethod = listType.GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
                if (addMethod != null)
                {
                    MelonLogger.Msg($"[AddToTrackDataList] Add 메서드 발견: {addMethod.Name}");
                    addMethod.Invoke(trackDatasList, new object[] { clonedTrack });
                    MelonLogger.Msg("[AddToTrackDataList] Add 메서드 호출 성공");
                    return true;
                }

                MelonLogger.Error("[AddToTrackDataList] trackDatas 리스트에 Add 메서드를 찾을 수 없습니다.");
                var methods = listType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                MelonLogger.Msg("[AddToTrackDataList] 사용 가능한 메서드들:");
                foreach (var method in methods)
                {
                    MelonLogger.Msg($"  - {method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})");
                }

                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AddToTrackDataList] TrackData 리스트 추가 실패: {ex.Message}");
                MelonLogger.Error($"[AddToTrackDataList] 스택 트레이스: {ex.StackTrace}");
                return false;
            }
        }
    
    }
}
