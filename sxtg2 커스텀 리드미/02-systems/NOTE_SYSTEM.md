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

---

## 노트 흔들림 연출 (NoteSway, 2026-07-27 추가)

노트가 눈송이처럼 좌우로 흔들리며 내려오는 순수 시각 효과입니다. `SaveCustomKey/config.txt`의
`NoteSway` 항목으로 켭니다(기본 꺼짐). 구현은 `Hooks/GameplayHooks.cs`의 `NoteSwayHook`.

### 왜 루트를 흔드는가

원본 `RG_NoteObject.CalculatePosition(curTime)`은 **x를 항상 0으로 고정**한 채 자식들의 y만 세팅합니다.

```csharp
shortNote.anchoredPosition = new Vector3(0f, num, 0f);
if (Duration != 0f)
{
    holdMask.sizeDelta        = (x, num2 - num);           // 길이 = 꼬리y - 헤드y
    holdMask.anchoredPosition = (0, num + (num2-num)/2);   // 중심
    holdTexture.anchoredPosition = (0, -holdMask.y + 450); // 마스크 이동을 상쇄
    tailNote.anchoredPosition = (0, num2);
}
```

홀드 몸통은 **단일 RectTransform 직사각형**이라 S자로 휘게 만들 수 없습니다. 그래서 자식이 아니라
`RG_NoteObject`의 **루트 RectTransform**을 통째로 미는 방식을 씁니다.

- 헤드·몸통·꼬리가 한 덩어리(뻣뻣한 막대)로 움직입니다. 최신 게임 버전의 연출도 이 형태입니다.
- `holdMask`만 x로 옮기면 그 안의 `holdTexture`가 상대적으로 밀려서 무늬만 반대로 미끄러져 보입니다
  (원본이 y축에 대해 정확히 그 보정을 하고 있음). 루트를 옮기면 마스크와 텍스처가 함께 움직여
  이 문제가 생기지 않습니다.
- 루트의 x는 게임이 건드리지 않습니다. `ForceSetPosition`이 유일하게 루트를 만지는 메서드인데
  현재 빌드에서 **호출하는 곳이 없습니다**. 그래서 최초 관측 시의 x를 기준점(`BaseX`)으로 캡처해도
  안전합니다.

### 흔들림 식

```text
offset = amplitude * sin(curTime * speed * 2π + phase)
```

- `curTime`은 곡 진행 시간(`Time.time`이 아님)이라 일시정지하면 흔들림도 같이 멈춥니다.
- `phase`는 `Mathf.Repeat(Timing * 12.9898f, 2π)` — 노트마다 위상을 흩뿌려 제각각 흔들리게 합니다.
  `Timing` 기반이라 결정론적이고, 리트라이해도 같은 궤적이 나옵니다.
- `NoteSwayDamping`이 켜져 있으면 판정선 도달 `NoteSwayDampingTime`초 전부터 진폭이 0으로 수렴합니다.
  화면 위쪽에서는 나풀거리다가 칠 때는 제자리에 있으므로 정확도에 영향을 주지 않습니다.

### 판정과의 관계

판정은 `RG_PS_Judgement.TryJudgeShortNote`가 `Note.timing`과 시간만 비교하고 화면 위치는 보지
않습니다. 따라서 이 연출은 진폭을 아무리 키워도 **판정에 전혀 영향이 없습니다**.

### 주의

`Lane` 프리팹에 `RectMask2D`가 붙어 있으면 진폭이 클 때 레인 밖으로 나간 노트가 잘립니다.
프리팹 설정이라 코드로는 확인할 수 없으므로, 기본값(12px)에서 시작해 실제 화면을 보며 조정하세요.

상태(`BaseX`/`Phase`)는 노트 인스턴스 ID로 캐싱하며 씬 전환 시 `NoteSwayHook.Reset()`으로 비웁니다.
