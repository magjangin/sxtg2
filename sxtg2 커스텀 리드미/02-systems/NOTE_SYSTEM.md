# 🎵 노트 시스템

**노트 생성, 변환 및 주입 시스템**

---

## 노트 타입

```csharp
// ParsedNote (BMS → 중간 형식)
public class ParsedNote
{
    public float Time { get; set; }        // 초 단위
    public int Lane { get; set; }          // 0-9
    public NoteType NoteType { get; set; } // Normal, Long, Open
    public float Length { get; set; }      // 홀드 길이
}

// 게임 노트
RhythmGame.ShortNote  // 일반 노트
RhythmGame.HoldNote   // 홀드 노트
```

---

## 노트 생성

### ShortNote 생성

```csharp
public static object CreateShortNote(ParsedNote parsed)
{
    var type = TypeCache.ShortNoteType;
    var constructor = type.GetConstructor(new[] { typeof(float), typeof(int) });
    
    // 생성
    var note = constructor.Invoke(new object[] { 
        parsed.Time, 
        parsed.Lane 
    });
    
    // 필드 설정
    SetField(note, "nType", GetEnumValue("NoteType", "SHORT"));
    SetField(note, "nColor", GetNoteColor(parsed.Lane));
    SetField(note, "targetLane", parsed.Lane);
    SetField(note, "timing", parsed.Time);
    
    return note;
}
```

### HoldNote 생성

```csharp
public static object CreateHoldNote(ParsedNote parsed)
{
    var type = TypeCache.HoldNoteType;
    
    // 생성자 찾기 (duration 받는 버전)
    var constructor = type.GetConstructor(new[] { 
        typeof(float),  // timing
        typeof(object), // nType
        typeof(object), // nColor
        typeof(int),    // targetLane
        typeof(float)   // duration
    });
    
    // 생성
    var note = constructor.Invoke(new object[] { 
        parsed.Time,
        GetEnumValue("NoteType", "HOLD"),
        GetNoteColor(parsed.Lane),
        parsed.Lane,
        parsed.Length
    });
    
    // tickTime 생성
    var tickTime = GenerateTickTimeArray(parsed.Time, parsed.Length);
    SetField(note, "tickTime", tickTime);
    SetField(note, "tickLength", tickTime.Length);
    SetField(note, "tickJudge", new bool[tickTime.Length]);
    SetField(note, "isFinished", false);
    SetField(note, "isHeadJudged", false);
    SetField(note, "elapsedTick", 0);
    
    return note;
}
```

---

## tickTime 생성

```csharp
public static float[] GenerateTickTimeArray(float timing, float duration)
{
    const float FIRST_TICK_OFFSET = 0.188f;
    const float TICK_INTERVAL = 0.094f;
    
    var tickList = new List<float>();
    var endTime = timing + duration;
    var currentTime = timing + FIRST_TICK_OFFSET;
    
    while (currentTime < endTime)
    {
        tickList.Add(currentTime);
        currentTime += TICK_INTERVAL;
    }
    
    return tickList.ToArray();
}

// 예시
// timing = 91.5, duration = 1.5
// tickTime = [91.688, 91.782, 91.876, ..., 92.813]
```

---

## 노트 색상 규칙

```csharp
public static object GetNoteColor(int lane)
{
    // OPEN/CLOSE (레인 9)
    if (lane == 9)
        return GetEnumValue("NoteColor", "OPEN");
    
    // RED (레인 4, 5)
    if (lane == 4 || lane == 5)
        return GetEnumValue("NoteColor", "RED");
    
    // BLUE (나머지)
    return GetEnumValue("NoteColor", "BLUE");
}
```

---

## 노트 주입

