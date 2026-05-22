# 🔍 파싱 알고리즘

**BMS 파일 파싱 알고리즘 상세**

---

## 전체 파싱 흐름

```csharp
public static List<ParsedNote> ParseBmsFile(string filePath)
{
    // 1. 초기화
    var notes = new List<ParsedNote>();
    var bpmDict = new Dictionary<string, float>();
    var dataList = new List<BpmData>();
    
    // 2. 파일 읽기
    var lines = File.ReadAllLines(filePath, Encoding.UTF8);
    
    // 3. 라인별 파싱
    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || !trimmed.StartsWith("#"))
            continue;
        
        if (trimmed.Contains(" "))
            ParseHeaderLine(trimmed, bpmDict, dataList);
        else if (trimmed.Contains(":"))
            ParseDataLine(trimmed, notes, dataList, bpmDict);
    }
    
    // 4. 후처리
    notes = notes.OrderBy(n => n.Time).ToList();
    CalculateHoldNoteLengths(notes);
    notes.RemoveAll(n => n.NoteType == NoteType.HoldEnd || n.NoteType == NoteType.Close);
    
    return notes;
}
```

---

## 헤더 파싱

```csharp
private static void ParseHeaderLine(string line, 
    Dictionary<string, float> bpmDict, 
    List<BpmData> dataList)
{
    // "#KEY VALUE" 형식
    var parts = line.Substring(1).Split(new[] { ' ' }, 2);
    if (parts.Length < 2) return;
    
    var key = parts[0].ToUpper();
    var value = parts[1];
    
    if (key.StartsWith("BPM"))
    {
        if (key.Length > 3) // #BPMXX
        {
            var index = key.Substring(3, 2);
            var bpm = float.Parse(value);
            bpmDict[index] = bpm;
        }
        else // #BPM
        {
            var bpm = float.Parse(value);
            bpmDict["00"] = bpm;
            
            dataList.Add(new BpmData 
            { 
                Tick = 0, 
                Freq = 60f / bpm 
            });
        }
    }
}
```

---

## 데이터 라인 파싱

```csharp
private static void ParseDataLine(string line, 
    List<ParsedNote> notes, 
    List<BpmData> dataList,
    Dictionary<string, float> bpmDict)
{
    // "#MMMCC:데이터" 형식
    var colonIndex = line.IndexOf(':');
    var header = line.Substring(1, colonIndex - 1);
    var data = line.Substring(colonIndex + 1);
    
    // Measure와 Channel 추출
    int measure = 0;
    int channel = 0;
    
    if (header.Length >= 5)
    {
        // #MMMCC 형식
        var measureStr = header.Substring(0, header.Length - 2);
        var channelStr = header.Substring(header.Length - 2);
        
        int.TryParse(measureStr, out measure);
        int.TryParse(channelStr, out channel);
    }
    else if (header.Length >= 2)
    {
        // #CC 형식 (Measure 생략)
        var channelStr = header.Substring(header.Length - 2);
        int.TryParse(channelStr, out channel);
    }
    
    // 채널 필터링
    if (!IsValidChannel(channel))
        return;
    
    // 노트 데이터 파싱
    ParseNoteData(measure, channel, data, notes, dataList);
}
```

---

## 노트 데이터 파싱

```csharp
private static void ParseNoteData(int measure, int channel, string data,
    List<ParsedNote> notes, List<BpmData> dataList)
{
    var objLength = data.Length / 2; // 총 슬롯 수
    
    for (int i = 0; i < data.Length; i += 2)
    {
        var noteValue = data.Substring(i, 2);
        if (noteValue == "00") continue;
        
        // Tick 계산
        var slotIndex = i / 2;
        var tick = (float)measure + ((float)slotIndex / objLength);
        
        // 시간 계산
        var time = CalculateTime(tick, dataList);
        
        // 레인 매핑
        int lane;
        if (noteValue == "04" || noteValue == "05")
            lane = 9; // OPEN/CLOSE
        else if (LaneMapping.ContainsKey(channel))
            lane = LaneMapping[channel];
        else
            continue;
        
        // 노트 타입 결정
        var noteType = GetNoteType(noteValue);
        
        // ParsedNote 생성
        notes.Add(new ParsedNote
        {
            Time = time,
            Lane = lane,
            NoteType = noteType,
            OriginalNoteValue = noteValue
        });
    }
}
```

