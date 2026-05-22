using System;
using System.Reflection;
using MelonLoader;
using sxtg2.Helpers;

namespace sxtg2.Hooks.SXGT
{
    public static partial class SXGTDataHook
    {
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

    }
}
