using HarmonyLib;
using MelonLoader;
using System.Linq;
using System.Reflection;
using System;
using UnityEngine;
using sxtg2.Helpers.Finders;
using sxtg2.Helpers.Track;
using sxtg2.Helpers;

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

    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        internal static void ClearAllNotes(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                MelonLogger.Msg("[SXGTDataHook] 원본 노트 제거 시작");

                object laneData;
                PropertyInfo itemProp;
                if (!GetLaneDataAccessors(sxgtDataInstance, sxgtDataType, out laneData, out itemProp))
                {
                    return;
                }

                var noteDataType = ExtractNoteDataTypeFromLaneData(sxgtDataInstance, sxgtDataType);
                if (noteDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] Note 타입을 찾을 수 없어 필드 출력을 건너뜁니다.");
                }

                var stats = new NoteRemovalStats();
                for (int lane = 0; lane <= 9; lane++)
                {
                    try
                    {
                        var noteList = itemProp.GetValue(laneData, new object[] { lane });
                        if (noteList != null)
                        {
                            ExtractNoteTypesFromLane(noteList, lane, stats);
                            RemoveNotesFromLane(noteList, lane, stats);
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[SXGTDataHook] 레인 {lane} 제거 실패: {ex.Message}");
                    }
                }

                LogRemovalResults(stats);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SXGTDataHook] 노트 제거 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static bool GetLaneDataAccessors(object sxgtDataInstance, Type sxgtDataType, out object laneData, out PropertyInfo itemProp)
        {
            if (!GameLaneDataHelper.TryGetLaneDataAndItemProperty(sxgtDataType, sxgtDataInstance, out laneData, out itemProp, out var failure))
            {
                switch (failure)
                {
                    case LaneDataAccessFailure.NoLaneField:
                        MelonLogger.Warning("[SXGTDataHook] laneData 필드를 찾을 수 없습니다.");
                        break;
                    case LaneDataAccessFailure.LaneDataNull:
                        MelonLogger.Warning("[SXGTDataHook] laneData가 null입니다.");
                        break;
                    case LaneDataAccessFailure.NoItemProperty:
                        MelonLogger.Warning("[SXGTDataHook] Item 속성을 찾을 수 없습니다.");
                        break;
                    case LaneDataAccessFailure.InvalidArguments:
                        MelonLogger.Warning("[SXGTDataHook] laneData 접근: 인스턴스 또는 타입이 null입니다.");
                        break;
                }

                return false;
            }

            return true;
        }

        private class NoteRemovalStats
        {
            public System.Collections.Generic.HashSet<string> FoundNoteTypes { get; set; } = new System.Collections.Generic.HashSet<string>();
            public int TotalRemoved { get; set; } = 0;
            public int ClearedLanes { get; set; } = 0;
            public int HoldNoteCount { get; set; } = 0;
        }

        private static void ExtractNoteTypesFromLane(object noteList, int lane, NoteRemovalStats stats)
        {
            var countProp = noteList.GetType().GetProperty("Count");
            if (countProp == null) return;

            int beforeCount = (int)countProp.GetValue(noteList);
            if (beforeCount == 0) return;

            var itemProperty = noteList.GetType().GetProperty("Item");
            if (itemProperty == null) return;

            MelonLogger.Msg("[SXGTDataHook] laneData에서 노트 타입 추출 시작");

            for (int i = 0; i < beforeCount; i++)
            {
                if (_shortNoteType != null && _holdNoteType != null)
                {
                    break;
                }

                try
                {
                    var note = itemProperty.GetValue(noteList, new object[] { i });
                    if (note != null)
                    {
                        var actualNoteType = note.GetType();
                        stats.FoundNoteTypes.Add(actualNoteType.FullName);
                        StoreNoteTypeAndFindConstructors(actualNoteType);

                        if (IsHoldNote(note, actualNoteType))
                        {
                            stats.HoldNoteCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[SXGTDataHook] 레인 {lane} 노트 {i} 필드 출력 실패: {ex.Message}");
                }
            }
        }


        private static void RemoveNotesFromLane(object noteList, int lane, NoteRemovalStats stats)
        {
            var countProp = noteList.GetType().GetProperty("Count");
            if (countProp == null) return;

            int beforeCount = (int)countProp.GetValue(noteList);
            if (beforeCount == 0) return;

            var clearMethod = noteList.GetType().GetMethod("Clear");
            if (clearMethod != null)
            {
                clearMethod.Invoke(noteList, null);

                int afterCount = (int)countProp.GetValue(noteList);
                if (afterCount == 0)
                {
                    stats.TotalRemoved += beforeCount;
                    stats.ClearedLanes++;
                }
                else
                {
                    MelonLogger.Warning($"[SXGTDataHook] 레인 {lane}: Clear 실패 ({beforeCount} -> {afterCount}), RemoveAt으로 강제 제거 시도");
                    ForceRemoveNotesWithRemoveAt(noteList, countProp, beforeCount, stats);
                }
            }
            else
            {
                ForceRemoveNotesWithRemoveAt(noteList, countProp, beforeCount, stats);
            }
        }

        private static void ForceRemoveNotesWithRemoveAt(object noteList, PropertyInfo countProp, int beforeCount, NoteRemovalStats stats)
        {
            var removeAtMethod = noteList.GetType().GetMethod("RemoveAt");
            if (removeAtMethod != null && countProp != null)
            {
                int currentCount = beforeCount;
                while (currentCount > 0)
                {
                    removeAtMethod.Invoke(noteList, new object[] { 0 });
                    currentCount = (int)countProp.GetValue(noteList);
                }
                stats.TotalRemoved += beforeCount;
                stats.ClearedLanes++;
            }
        }

        private static void LogRemovalResults(NoteRemovalStats stats)
        {
            MelonLogger.Msg($"[SXGTDataHook] laneData에서 발견된 노트 타입들 ({stats.FoundNoteTypes.Count}개):");
            foreach (var typeName in stats.FoundNoteTypes)
            {
                MelonLogger.Msg($"  - {typeName}");
            }

            MelonLogger.Msg($"[SXGTDataHook] 원본 노트 제거 완료: {stats.ClearedLanes}개 레인에서 총 {stats.TotalRemoved}개 노트 제거 (홀드 노트: {stats.HoldNoteCount}개)");
            if (_shortNoteType != null && _shortNoteConstructor != null)
            {
                MelonLogger.Msg($"[SXGTDataHook] ShortNote 생성자 찾기 성공: {_shortNoteConstructor.GetParameters().Length}개 파라미터");
            }
            if (_holdNoteType != null)
            {
                if (_holdNoteDurationConstructor != null)
                    MelonLogger.Msg($"[SXGTDataHook] HoldNote duration 생성자 찾기 성공: {_holdNoteDurationConstructor.GetParameters().Length}개 파라미터");
                if (_holdNoteTickTimeConstructor != null)
                    MelonLogger.Msg($"[SXGTDataHook] HoldNote tickTime 생성자 찾기 성공: {_holdNoteTickTimeConstructor.GetParameters().Length}개 파라미터");
                if (_holdNoteBasicConstructor != null)
                    MelonLogger.Msg($"[SXGTDataHook] HoldNote 기본 생성자 찾기 성공: {_holdNoteBasicConstructor.GetParameters().Length}개 파라미터");
            }
        }

    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        public static void ProcessPendingNoteRemovalAndInjection()
        {
            MelonLogger.Msg("[SXGTDataHook] ProcessPendingNoteRemovalAndInjection 호출됨");

            if (!CustomTrackHelper.IsCustomPlayActive())
            {
                MelonLogger.Msg("[SXGTDataHook] 일반 트랙 - 노트 제거/주입 건너뜀");
                return;
            }

            object sxgtDataInstance = _pendingSXGTDataInstance;
            Type sxgtDataType = _pendingSXGTDataType;

            if (sxgtDataInstance == null || sxgtDataType == null)
            {
                MelonLogger.Msg("[SXGTDataHook] 대기 중인 인스턴스가 없어 직접 찾기 시도...");

                sxgtDataInstance = NoteDataFinder.FindNoteData();
                if (sxgtDataInstance != null)
                {
                    sxgtDataType = sxgtDataInstance.GetType();
                    MelonLogger.Msg($"[SXGTDataHook] NoteDataFinder로 SXGTData 인스턴스를 찾았습니다. (타입: {sxgtDataType.Name})");
                }
                else
                {
                    TryFindFromManagerPlay(ref sxgtDataInstance, ref sxgtDataType);
                }

                if (sxgtDataInstance == null)
                {
                    TryFindFromObjects(ref sxgtDataInstance, ref sxgtDataType);
                }

                if (sxgtDataInstance == null || sxgtDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] SXGTData 인스턴스를 찾을 수 없습니다.");
                    return;
                }
            }
            else
            {
                MelonLogger.Msg("[SXGTDataHook] 대기 중인 SXGTData 인스턴스 사용");
            }

            try
            {
                using (ModLog.BeginCorrelation("SXGTNoteInject", sxgtDataType?.Name ?? "sxgt"))
                {
                MelonLogger.Msg("[SXGTDataHook] 노트 제거 및 주입 시작");
                ProcessNoteRemovalAndInjection(sxgtDataInstance, sxgtDataType);

                if (sxgtDataInstance == _pendingSXGTDataInstance)
                {
                    _pendingSXGTDataInstance = null;
                    _pendingSXGTDataType = null;
                }

                MelonLogger.Msg("[SXGTDataHook] 노트 제거 및 주입 완료");
                }
            }
            catch (Exception ex)
            {
                ModLog.Exception("SXGTDataHook.ProcessPendingNoteRemovalAndInjection", ex);
            }
        }

        private static void TryFindFromManagerPlay(ref object sxgtDataInstance, ref Type sxgtDataType)
        {
            try
            {
                var managerPlayType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.ManagerPlay);
                if (managerPlayType == null)
                    return;

                object managerPlayInstance = GameSingletonFinder.TryFindSingleton(ReflectionMemberNames.Types.ManagerPlay);
                if (managerPlayInstance == null)
                {
                    var findObjectsOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                    if (findObjectsOfTypeMethod != null)
                    {
                        var managers = findObjectsOfTypeMethod.Invoke(null, new object[] { managerPlayType }) as UnityEngine.Object[];
                        if (managers != null && managers.Length > 0)
                            managerPlayInstance = managers[0];
                    }
                }

                if (managerPlayInstance == null)
                    return;

                var fields = managerPlayType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    var fieldType = field.FieldType;
                    if (fieldType != null && fieldType.Name == ReflectionMemberNames.Types.SXGTData)
                    {
                        var fieldValue = field.GetValue(managerPlayInstance);
                        if (fieldValue != null)
                        {
                            sxgtDataInstance = fieldValue;
                            sxgtDataType = fieldValue.GetType();
                            MelonLogger.Msg($"[SXGTDataHook] ManagerPlay에서 SXGTData 인스턴스를 찾았습니다. (필드: {field.Name})");
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] ManagerPlay에서 찾기 실패: {ex.Message}");
            }
        }

        private static void TryFindFromObjects(ref object sxgtDataInstance, ref Type sxgtDataType)
        {
            sxgtDataType = TypeFinderHelper.FindType(ReflectionMemberNames.Types.SXGTData);
            if (sxgtDataType == null)
                return;

            try
            {
                var findObjectsOfTypeMethod = typeof(UnityEngine.Object).GetMethod("FindObjectsOfType", new[] { typeof(Type) });
                if (findObjectsOfTypeMethod != null)
                {
                    var objects = findObjectsOfTypeMethod.Invoke(null, new object[] { sxgtDataType }) as UnityEngine.Object[];
                    if (objects != null && objects.Length > 0)
                    {
                        sxgtDataInstance = objects[0];
                        MelonLogger.Msg($"[SXGTDataHook] FindObjectsOfType으로 SXGTData 인스턴스를 찾았습니다. ({objects.Length}개 발견)");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] FindObjectsOfType 호출 실패: {ex.Message}");
            }
        }

        private static void ProcessNoteRemovalAndInjection(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                MelonLogger.Msg("[SXGTDataHook] 원본 노트 제거 및 커스텀 차트 주입 시작");

                var noteDataType = ExtractNoteDataTypeFromLaneData(sxgtDataInstance, sxgtDataType);
                if (noteDataType == null)
                {
                    MelonLogger.Warning("[SXGTDataHook] laneData에서 NoteData 타입을 추출할 수 없습니다.");
                }
                else
                {
                    MelonLogger.Msg($"[SXGTDataHook] NoteData 타입 발견: {noteDataType.FullName}");
                }

                ClearAllNotes(sxgtDataInstance, sxgtDataType);

                if (noteDataType != null)
                {
                    Processors.CustomChartInjector.InjectBmsNotesToLaneData(sxgtDataInstance, sxgtDataType, noteDataType);
                }
                else
                {
                    MelonLogger.Warning("[SXGTDataHook] NoteData 타입을 찾을 수 없어 커스텀 차트 주입을 건너뜁니다.");
                }

            }
            catch (Exception ex)
            {
                ModLog.Exception("SXGTDataHook.ProcessNoteRemovalAndInjection", ex);
            }
        }
    
    }
}