---

## 시간 계산 알고리즘

```csharp
private static float CalculateTime(float tick, List<BpmData> dataList)
{
    // BPM 데이터 없음
    if (dataList.Count == 0)
        return tick * 0.4f;
    
    // 현재 tick보다 작은 BPM 변화들
    var relevantBpms = dataList.Where(d => d.Tick < tick).ToList();
    
    // BPM 변화 없음
    if (relevantBpms.Count == 0)
    {
        var firstBpm = dataList[0];
        return tick * 4f * firstBpm.Freq;
    }
    
    // BPM 변화 있음 - 구간별 계산
    float time = 0f;
    
    for (int i = relevantBpms.Count - 1; i >= 0; i--)
    {
        var currentBpm = relevantBpms[i];
        float offset;
        
        if (i == 0)
        {
            // 마지막 구간: 현재 tick까지
            offset = tick - currentBpm.Tick;
        }
        else
        {
            // 중간 구간: 다음 BPM 변화까지
            var prevBpm = relevantBpms[i - 1];
            offset = prevBpm.Tick - currentBpm.Tick;
        }
        
        // 시간 누적 (1 measure = 4 beats)
        time += offset * 4f * currentBpm.Freq;
    }
    
    return time;
}
```

**예시:**
```
BPM 변화:
- Tick 0.0: 120 BPM (Freq = 0.5)
- Tick 2.0: 180 BPM (Freq = 0.333)

현재 Tick: 3.5

계산:
1. Tick 2.0~3.5 (180 BPM)
   time = (3.5 - 2.0) × 4 × 0.333 = 2.0초

2. Tick 0.0~2.0 (120 BPM)
   time += (2.0 - 0.0) × 4 × 0.5 = 4.0초

총 시간: 6.0초
```

---

## 홀드 노트 길이 계산

```csharp
private static void CalculateHoldNoteLengths(List<ParsedNote> notes)
{
    // 레인별 그룹화
    var notesByLane = notes.GroupBy(n => n.Lane);
    
    foreach (var laneGroup in notesByLane)
    {
        var laneNotes = laneGroup.OrderBy(n => n.Time).ToList();
        
        for (int i = 0; i < laneNotes.Count; i++)
        {
            var note = laneNotes[i];
            
            // 일반 홀드 (02-03)
            if (note.NoteType == NoteType.Long)
            {
                var endNote = FindNextNote(laneNotes, i, NoteType.HoldEnd);
                if (endNote != null)
                {
                    note.Length = endNote.Time - note.Time;
                }
                else
                {
                    MelonLogger.Warning($"홀드 끝을 찾을 수 없음: Lane {note.Lane}, Time {note.Time}");
                }
            }
            
            // 오픈/클로즈 (04-05)
            else if (note.NoteType == NoteType.Open)
            {
                var closeNote = FindNextNote(laneNotes, i, NoteType.Close);
                if (closeNote != null)
                {
                    note.Length = closeNote.Time - note.Time;
                }
                else
                {
                    MelonLogger.Warning($"클로즈를 찾을 수 없음: Lane {note.Lane}, Time {note.Time}");
                }
            }
        }
    }
}

private static ParsedNote FindNextNote(List<ParsedNote> notes, int startIndex, NoteType targetType)
{
    for (int i = startIndex + 1; i < notes.Count; i++)
    {
        if (notes[i].NoteType == targetType)
        {
            return notes[i];
        }
    }
    return null;
}
```