```csharp
public static void InjectBmsNotesToLaneData(object sxgtData)
{
    if (_parsedBmsNotes == null || _parsedBmsNotes.Count == 0)
        return;
    
    var laneDataField = sxgtData.GetType().GetField("laneData");
    var laneData = laneDataField.GetValue(sxgtData);
    
    // 레인별 그룹화
    var notesByLane = _parsedBmsNotes
        .GroupBy(n => n.Lane)
        .ToDictionary(g => g.Key, g => g.OrderBy(n => n.Time).ToList());
    
    // 각 레인에 주입
    foreach (var kvp in notesByLane)
    {
        int lane = kvp.Key;
        var notes = kvp.Value;
        
        var laneList = laneData[lane];
        laneList.Clear();
        
        foreach (var parsed in notes)
        {
            object gameNote;
            
            if (parsed.NoteType == NoteType.Normal)
                gameNote = CreateShortNote(parsed);
            else
                gameNote = CreateHoldNote(parsed);
            
            laneList.Add(gameNote);
        }
        
        MelonLogger.Msg($"레인 {lane}: {notes.Count}개 노트 주입");
    }
}
```

---

## 노트 매칭 (홀드)

```csharp
public static void MatchHoldNotes(List<ParsedNote> notes)
{
    var notesByLane = notes.GroupBy(n => n.Lane);
    
    foreach (var laneGroup in notesByLane)
    {
        var laneNotes = laneGroup.OrderBy(n => n.Time).ToList();
        
        for (int i = 0; i < laneNotes.Count; i++)
        {
            var note = laneNotes[i];
            
            // 홀드 시작 (02)
            if (note.NoteType == NoteType.Long)
            {
                // 다음 HoldEnd (03) 찾기
                for (int j = i + 1; j < laneNotes.Count; j++)
                {
                    var endNote = laneNotes[j];
                    
                    if (endNote.NoteType == NoteType.HoldEnd)
                    {
                        // 길이 계산
                        note.Length = endNote.Time - note.Time;
                        
                        // 끝 노트 제거
                        laneNotes.RemoveAt(j);
                        break;
                    }
                }
            }
            
            // OPEN (04)
            else if (note.NoteType == NoteType.Open)
            {
                // 다음 CLOSE (05) 찾기
                for (int j = i + 1; j < laneNotes.Count; j++)
                {
                    var closeNote = laneNotes[j];
                    
                    if (closeNote.NoteType == NoteType.Close)
                    {
                        note.Length = closeNote.Time - note.Time;
                        laneNotes.RemoveAt(j);
                        break;
                    }
                }
            }
        }
    }
    
    // HoldEnd, Close 노트 제거
    notes.RemoveAll(n => 
        n.NoteType == NoteType.HoldEnd || 
        n.NoteType == NoteType.Close
    );
}
```

---

## 노트 검증

```csharp
public static bool ValidateNote(ParsedNote note)
{
    // 시간 검증
    if (note.Time < 0)
    {
        MelonLogger.Warning($"잘못된 시간: {note.Time}");
        return false;
    }
    
    // 레인 검증
    if (note.Lane < 0 || note.Lane > 9)
    {
        MelonLogger.Warning($"잘못된 레인: {note.Lane}");
        return false;
    }
    
    // 홀드 길이 검증
    if ((note.NoteType == NoteType.Long || note.NoteType == NoteType.Open) 
        && note.Length <= 0)
    {
        MelonLogger.Warning($"잘못된 홀드 길이: {note.Length}");
        return false;
    }
    
    return true;
}
```

---

## 노트 정렬

```csharp
public static void SortNotes(List<ParsedNote> notes)
{
    // 시간 → 레인 순으로 정렬
    notes.Sort((a, b) =>
    {
        int timeCompare = a.Time.CompareTo(b.Time);
        if (timeCompare != 0)
            return timeCompare;
        
        return a.Lane.CompareTo(b.Lane);
    });
}
```

---

## 노트 통계

```csharp
public static void LogNoteStatistics(List<ParsedNote> notes)
{
    var total = notes.Count;
    var normal = notes.Count(n => n.NoteType == NoteType.Normal);
    var hold = notes.Count(n => n.NoteType == NoteType.Long);
    var open = notes.Count(n => n.NoteType == NoteType.Open);
    
    MelonLogger.Msg($"=== 노트 통계 ===");
    MelonLogger.Msg($"총 노트: {total}");
    MelonLogger.Msg($"일반: {normal}");
    MelonLogger.Msg($"홀드: {hold}");
    MelonLogger.Msg($"오픈: {open}");
    
    // 레인별 통계
    var byLane = notes.GroupBy(n => n.Lane);
    foreach (var group in byLane.OrderBy(g => g.Key))
    {
        MelonLogger.Msg($"레인 {group.Key}: {group.Count()}개");
    }
}
```

