using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using sxtg2.Helpers;
using sxtg2.Hooks.SXGT;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static partial class CustomChartInjector
    {
        private static List<BmsParser.ParsedNote> _parsedBmsNotes;
        private const float TICK_INTERVAL = 0.094f; // 로그 기준 틱 간격
        private const float FIRST_TICK_OFFSET = 0.188f; // 첫 틱 시작 오프셋 (timing + 0.188초)
        private static readonly Dictionary<Type, (MethodInfo clear, MethodInfo add, Type elementType)> _noteListMethodCache = new Dictionary<Type, (MethodInfo clear, MethodInfo add, Type elementType)>();

        // 주입된 노트 기준으로 다시 센 개수. SXGTReader가 도너 트랙 기준으로 채워둔
        // totalNotes/totalNoteWithTicks는 laneData만 갈아끼워서는 갱신되지 않으므로,
        // 주입 직후 이 값으로 SXGTData 필드를 덮어써야 클리어 판정(스코어/곡 종료)이 실제 커스텀 차트와 맞는다.
        private static int _lastInjectedTotalNotes;
        private static int _lastInjectedTotalNoteWithTicks;

        public static void SetParsedBmsNotes(List<BmsParser.ParsedNote> notes)
        {
            _parsedBmsNotes = notes;
        }

        public static void InjectBmsNotesToLaneData(object sxgtDataInstance, Type sxgtDataType, Type noteDataType)
        {
            try
            {
                if (_parsedBmsNotes == null || _parsedBmsNotes.Count == 0)
                {
                    MelonLoader.MelonLogger.Warning("[CustomChartInjector] 파싱된 노트가 없습니다.");
                    return;
                }

                if (sxgtDataInstance == null)
                {
                    MelonLoader.MelonLogger.Warning("[CustomChartInjector] SXGTData 인스턴스가 null입니다.");
                    return;
                }

                if (sxgtDataType == null)
                {
                    MelonLoader.MelonLogger.Warning("[CustomChartInjector] SXGTData 타입이 null입니다.");
                    return;
                }

                // laneData에서 추출한 실제 노트 타입 사용
                var actualNoteType = Hooks.SXGT.SXGTDataHook.GetActualNoteType();
                if (actualNoteType != null)
                {
                    noteDataType = actualNoteType;
                    MelonLoader.MelonLogger.Msg($"[CustomChartInjector] 실제 노트 타입 사용: {actualNoteType.FullName}");
                }
                else if (noteDataType == null)
                {
                    MelonLoader.MelonLogger.Warning("[CustomChartInjector] NoteData 타입이 null입니다.");
                    return;
                }

                if (!GameLaneDataHelper.TryGetLaneDataAndItemProperty(sxgtDataType, sxgtDataInstance, out var laneData, out var itemProp, out var laneFail))
                {
                    switch (laneFail)
                    {
                        case LaneDataAccessFailure.NoLaneField:
                            MelonLoader.MelonLogger.Warning("[CustomChartInjector] laneData 필드를 찾을 수 없습니다.");
                            break;
                        case LaneDataAccessFailure.LaneDataNull:
                            MelonLoader.MelonLogger.Warning("[CustomChartInjector] laneData가 null입니다.");
                            break;
                        case LaneDataAccessFailure.NoItemProperty:
                            MelonLoader.MelonLogger.Warning("[CustomChartInjector] laneData.Item 속성을 찾을 수 없습니다.");
                            break;
                        default:
                            MelonLoader.MelonLogger.Warning("[CustomChartInjector] laneData 접근 실패.");
                            break;
                    }

                    return;
                }

                using (ModLog.BeginCorrelation("BmsLaneInject", $"{_parsedBmsNotes.Count}"))
                {
                // 레인별로 노트 그룹화
                var notesByLane = new Dictionary<int, List<BmsParser.ParsedNote>>();
                foreach (var note in _parsedBmsNotes)
                {
                    if (!notesByLane.ContainsKey(note.Lane))
                    {
                        notesByLane[note.Lane] = new List<BmsParser.ParsedNote>();
                    }
                    notesByLane[note.Lane].Add(note);
                }

                _lastInjectedTotalNotes = 0;
                _lastInjectedTotalNoteWithTicks = 0;

                // 각 레인에 노트 주입
                foreach (var laneGroup in notesByLane)
                {
                    var lane = laneGroup.Key;
                    var notes = laneGroup.Value;

                    // 시간 순 정렬
                    notes.Sort((a, b) => a.Time.CompareTo(b.Time));

                    // 기존 노트 제거
                    var noteList = itemProp.GetValue(laneData, new object[] { lane });
                    if (noteList != null)
                    {
                        InjectNotesIntoLane(noteList, notes, noteDataType, lane);
                    }
                }

                ApplyNoteCountsToSxgtData(sxgtDataInstance, sxgtDataType);

                MelonLoader.MelonLogger.Msg($"[CustomChartInjector] {_parsedBmsNotes.Count}개의 노트 주입 완료");
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[CustomChartInjector] 노트 주입 실패: {ex.Message}");
                MelonLoader.MelonLogger.Error(ex.StackTrace);
            }
        }

        private static void InjectNotesIntoLane(object noteList, List<BmsParser.ParsedNote> notes, Type noteDataType, int lane)
        {
            var listMeta = GetNoteListMeta(noteList.GetType());

            if (noteList is IList list)
            {
                list.Clear();
                AddNotesWithIList(list, notes, noteDataType, lane, listMeta.elementType);
                return;
            }

            if (listMeta.clear != null)
            {
                listMeta.clear.Invoke(noteList, null);
            }

            AddNotesWithReflection(noteList, notes, noteDataType, lane, listMeta);
        }

        private static void AddNotesWithIList(IList list, List<BmsParser.ParsedNote> notes, Type noteDataType, int lane, Type elementType)
        {
            foreach (var parsedNote in notes)
            {
                if (IsLengthMarkerNote(parsedNote))
                {
                    continue;
                }

                var gameNote = CreateGameNote(parsedNote, noteDataType);
                if (gameNote == null)
                {
                    continue;
                }

                try
                {
                    list.Add(gameNote);
                    AccumulateNoteCounts(lane, parsedNote, gameNote);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] 레인 {lane}: IList Add 실패 - 생성된 타입: {gameNote.GetType().FullName}, 요구 타입: {elementType?.FullName ?? "null"}, 오류: {ex.Message}");
                }
            }
        }

        private static void AddNotesWithReflection(
            object noteList,
            List<BmsParser.ParsedNote> notes,
            Type noteDataType,
            int lane,
            (MethodInfo clear, MethodInfo add, Type elementType) listMeta)
        {
            foreach (var parsedNote in notes)
            {
                if (IsLengthMarkerNote(parsedNote))
                {
                    continue;
                }

                var gameNote = CreateGameNote(parsedNote, noteDataType);
                if (gameNote == null || listMeta.add == null)
                {
                    continue;
                }

                try
                {
                    listMeta.add.Invoke(noteList, new object[] { gameNote });
                    AccumulateNoteCounts(lane, parsedNote, gameNote);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] 레인 {lane}: 노트 추가 실패 - 생성된 타입: {gameNote.GetType().FullName}, 요구 타입: {listMeta.elementType?.FullName ?? "null"}, 오류: {ex.Message}");
                }
            }
        }

        private static bool IsLengthMarkerNote(BmsParser.ParsedNote note)
        {
            return note.NoteType == BmsParser.NoteType.HoldEnd ||
                   note.NoteType == BmsParser.NoteType.Close;
        }


        private static float[] GenerateTickTimeArray(float timing, float duration)
        {
            var tickTimes = new List<float>();
            
            // 첫 틱은 timing + FIRST_TICK_OFFSET에서 시작
            float currentTime = timing + FIRST_TICK_OFFSET;
            float endTime = timing + duration;

            // duration 동안 TICK_INTERVAL 간격으로 틱 생성
            while (currentTime < endTime)
            {
                tickTimes.Add(currentTime);
                currentTime += TICK_INTERVAL;
            }

            return tickTimes.ToArray();
        }

        private static (MethodInfo clear, MethodInfo add, Type elementType) GetNoteListMeta(Type noteListType)
        {
            if (_noteListMethodCache.TryGetValue(noteListType, out var cached))
            {
                return cached;
            }

            var clearMethod = noteListType.GetMethod("Clear");
            var addMethod = noteListType.GetMethod("Add");

            Type elementType = null;
            if (noteListType.IsGenericType)
            {
                var genericArgs = noteListType.GetGenericArguments();
                if (genericArgs.Length > 0)
                {
                    elementType = genericArgs[0];
                    MelonLoader.MelonLogger.Msg($"[CustomChartInjector] noteList 타입: {noteListType.FullName}, 요소 타입: {elementType.FullName}");
                }
            }

            var meta = (clear: clearMethod, add: addMethod, elementType: elementType);
            _noteListMethodCache[noteListType] = meta;
            return meta;
        }

        /// <summary>
        /// 실제로 주입에 성공한 노트를 기준으로 totalNotes/totalNoteWithTicks 카운터를 누적합니다.
        /// 게임 원본(SXGTReader.ReadBMSFile)은 laneData의 9,10번 레인(액션/오픈 게이트)을 집계에서 제외하고,
        /// 홀드 노트는 tickLength만큼을 추가로 더합니다 - 여기서도 동일한 규칙을 따릅니다.
        /// </summary>
        private static void AccumulateNoteCounts(int lane, BmsParser.ParsedNote parsedNote, object gameNote)
        {
            if (lane == 9 || lane == 10)
            {
                return;
            }

            _lastInjectedTotalNotes++;
            _lastInjectedTotalNoteWithTicks++;

            // 원본 게임은 홀드 노트(BLUE/RED 색상)에 한해 tickLength를 추가로 더한다.
            // BmsParser에서는 Open 타입만 게이트(레인 9) 색상으로 빠지므로, Long 타입만 여기 해당한다.
            if (parsedNote.NoteType != BmsParser.NoteType.Long || gameNote == null)
            {
                return;
            }

            var tickLengthField = gameNote.GetType().GetField(
                ReflectionMemberNames.HoldNoteMembers.TickLength,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (tickLengthField?.GetValue(gameNote) is int tickLength && tickLength > 0)
            {
                _lastInjectedTotalNoteWithTicks += tickLength;
            }
        }

        /// <summary>
        /// 주입 완료 후 다시 센 노트 개수를 SXGTData 인스턴스에 반영합니다.
        /// 이 값을 갱신하지 않으면 곡 종료/클리어 판정이 도너 트랙의 노트 개수를 기준으로 동작합니다.
        /// </summary>
        private static void ApplyNoteCountsToSxgtData(object sxgtDataInstance, Type sxgtDataType)
        {
            try
            {
                bool setNotes = SetIntField(sxgtDataInstance, sxgtDataType,
                    ReflectionMemberNames.SXGTDataMembers.TotalNotes, _lastInjectedTotalNotes);
                bool setTicks = SetIntField(sxgtDataInstance, sxgtDataType,
                    ReflectionMemberNames.SXGTDataMembers.TotalNoteWithTicks, _lastInjectedTotalNoteWithTicks);

                if (setNotes && setTicks)
                {
                    MelonLogger.Msg($"[CustomChartInjector] 노트 개수 재계산 완료: totalNotes={_lastInjectedTotalNotes}, totalNoteWithTicks={_lastInjectedTotalNoteWithTicks}");
                }
                else
                {
                    MelonLogger.Warning("[CustomChartInjector] totalNotes/totalNoteWithTicks 필드를 찾지 못해 재계산 값을 반영하지 못했습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartInjector] 노트 개수 재계산 반영 실패: {ex.Message}");
            }
        }

        private static bool SetIntField(object instance, Type type, string fieldName, int value)
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return false;
            }

            field.SetValue(instance, value);
            return true;
        }
    }
}