---

## 채널 검증

```csharp
private static bool IsValidChannel(int channel)
{
    // 노트 채널
    if (LaneMapping.ContainsKey(channel))
        return true;
    
    // OPEN/CLOSE 채널
    if (channel == 4 || channel == 5)
        return true;
    
    return false;
}

private static readonly Dictionary<int, int> LaneMapping = new Dictionary<int, int>
{
    { 16, 0 },
    { 11, 1 },
    { 12, 2 },
    { 13, 3 },
    { 14, 4 },
    { 15, 5 },
    { 18, 6 }
};
```

---

## 노트 타입 결정

```csharp
private static NoteType GetNoteType(string noteValue)
{
    if (NoteTypeMapping.ContainsKey(noteValue))
        return NoteTypeMapping[noteValue];
    
    return NoteType.Normal; // 기본값
}

private static readonly Dictionary<string, NoteType> NoteTypeMapping = new Dictionary<string, NoteType>
{
    { "01", NoteType.Normal },
    { "02", NoteType.Long },
    { "03", NoteType.HoldEnd },
    { "04", NoteType.Open },
    { "05", NoteType.Close }
};
```

---

## 최적화 기법

### 1. 문자열 처리 최적화

```csharp
// 나쁜 예
for (int i = 0; i < data.Length; i += 2)
{
    var noteValue = data.Substring(i, 2); // 매번 새 문자열 생성
}

// 좋은 예
var span = data.AsSpan();
for (int i = 0; i < span.Length; i += 2)
{
    var noteValue = span.Slice(i, 2).ToString(); // 필요할 때만 생성
}
```

### 2. 딕셔너리 캐싱

```csharp
// 정적 딕셔너리 사용
private static readonly Dictionary<int, int> LaneMapping = ...;
private static readonly Dictionary<string, NoteType> NoteTypeMapping = ...;
```

### 3. LINQ 최소화

```csharp
// 나쁜 예
var relevantBpms = dataList.Where(d => d.Tick < tick).OrderBy(d => d.Tick).ToList();

// 좋은 예 (이미 정렬되어 있다면)
var relevantBpms = new List<BpmData>();
foreach (var bpm in dataList)
{
    if (bpm.Tick >= tick) break;
    relevantBpms.Add(bpm);
}
```

---

## 에러 처리

```csharp
public static List<ParsedNote> ParseBmsFileSafe(string filePath)
{
    try
    {
        return ParseBmsFile(filePath);
    }
    catch (FileNotFoundException)
    {
        MelonLogger.Error($"파일을 찾을 수 없음: {filePath}");
        return new List<ParsedNote>();
    }
    catch (FormatException ex)
    {
        MelonLogger.Error($"파싱 오류: {ex.Message}");
        return new List<ParsedNote>();
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"알 수 없는 오류: {ex.Message}");
        MelonLogger.Error(ex.StackTrace);
        return new List<ParsedNote>();
    }
}
```

---

## 통계 및 검증

```csharp
private static void ValidateAndLogStatistics(List<ParsedNote> notes)
{
    MelonLogger.Msg($"=== 파싱 완료 ===");
    MelonLogger.Msg($"총 노트: {notes.Count}");
    
    var byType = notes.GroupBy(n => n.NoteType);
    foreach (var group in byType)
    {
        MelonLogger.Msg($"{group.Key}: {group.Count()}개");
    }
    
    var byLane = notes.GroupBy(n => n.Lane);
    foreach (var group in byLane.OrderBy(g => g.Key))
    {
        MelonLogger.Msg($"레인 {group.Key}: {group.Count()}개");
    }
    
    // 검증
    var invalidNotes = notes.Where(n => n.Time < 0 || n.Lane < 0 || n.Lane > 9).ToList();
    if (invalidNotes.Any())
    {
        MelonLogger.Warning($"잘못된 노트 {invalidNotes.Count}개 발견");
    }
}
