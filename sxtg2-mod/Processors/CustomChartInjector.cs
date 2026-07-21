using MelonLoader;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using System;
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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

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
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static object CreateNoteInstance(BmsParser.ParsedNote parsedNote, bool isHoldNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            object noteInstance = null;

            if (isHoldNote)
            {
                noteInstance = CreateHoldNoteInstance(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }
            else
            {
                noteInstance = CreateShortNoteInstance(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }

            if (noteInstance == null)
            {
                noteInstance = CreateNoteInstanceFallback(parsedNote, actualNoteType, nTypeValue, nColorValue);
            }

            return noteInstance;
        }

        private static object CreateHoldNoteInstance(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            if (parsedNote.Length > 0f)
            {
                var tickTimeArray = GenerateTickTimeArray(parsedNote.Time, parsedNote.Length);

                var durationCtor = SXGTDataHook.GetHoldNoteDurationConstructor();
                if (durationCtor != null && nTypeValue != null && nColorValue != null)
                {
                    try
                    {
                        var instance = durationCtor.Invoke(new object[]
                        {
                            parsedNote.Time,
                            nTypeValue,
                            nColorValue,
                            parsedNote.Lane,
                            parsedNote.Length
                        });
                        MelonLogger.Msg("[CustomChartInjector] HoldNote duration 생성자 사용 성공");
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[CustomChartInjector] HoldNote duration 생성자 호출 실패: {ex.Message}");
                    }
                }

                var tickTimeCtor = SXGTDataHook.GetHoldNoteTickTimeConstructor();
                if (tickTimeCtor != null && nTypeValue != null && nColorValue != null)
                {
                    try
                    {
                        var instance = tickTimeCtor.Invoke(new object[]
                        {
                            parsedNote.Time,
                            nTypeValue,
                            nColorValue,
                            parsedNote.Lane,
                            tickTimeArray
                        });
                        MelonLogger.Msg("[CustomChartInjector] HoldNote tickTime 생성자 사용 성공");
                        return instance;
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[CustomChartInjector] HoldNote tickTime 생성자 호출 실패: {ex.Message}");
                    }
                }
            }

            var basicCtor = SXGTDataHook.GetHoldNoteBasicConstructor();
            if (basicCtor != null)
            {
                try
                {
                    var instance = basicCtor.Invoke(new object[] { parsedNote.Time, parsedNote.Lane });

                    SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                    SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                    if (parsedNote.Length > 0f)
                    {
                        SetNoteField(instance, actualNoteType, "duration", parsedNote.Length);
                    }

                    MelonLogger.Msg("[CustomChartInjector] HoldNote 기본 생성자 사용 성공");
                    return instance;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] HoldNote 기본 생성자 호출 실패: {ex.Message}");
                }
            }

            return null;
        }

        private static object CreateShortNoteInstance(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            var shortCtor = SXGTDataHook.GetShortNoteConstructor();
            if (shortCtor != null)
            {
                try
                {
                    var instance = shortCtor.Invoke(new object[] { parsedNote.Time, parsedNote.Lane });

                    SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                    SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                    return instance;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[CustomChartInjector] ShortNote 생성자 호출 실패: {ex.Message}");
                }
            }

            return null;
        }

        private static object CreateNoteInstanceFallback(BmsParser.ParsedNote parsedNote, Type actualNoteType, object nTypeValue, object nColorValue)
        {
            try
            {
                var instance = Activator.CreateInstance(actualNoteType);
                SetNoteField(instance, actualNoteType, "timing", parsedNote.Time);
                SetNoteField(instance, actualNoteType, "targetLane", parsedNote.Lane);
                SetNoteField(instance, actualNoteType, "nType", nTypeValue);
                SetNoteField(instance, actualNoteType, "nColor", nColorValue);

                if (parsedNote.Length > 0f)
                {
                    SetNoteField(instance, actualNoteType, "duration", parsedNote.Length);
                }

                MelonLogger.Warning("[CustomChartInjector] 저장된 생성자를 찾을 수 없어 기본 생성자 사용");
                return instance;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 노트 생성 실패: {ex.Message}");
                return null;
            }
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static readonly Dictionary<System.Reflection.Assembly, (Type noteTypeEnum, Type noteColorEnum)> _enumTypeCache =
            new Dictionary<System.Reflection.Assembly, (Type, Type)>();
        private static object _cachedValShort;
        private static object _cachedValHold;
        private static object _cachedValOpen;
        private static object _cachedValRed;
        private static object _cachedValBlue;

        private static void FindNoteEnums(BmsParser.ParsedNote parsedNote, Type actualNoteType, out object nTypeValue, out object nColorValue)
        {
            nTypeValue = null;
            nColorValue = null;

            var assembly = actualNoteType.Assembly;
            if (!_enumTypeCache.TryGetValue(assembly, out var enumTypes))
            {
                enumTypes = CacheEnumsForAssembly(assembly);
                _enumTypeCache[assembly] = enumTypes;
            }

            if (enumTypes.noteTypeEnum != null)
            {
                if (parsedNote.NoteType == BmsParser.NoteType.Normal)
                {
                    nTypeValue = _cachedValShort;
                }
                else if (parsedNote.NoteType == BmsParser.NoteType.Long ||
                         parsedNote.NoteType == BmsParser.NoteType.Open)
                {
                    nTypeValue = _cachedValHold;
                }
            }

            if (enumTypes.noteColorEnum != null)
            {
                if (parsedNote.NoteType == BmsParser.NoteType.Open ||
                    parsedNote.NoteType == BmsParser.NoteType.Close)
                {
                    nColorValue = _cachedValOpen;
                }
                else if (parsedNote.Lane == 4 || parsedNote.Lane == 5)
                {
                    nColorValue = _cachedValRed;
                }
                else
                {
                    nColorValue = _cachedValBlue;
                }
            }
        }

        private static object TryParseEnumMemberIgnoreCase(Type enumType, string name)
        {
            if (enumType == null || !enumType.IsEnum || string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (var n in Enum.GetNames(enumType))
            {
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Enum.Parse(enumType, n);
                }
            }

            return null;
        }

        private static (Type noteTypeEnum, Type noteColorEnum) CacheEnumsForAssembly(System.Reflection.Assembly assembly)
        {
            try
            {
                Type noteTypeEnum = null;
                Type noteColorEnum = null;

                foreach (var type in assembly.GetTypes())
                {
                    if (!type.IsEnum)
                    {
                        continue;
                    }

                    if (noteTypeEnum == null && (type.Name.Contains("NoteType") || type.Name == "nType"))
                    {
                        noteTypeEnum = type;
                    }
                    else if (noteColorEnum == null && (type.Name.Contains("NoteColor") || type.Name == "nColor"))
                    {
                        noteColorEnum = type;
                    }

                    if (noteTypeEnum != null && noteColorEnum != null)
                    {
                        break;
                    }
                }

                if (_cachedValShort == null && noteTypeEnum != null)
                {
                    _cachedValShort = TryParseEnumMemberIgnoreCase(noteTypeEnum, "SHORT");
                    _cachedValHold = TryParseEnumMemberIgnoreCase(noteTypeEnum, "HOLD");
                }

                if (_cachedValOpen == null && noteColorEnum != null)
                {
                    _cachedValOpen = TryParseEnumMemberIgnoreCase(noteColorEnum, "OPEN");
                    _cachedValRed = TryParseEnumMemberIgnoreCase(noteColorEnum, "RED");
                    _cachedValBlue = TryParseEnumMemberIgnoreCase(noteColorEnum, "BLUE");
                }

                MelonLogger.Msg($"[CustomChartInjector] Enum 타입 캐싱 완료 (Assembly: {assembly.GetName().Name})");
                return (noteTypeEnum, noteColorEnum);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartInjector] Enum 캐싱 실패: {ex.Message}");
                return (null, null);
            }
        }
    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        /// <summary>
        /// 게임 노트 인스턴스를 생성합니다.
        /// </summary>
        private static object CreateGameNote(BmsParser.ParsedNote parsedNote, Type noteDataType)
        {
            try
            {
                // 노트 타입 결정 (홀드 노트인지 확인 및 실제 타입 선택)
                bool isHoldNote;
                Type actualNoteType;
                DetermineNoteType(parsedNote, noteDataType, out isHoldNote, out actualNoteType);

                // Enum 값 찾기 및 설정
                object nTypeValue, nColorValue;
                FindNoteEnums(parsedNote, actualNoteType, out nTypeValue, out nColorValue);

                // 생성자 호출하여 노트 인스턴스 생성
                object noteInstance = CreateNoteInstance(parsedNote, isHoldNote, actualNoteType, nTypeValue, nColorValue);

                // 필드 설정
                SetNoteFields(noteInstance, parsedNote, actualNoteType, nTypeValue, nColorValue);

                return noteInstance;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 게임 노트 생성 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 노트 타입을 결정합니다 (홀드 노트인지 확인 및 실제 타입 선택).
        /// </summary>
        private static void DetermineNoteType(BmsParser.ParsedNote parsedNote, Type noteDataType, out bool isHoldNote, out Type actualNoteType)
        {
            // 홀드 노트인지 확인
            isHoldNote = parsedNote.NoteType == BmsParser.NoteType.Long || 
                         parsedNote.NoteType == BmsParser.NoteType.Open;

            // 노트 타입에 따라 적절한 타입 선택
            actualNoteType = noteDataType;
            if (isHoldNote)
            {
                var holdNoteType = SXGTDataHook.GetHoldNoteType();
                if (holdNoteType != null)
                {
                    actualNoteType = holdNoteType;
                    MelonLogger.Msg($"[CustomChartInjector] HoldNote 타입 사용: {holdNoteType.FullName}");
                }
            }
            else
            {
                var shortNoteType = SXGTDataHook.GetShortNoteType();
                if (shortNoteType != null)
                {
                    actualNoteType = shortNoteType;
                }
            }
        }

    
    }
}