---

## 노트 디버깅

```csharp
public static void LogNoteDetails(object note)
{
    var type = note.GetType();
    
    MelonLogger.Msg($"=== {type.Name} ===");
    
    // 주요 필드
    var timing = GetField(note, "timing");
    var lane = GetField(note, "targetLane");
    var nType = GetField(note, "nType");
    var nColor = GetField(note, "nColor");
    
    MelonLogger.Msg($"Timing: {timing}");
    MelonLogger.Msg($"Lane: {lane}");
    MelonLogger.Msg($"Type: {nType}");
    MelonLogger.Msg($"Color: {nColor}");
    
    // 홀드 노트 추가 정보
    if (type.Name.Contains("HoldNote"))
    {
        var duration = GetField(note, "duration");
        var tickTime = GetField(note, "tickTime") as float[];
        
        MelonLogger.Msg($"Duration: {duration}");
        MelonLogger.Msg($"TickTime Length: {tickTime?.Length}");
        
        if (tickTime != null && tickTime.Length > 0)
        {
            MelonLogger.Msg($"First Tick: {tickTime[0]}");
            MelonLogger.Msg($"Last Tick: {tickTime[tickTime.Length - 1]}");
        }
    }
}
```

---

## 노트 스킨(커스텀 스프라이트)

위의 내용이 BMS 데이터를 게임 노트 객체로 변환/주입하는 흐름이라면, 이 절은 이미 생성된 노트의
**시각적 스킨**을 교체하는 별도 기능입니다(원래 `InventoryPopup` 모드에서 이식됨, `H:\source\repos\InventoryPopup`).

### 후킹 지점

`RhythmGame.NoteGenerator.Generate(LaneIndex, Note)`의 Postfix에서 반환된 `RG_NoteObject`를 받아
처리합니다. `Assembly-CSharp`를 프로젝트에서 직접 참조하지 않으므로 `object`로 받고 리플렉션으로
`RectTransform` 필드에 접근합니다.

```csharp
// RhythmGame.RG_NoteObject의 실제 필드 (디컴파일 기준)
[SerializeField] private RectTransform shortNote;    // 메인 노트 본체
[SerializeField] private RectTransform holdTexture;   // 홀드 몸통
[SerializeField] private RectTransform tailNote;      // 홀드 끝부분
```

### 처리 순서 (`Hooks/Note/NoteSpriteHook.cs`)

1. `Generate` 반환값(`RG_NoteObject`)에서 `gameObject` 이름(예: `Default_Blue(Clone)`)을 읽어
   노트 타입(`Blue`/`Red`/`Gate`)을 추출한다 (`CustomNoteSpriteLoader.ExtractNoteType`).
2. `shortNote`/`tailNote`/`holdTexture` 필드를 리플렉션으로 가져와 `Image.sprite`를 교체한다.
   - `shortNote`: 폴백 없음(Gate만 Blue로 폴백)
   - `tailNote`/`holdTexture`: `[Type][Suffix]` → `[Suffix][Type]` → `[Suffix]` → 공용 이름 순으로 탐색, 없으면 적용하지 않음
3. `NoteRendererRecovery.RecoverNoteRenderer`로 `Image.SetNativeSize()` + `SetAllDirty()`를 호출해
   스프라이트 교체 직후 UI가 갱신되지 않는 문제를 해소한다.

### 스프라이트 소스

`CustomNoteSpriteLoader`가 `{게임 설치 폴더}\CustomNotes\*.png`를 읽어 `Sprite.Create`로 변환하고
파일명(첫 글자만 대문자로 표준화) 기준으로 캐싱한다. 폴더 규칙은 `01-user-guide/INSTALL_AND_LAYOUT.md`
5절 참고.
