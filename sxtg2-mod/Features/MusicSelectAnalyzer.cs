using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Features
{
    public static partial class MusicSelectAnalyzer
    {
        private static bool _hasAnalyzed = false;

        public static void AnalyzeMusicSelectScene()
        {
            MelonLogger.Msg($"[디버그] AnalyzeMusicSelectScene 호출됨 (현재 플래그: {_hasAnalyzed})");
            
            if (_hasAnalyzed)
            {
                MelonLogger.Msg("[디버그] AnalyzeMusicSelectScene: 이미 분석 완료, 중복 방지로 리턴");
                return; // 이미 분석했으면 중복 방지
            }
            
            MelonLogger.Msg("[디버그] AnalyzeMusicSelectScene: 분석 시작, 플래그를 true로 설정");
            _hasAnalyzed = true; // 분석 시작 플래그 설정 (중복 방지)
            
            try
            {
                MelonLogger.Msg("곡 목록 존재 여부 확인 중 (MusicSelect)...");
                
                // TrackManager 타입 찾기 시도
                Type trackManagerType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.TrackManager);
                if (trackManagerType == null)
                {
                    MelonLogger.Msg("TrackManager 타입을 찾을 수 없습니다.");
                    MelonLogger.Msg("대체 트랙 타입 찾기 시도 중...");
                }
                
                // TrackData 타입 찾기
                Type trackDataType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.TrackData);
                if (trackDataType != null)
                {
                    MelonLogger.Msg($"타입 발견: {trackDataType.Name}");
                }
                
                // 씬의 모든 GameObject 가져오기
                GameObject[] allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
                MelonLogger.Msg($"씬의 GameObject 개수: {allObjects.Length}");
                
                // ScrollRect 찾기 (UI 모듈이 없을 수 있으므로 try-catch로 처리)
                int scrollRectCount = 0;
                try
                {
                    Type scrollRectType = TypeFinderHelper.FindType("UnityEngine.UI.ScrollRect");
                    if (scrollRectType != null)
                    {
                        var findMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new Type[] { });
                        if (findMethod != null)
                        {
                            var genericMethod = findMethod.MakeGenericMethod(scrollRectType);
                            var scrollRects = genericMethod.Invoke(null, null) as Array;
                            scrollRectCount = scrollRects?.Length ?? 0;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MusicSelectAnalyzer] ScrollRect 검색 실패 (UI 모듈 없음 등): {ex.Message}");
                }
                MelonLogger.Msg($"ScrollRect 개수: {scrollRectCount}");
                
                // ManagerMusicSelect 찾기
                AnalyzeManagerMusicSelect(allObjects);
                
                // TrackData 관련 GameObject 찾기
                TrackDataAnalyzer.FindTrackDataRelatedObjects(allObjects, trackDataType);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"MusicSelect 씬 분석 중 오류 발생: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
                _hasAnalyzed = false; // 오류 발생 시 플래그 리셋하여 재시도 가능하도록
            }
        }

        public static void ResetAnalysisFlag()
        {
            _hasAnalyzed = false;
        }
        
        private static void AnalyzeManagerMusicSelect(GameObject[] allObjects)
        {
            try
            {
                MelonLogger.Msg("=== ManagerMusicSelect 분석 ===");
                
                Type managerType = ManagerMusicSelectBridge.TryGetManagerMusicSelectType();
                if (managerType == null)
                {
                    managerType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(asm => asm.GetTypes())
                        .FirstOrDefault(t => t.Name == ReflectionMemberNames.Types.ManagerMusicSelect);
                }
                
                if (managerType == null)
                {
                    MelonLogger.Msg("ManagerMusicSelect 타입을 찾을 수 없습니다.");
                    return;
                }
                
                MelonLogger.Msg($"ManagerMusicSelect 타입 발견: {managerType.FullName}");
                
                object instance = ManagerMusicSelectBridge.TryGetManagerInstance(managerType);
                
                MelonLogger.Msg($"ManagerMusicSelect.Instance: {(instance != null ? "있음" : "없음")}");
                
                if (instance != null)
                {
                    // trackDatas 필드 찾기
                    FieldInfo trackDatasField = managerType.GetField("trackDatas", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (trackDatasField == null)
                    {
                        // 백킹 필드 찾기
                        trackDatasField = managerType.GetField("<trackDatas>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                    }
                    
                    if (trackDatasField != null)
                    {
                        MelonLogger.Msg($"trackDatas 필드 발견: {trackDatasField.Name}");
                        
                        object trackDatasValue = trackDatasField.GetValue(instance);
                        if (trackDatasValue != null)
                        {
                            if (trackDatasValue is System.Collections.ICollection collection)
                            {
                                MelonLogger.Msg($"trackDatas 리스트 개수: {collection.Count}");
                                
                                if (collection.Count > 0)
                                {
                                    var enumerator = collection.GetEnumerator();
                                    if (enumerator.MoveNext())
                                    {
                                        object firstTrack = enumerator.Current;
                                        TrackDataAnalyzer.AnalyzeTrackData(firstTrack);
                                        
                                        // 다중 앨범 지원: 모든 BMS 파일에 대해 TrackData 생성 및 주입
                                        TrackDataAnalyzer.InjectMultipleTrackData(firstTrack, trackDatasValue, trackDatasField, instance);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"ManagerMusicSelect 분석 중 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
        
    }
}
