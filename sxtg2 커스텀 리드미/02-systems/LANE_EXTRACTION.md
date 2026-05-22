# 레인별 추출(laneData) 상세 - sxtg2 기준

이 문서는 `SXGTData.laneData`에서 **레인(0~9)별로 노트를 추출/분석/제거**하는 로직을 "코드 그대로" 이해할 수 있게 정리한 문서입니다.

---

## 목차
1. [laneData는 무엇인가?](#1-lanedata는-무엇인가)
2. [레인별 접근 - 공통 리플렉션 패턴](#2-레인별-접근---공통-리플렉션-패턴)
3. [레인별 "추출" (읽기/분석)](#3-레인별-추출-읽기분석---notedataextractor)
4. [레인별 "타입 추출" (생성자 캐시)](#4-레인별-타입-추출-실제-타입생성자-캐시---sxgtdatahook)
5. [레인별 "제거" (Clear/RemoveAt)](#5-레인별-제거-clearremoveat---sxgtdatahookclearallnotes)
6. [레인 범위는 왜 0~9인가?](#6-레인-범위는-왜-09인가)
7. [디버깅 체크리스트](#7-디버깅-체크리스트)
8. [성능 최적화 팁](#8-성능-최적화-팁)
9. [요약](#9-요약)

---

## 1) laneData는 무엇인가?

### 구조 설명

`SXGTData` 클래스 내부에 있는 `laneData` 필드는 게임의 모든 노트를 **레인별로 분류하여 저장**하는 핵심 데이터 구조입니다.

**타입**: `Dictionary<int, List<NoteType>>`
- **키(int)**: 레인 번호 (0~9, 총 10개 레인)
- **값(List)**: 해당 레인에 배치된 모든 노트 객체의 리스트

### 시각적 표현

```
laneData
├─ [0] → List<Note> { ShortNote(100ms), HoldNote(500ms), ... }
├─ [1] → List<Note> { ShortNote(200ms), ... }
├─ [2] → List<Note> { ... }
├─ ...
└─ [9] → List<Note> { ... }
```

### 중요 포인트

1. **타입 다형성**
   - 리스트의 선언 타입은 base 타입(`Note`)이지만
   - 실제 인스턴스는 `ShortNote`, `HoldNote` 등 **파생 타입**
   - 따라서 런타임에 `note.GetType()`으로 실제 타입을 확인해야 함

2. **레인 번호 의미**
   - 0~9: 게임의 물리적 레인 위치
   - 각 레인은 독립적인 노트 시퀀스를 가짐
   - BMS에서 `#xxx1y`의 `y` 값이 레인 번호에 대응

3. **시간 순서 보장**
   - 각 레인 내의 노트는 일반적으로 `timing` 기준 오름차순 정렬
   - 하지만 보장되지 않을 수 있으므로 필요시 명시적 정렬 필요

---

## 2) 레인별 접근 - 공통 리플렉션 패턴

### 왜 리플렉션이 필요한가?

게임 코드는 난독화되어 있어 타입명/필드명이 정확하지 않습니다. 따라서 **런타임 리플렉션**으로 동적 접근이 필수입니다.

### 공통 접근 패턴 (단계별 분석)

```csharp
// Step 1: laneData 필드 획득
FieldInfo laneDataField = sxgtDataType.GetField("laneData", 
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

// Step 2: 실제 laneData 객체 가져오기 (Dictionary<int, List<Note>>)
object laneData = laneDataField.GetValue(sxgtDataInstance);

// Step 3: Dictionary의 indexer 프로퍼티 찾기 (Item[int key])
PropertyInfo itemProp = laneData.GetType().GetProperty("Item");

// Step 4: 특정 레인의 노트 리스트 가져오기
object noteList = itemProp.GetValue(laneData, new object[] { laneNumber });
```

### 단계별 설명

| 단계 | 목적 | 주의사항 |
|------|------|----------|
| 1 | `laneData` 필드 메타정보 획득 | 필드명이 난독화될 수 있음 |
| 2 | 인스턴스에서 실제 Dictionary 객체 추출 | `null` 체크 필수 |
| 3 | Dictionary의 `[]` 연산자 접근 | `Item` 프로퍼티는 C# indexer의 내부 구현 |
| 4 | 레인 번호로 List 조회 | 해당 레인이 없으면 예외 발생 가능 |

### 사용 위치

이 패턴은 프로젝트 전반에서 반복 사용됩니다:

| 파일 | 메서드 | 용도 |
|------|--------|------|
| `Helpers/Finders/NoteDataFinder.cs` | `GetLaneNotes(...)` | 특정 레인의 노트 목록 조회 |
| `NoteDataExtractor.cs` | `ExtractNoteData(...)` | 전체 레인 순회하며 노트 정보 추출 |
| `SXGTDataHook.cs` | `ClearAllNotes(...)` | 전체 레인 순회하며 노트 제거 + 타입 캐시 |

### 왜 직접 접근하지 않나?

```csharp
// ❌ 불가능 (난독화된 타입명/컴파일 타임에 알 수 없음)
var laneData = sxgtData.laneData;
var notes = laneData[0];

// ✅ 가능 (런타임 리플렉션)
var noteList = itemProp.GetValue(laneData, new object[] { 0 });
```

---

## 3) 레인별 "추출" (읽기/분석) - NoteDataExtractor

**파일**: [`sxtg2-mod/Processors/NoteDataExtractor.cs`](../sxtg2-mod/Processors/NoteDataExtractor.cs)

### 목적

원본 `laneData`를 **수정하지 않고**, 모든 레인의 노트를 순회하며 필요한 정보만 추출하여 분석용 데이터로 변환합니다.

### 추출 대상 정보

각 노트에서 다음 필드를 추출합니다:

| 필드명 | 타입 | 의미 |
|--------|------|------|
| `timing` | float/double | 노트 타이밍 (밀리초) |
| `nType` | int/enum | 노트 타입 (Short=0, Hold=1 등) |
| `nColor` | int/enum | 노트 색상/레인 정보 |

### 동작 흐름 (실제 코드 기반)

```csharp
// 실제 파일: sxtg2-mod/Processors/NoteDataExtractor.cs
public static List<NoteInfo> ExtractNoteData(object laneData, Type noteDataType)
{
    var notes = new List<NoteInfo>();
    
    try
    {
        if (laneData == null || noteDataType == null)
            return notes;

        // 1. laneData에서 Item 프로퍼티 획득
        var laneDataType = laneData.GetType();
        var itemProp = laneDataType.GetProperty("Item");

        // 2. 각 레인에서 노트 추출 (0~9)
        for (int lane = 0; lane <= 9; lane++)
        {
            try
            {
                // 3. 레인의 노트 리스트 가져오기
                var noteList = itemProp.GetValue(laneData, new object[] { lane });
                if (noteList != null)
                {
                    // 4. Count 프로퍼티로 노트 개수 확인
                    var countProperty = noteList.GetType().GetProperty("Count");
                    if (countProperty != null)
                    {
                        var count = (int)countProperty.GetValue(noteList);
                        
                        // 5. Item[index]로 각 노트 접근
                        for (int i = 0; i < count; i++)
                        {
                            var itemProperty = noteList.GetType().GetProperty("Item");
                            if (itemProperty != null)
                            {
                                // 6. 개별 노트 객체 가져오기
                                var note = itemProperty.GetValue(noteList, new object[] { i });
                                if (note != null)
                                {
                                    // 7. 노트 필드 값 추출 (ExtractNoteInfo 호출)
                                    var noteInfo = ExtractNoteInfo(note, noteDataType, lane);
                                    if (noteInfo != null)
                                    {
                                        notes.Add(noteInfo);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 개별 레인 추출 실패는 무시
            }
        }
    }
    catch (Exception ex)
    {
        MelonLogger.Warning($"[NoteDataExtractor] 노트 데이터 추출 실패: {ex.Message}");
    }

    return notes;
}

// 개별 노트 정보 추출 (별도 메서드)
private static NoteInfo ExtractNoteInfo(object note, Type noteDataType, int lane)
{
    try
    {
        // BindingFlags로 public/private 필드 모두 검색
        var timingField = noteDataType.GetField("timing", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var nTypeField = noteDataType.GetField("nType", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var nColorField = noteDataType.GetField("nColor", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        var timing = timingField != null ? (float)timingField.GetValue(note) : 0f;
        var nType = nTypeField != null ? nTypeField.GetValue(note) : null;
        var nColor = nColorField != null ? nColorField.GetValue(note) : null;

        return new NoteInfo
        {
            Timing = timing,
            Lane = lane,
            NoteType = nType,  // 실제 코드는 NoteType 사용
            NoteColor = nColor // 실제 코드는 NoteColor 사용
        };
    }
    catch
    {
        return null;
    }
}
```

### 주요 특징 (실제 구현 기준)

1. **파라미터 차이**
   - 문서 초안: `ExtractNoteData(object sxgtData, Type noteDataType)`
   - **실제 코드**: `ExtractNoteData(object laneData, Type noteDataType)`
   - ⚠️ 첫 번째 인자가 `sxgtData`가 아니라 **`laneData` 자체**를 받음!

2. **비파괴적 읽기**
   - 원본 `laneData`를 수정하지 않음
   - 게임 로직에 영향 없이 분석 가능

3. **개별 레인 격리**
   - 한 레인의 오류가 다른 레인 처리를 막지 않음
   - `try-catch`로 각 레인 독립 처리 (예외 메시지 없이 조용히 스킵)

4. **타입 불일치 처리**
   - `noteDataType` 파라미터로 필드 조회 타입 지정
   - 잘못된 타입이 전달되면 필드를 찾지 못해 기본값(0 또는 null) 반환

5. **BindingFlags 사용**
   - `Public | NonPublic | Instance`로 private 필드도 접근 가능
   - 난독화된 필드에도 대응

### 실패 케이스와 해결

| 증상 | 원인 | 해결 방법 |
|------|------|----------|
| 모든 `timing`이 0 | `noteDataType` 불일치 | 실제 노트 타입으로 교체 (`note.GetType()`) |
| 특정 레인만 추출 실패 | 해당 레인이 비어있거나 null | 정상 동작 (빈 레인은 스킵) |
| Count는 있는데 Item 접근 실패 | 컬렉션 타입이 List가 아님 | 다른 접근 방식 필요 (GetEnumerator 등) |

### 활용 예시 (실제 호출 방식)

```csharp
// ⚠️ 주의: laneData 객체를 직접 전달해야 함
// sxgtData에서 laneData를 먼저 추출
var laneDataField = sxgtDataType.GetField("laneData", 
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
var laneData = laneDataField.GetValue(sxgtDataInstance);

// NoteDataExtractor 호출
var extractedNotes = NoteDataExtractor.ExtractNoteData(laneData, actualNoteType);

// 레인별 노트 개수 확인
var laneNoteCounts = extractedNotes.GroupBy(n => n.Lane)
                                   .ToDictionary(g => g.Key, g => g.Count());

// 타이밍 분포 확인
if (extractedNotes.Count > 0)
{
    var timingRange = new {
        Min = extractedNotes.Min(n => n.Timing),
        Max = extractedNotes.Max(n => n.Timing)
    };
    
    // 타입별 분포 (NoteType은 object 타입이므로 ToString() 비교)
    MelonLogger.Msg($"총 추출된 노트: {extractedNotes.Count}개");
    MelonLogger.Msg($"타이밍 범위: {timingRange.Min}ms ~ {timingRange.Max}ms");
}
```

---

## 4) 레인별 "타입 추출" (실제 타입/생성자 캐시) - SXGTDataHook

**파일**: [`sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs`](../sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs)

### 왜 타입 추출이 필요한가?

커스텀 노트를 주입하려면 다음 정보가 필요합니다:

1. **정확한 노트 타입**: `ShortNote`, `HoldNote` 등의 실제 클래스
2. **생성자 시그니처**: 각 타입별 생성자 파라미터 구조
3. **필드 정보**: `timing`, `lane`, `duration` 등의 정확한 필드명

난독화/버전 차이로 이 정보는 컴파일 타임에 알 수 없으므로, **실제 게임 내 노트 객체를 샘플링**하여 런타임에 파악합니다.

### 타입 추출 전략

```
원본 차트의 노트들 
  → 실제 런타임 타입 확인 (note.GetType())
  → 생성자 파라미터 분석 (GetConstructors())
  → 정보를 정적 필드에 캐시
  → 이후 커스텀 노트 생성에 재사용
```

### 동작 흐름 (실제 코드 기반)

```csharp
// 실제 파일: sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs의 ClearAllNotes 메서드
private static void ClearAllNotes(object sxgtDataInstance, Type sxgtDataType)
{
    try
    {
        MelonLogger.Msg("[SXGTDataHook] 원본 노트 제거 시작");

        // 1. laneData 필드 획득
        var laneDataField = sxgtDataType.GetField("laneData", 
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (laneDataField == null)
        {
            MelonLogger.Warning("[SXGTDataHook] laneData 필드를 찾을 수 없습니다.");
            return;
        }

        var laneData = laneDataField.GetValue(sxgtDataInstance);
        if (laneData == null)
        {
            MelonLogger.Warning("[SXGTDataHook] laneData가 null입니다.");
            return;
        }

        // 2. Dictionary의 Item 프로퍼티 획득
        var itemProp = laneData.GetType().GetProperty("Item");
        if (itemProp == null)
        {
            MelonLogger.Warning("[SXGTDataHook] laneData Item 프로퍼티를 찾을 수 없습니다.");
            return;
        }

        var stats = new NoteRemovalStats();

        // 3. 레인 0~9 순회
        for (int lane = 0; lane <= 9; lane++)
        {
            try
            {
                // 4. 특정 레인의 노트 리스트 획득
                var noteList = itemProp.GetValue(laneData, new object[] { lane });
                if (noteList == null) continue;

                // 5. 타입 정보 추출 (제거 전에 수행!)
                ExtractNoteTypesFromLane(noteList, lane, stats);

                // 6. 노트 제거
                RemoveNotesFromLane(noteList, lane, stats);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SXGTDataHook] 레인 {lane} 처리 실패: {ex.Message}");
            }
        }

        // 7. 결과 로깅
        MelonLogger.Msg($"[SXGTDataHook] 노트 제거 완료: {stats.TotalRemoved}개 제거, " +
                       $"{stats.ClearedLanes}개 레인 클리어, " +
                       $"발견된 노트 타입: {string.Join(", ", stats.FoundNoteTypes)}");
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"[SXGTDataHook] 노트 제거 오류: {ex.Message}");
    }
}
```

### 타입 추출 프로세스 (실제 구현)

```csharp
// ExtractNoteTypesFromLane 메서드
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
        // 최적화: 필요한 타입을 모두 찾았으면 중단
        if (_shortNoteType != null && _holdNoteType != null)
        {
            break;
        }

        try
        {
            var note = itemProperty.GetValue(noteList, new object[] { i });
            if (note != null)
            {
                // 실제 노트 타입 확인
                var actualNoteType = note.GetType();
                stats.FoundNoteTypes.Add(actualNoteType.FullName);
                
                // 노트 타입별로 저장 및 생성자 찾기
                StoreNoteTypeAndFindConstructors(actualNoteType);
                
                // 홀드 노트인지 확인
                if (IsHoldNote(note, actualNoteType))
                {
                    stats.HoldNoteCount++;
                }
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[SXGTDataHook] 레인 {lane} 노트 {i} 처리 실패: {ex.Message}");
        }
    }
}

// StoreNoteTypeAndFindConstructors 메서드
private static void StoreNoteTypeAndFindConstructors(Type actualNoteType)
{
    var typeName = actualNoteType.Name;
    
    // ShortNote 타입 판별 (이름 기반)
    if (typeName.Contains("ShortNote") || typeName.Contains("Short"))
    {
        if (_shortNoteType == null)
        {
            _shortNoteType = actualNoteType;
            _actualNoteType = actualNoteType; // 기본 타입으로도 설정
            
            // ShortNote 생성자 찾기 (timing, lane)
            _shortNoteConstructor = FindShortNoteConstructor(actualNoteType);
        }
    }
    // HoldNote 타입 판별
    else if (typeName.Contains("HoldNote") || typeName.Contains("Hold"))
    {
        if (_holdNoteType == null)
        {
            _holdNoteType = actualNoteType;
            
            // HoldNote 생성자들 찾기
            FindHoldNoteConstructors(actualNoteType);
        }
    }
    // 기타 노트 타입
    else
    {
        // 첫 번째로 발견된 노트 타입 저장 (기본 타입으로 사용)
        if (_actualNoteType == null)
        {
            _actualNoteType = actualNoteType;
        }
    }
}

### 캐시되는 정보

| 변수명 | 타입 | 설명 | 예시 값 |
|--------|------|------|---------|
| `_actualNoteType` | Type | 기본 노트 타입 | `NoteData` 또는 난독화명 |
| `_shortNoteType` | Type | Short 노트 타입 | `ShortNote` 또는 난독화명 |
| `_holdNoteType` | Type | Hold 노트 타입 | `HoldNote` 또는 난독화명 |
| `_shortNoteConstructor` | ConstructorInfo | Short 생성자 | `ShortNote(float, int)` |
| `_holdNoteBasicConstructor` | ConstructorInfo | Hold 기본 생성자 | `HoldNote(float, int)` |
| `_holdNoteDurationConstructor` | ConstructorInfo | Hold duration 생성자 | `HoldNote(float, int, int, int, float)` |
| `_holdNoteTickTimeConstructor` | ConstructorInfo | Hold tickTime 생성자 | `HoldNote(float, int, int, int, float[])` |

### 생성자 판별 로직

```csharp
private static void CacheHoldNoteConstructors(Type holdType)
{
    var constructors = holdType.GetConstructors();
    
    foreach (var ctor in constructors)
    {
        var parameters = ctor.GetParameters();
        
        // 파라미터 개수와 타입으로 구분
        if (parameters.Length == 5)
        {
            var lastParamType = parameters[4].ParameterType;
            
            if (lastParamType == typeof(float))
            {
                // HoldNote(timing, nType, nColor, lane, duration)
                _holdNoteDurationConstructor = ctor;
            }
            else if (lastParamType.IsArray && 
                     lastParamType.GetElementType() == typeof(float))
            {
                // HoldNote(timing, nType, nColor, lane, tickTime[])
                _holdNoteTickTimeConstructor = ctor;
            }
        }
        else if (parameters.Length == 2)
        {
            // HoldNote(timing, lane) - 기본 생성자
            _holdNoteBasicConstructor = ctor;
        }
    }
}
```

### 타입 추출의 중요성

1. **버전 호환성**
   - 게임 업데이트로 타입명이 바뀌어도 자동 대응
   - 하드코딩된 타입명 없이 동적 파악

2. **난독화 대응**
   - 난독화된 클래스명도 런타임에 정확히 획득
   - `a`, `b`, `c` 같은 이름이라도 구조로 판별 가능

3. **올바른 인스턴스 생성**
   - 캐시된 생성자로 정확한 타입의 노트 생성
   - 게임 코드가 예상하는 정확한 객체 타입 보장

### 타입 추출 실패 시나리오

| 상황 | 결과 | 해결 방법 |
|------|------|----------|
| 모든 레인이 비어있음 | 타입 캐시 실패 | 게임을 재시작하여 노트가 있는 곡 선택 |
| 특정 타입(Hold)만 없음 | 해당 타입 생성자 캐시 안 됨 | Hold 노트 주입 불가, Short만 가능 |
| 생성자 시그니처 변경 | 파라미터 불일치로 생성 실패 | 생성자 판별 로직 업데이트 필요 |

### 활용 예시 (CustomChartInjector에서)

```csharp
// 캐시된 타입 정보로 Short 노트 생성
if (_shortNoteConstructor != null)
{
    object shortNote = _shortNoteConstructor.Invoke(new object[] { 
        1000f,  // timing
        5       // lane
    });
    
    // laneData[5]에 추가
    AddNoteToLane(sxgtData, 5, shortNote);
}

// 캐시된 타입 정보로 Hold 노트 생성
if (_holdNoteDurationConstructor != null)
{
    object holdNote = _holdNoteDurationConstructor.Invoke(new object[] {
        2000f,  // timing
        1,      // nType (Hold)
        0,      // nColor
        3,      // lane
        500f    // duration
    });
    
    AddNoteToLane(sxgtData, 3, holdNote);
}
```

---

## 5) 레인별 "제거" (Clear/RemoveAt) - SXGTDataHook.ClearAllNotes

**파일**: [`sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs`](../sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs)

### 목적

원본 노트를 **완전히 제거**하여 커스텀 노트 주입을 위한 깨끗한 상태를 만듭니다.

### 제거 전략 (실제 구현 - 2단계 안전 제거)

```csharp
// 실제 파일: sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs의 RemoveNotesFromLane 메서드
private static void RemoveNotesFromLane(object noteList, int lane, NoteRemovalStats stats)
{
    var countProp = noteList.GetType().GetProperty("Count");
    if (countProp == null) return;

    int beforeCount = (int)countProp.GetValue(noteList);
    if (beforeCount == 0) return;

    // 1단계: Clear 메서드로 제거 시도
    var clearMethod = noteList.GetType().GetMethod("Clear");
    if (clearMethod != null)
    {
        clearMethod.Invoke(noteList, null);
        
        // 제거 후 확인
        int afterCount = (int)countProp.GetValue(noteList);

        if (afterCount == 0)
        {
            // Clear 성공
            stats.TotalRemoved += beforeCount;
            stats.ClearedLanes++;
        }
        else
        {
            // Clear 실패 - RemoveAt으로 전환
            MelonLogger.Warning($"[SXGTDataHook] 레인 {lane}: Clear 실패 " +
                              $"({beforeCount} -> {afterCount}), RemoveAt으로 강제 제거 시도");
            ForceRemoveNotesWithRemoveAt(noteList, countProp, beforeCount, stats);
        }
    }
    else
    {
        // Clear 메서드가 없는 경우 바로 RemoveAt 사용
        ForceRemoveNotesWithRemoveAt(noteList, countProp, beforeCount, stats);
    }
}

// 2단계: RemoveAt을 사용한 강제 제거
private static void ForceRemoveNotesWithRemoveAt(object noteList, 
    PropertyInfo countProp, int beforeCount, NoteRemovalStats stats)
{
    var removeAtMethod = noteList.GetType().GetMethod("RemoveAt");
    if (removeAtMethod != null && countProp != null)
    {
        int currentCount = beforeCount;
        
        // ⚠️ 중요: 인덱스 0을 반복 제거 (뒤에서부터가 아님!)
        while (currentCount > 0)
        {
            removeAtMethod.Invoke(noteList, new object[] { 0 });
            currentCount = (int)countProp.GetValue(noteList);
        }
        
        stats.TotalRemoved += beforeCount;
        stats.ClearedLanes++;
    }
}
```

### 실제 구현의 차이점

문서 초안과 실제 코드의 주요 차이:

| 항목 | 문서 초안 | 실제 코드 |
|------|----------|----------|
| 제거 방향 | 뒤에서부터 (i = count-1; i >= 0) | **앞에서부터 반복 (RemoveAt(0))** |
| 제거 로직 | for 루프 | **while 루프 + Count 재확인** |
| 통계 수집 | 없음 | **NoteRemovalStats 객체 사용** |
| 로깅 | 간단 | **상세한 단계별 로깅** |

### 왜 RemoveAt(0)을 반복하나?

```csharp
// ✅ 실제 코드 방식 - RemoveAt(0) 반복
while (currentCount > 0)
{
    removeAtMethod.Invoke(noteList, new object[] { 0 });  // 항상 첫 번째 제거
    currentCount = (int)countProp.GetValue(noteList);     // 남은 개수 확인
}

// 왜 이 방식이 더 안전한가?
// - 항상 첫 번째 요소를 제거하므로 인덱스 범위 초과 불가능
// - 매번 Count를 확인하여 실제 제거 여부 검증
// - 무한 루프 방지 (Count가 변하지 않으면 자동 종료)
```
```

### 왜 2단계 제거가 필요한가?

| 단계 | 방법 | 목적 | 실패 가능성 |
|------|------|------|-------------|
| 1 | `Clear()` | 한 번에 모든 요소 제거 (빠름) | 컬렉션이 읽기 전용이거나 커스텀 구현일 때 |
| 2 | `RemoveAt()` | 개별 요소를 역순으로 제거 (안전) | `RemoveAt` 자체가 없거나 예외 발생 시 |

### 제거 순서가 중요한 이유 (업데이트)

```csharp
// ❌ 뒤에서부터 제거 (문서 초안에서 제시했던 방법)
for (int i = count - 1; i >= 0; i--)
{
    list.RemoveAt(i);  // 안전하지만 복잡
}

// ✅ 실제 구현 - 앞에서 반복 제거 (더 간단하고 안전)
while (count > 0)
{
    list.RemoveAt(0);  // 항상 첫 번째 제거
    count = list.Count; // 실제 개수 재확인
}

// 실제 구현이 더 나은 이유:
// 1. 인덱스 계산 불필요 (항상 0)
// 2. 매번 Count 검증으로 제거 실패 감지
// 3. 리플렉션 환경에서 더 안정적
```

### NoteRemovalStats 클래스 (실제 코드)

```csharp
// 노트 제거 통계를 추적하는 클래스
private class NoteRemovalStats
{
    public HashSet<string> FoundNoteTypes { get; set; } = new HashSet<string>();
    public int TotalRemoved { get; set; } = 0;
    public int ClearedLanes { get; set; } = 0;
    public int HoldNoteCount { get; set; } = 0;
}
```

### 전체 레인 제거 흐름 (실제 코드 반영)

```csharp
// ProcessNoteRemovalAndInjection에서 호출
private static void ClearAllNotes(object sxgtDataInstance, Type sxgtDataType)
{
    MelonLogger.Msg("[SXGTDataHook] 원본 노트 제거 시작");

    // laneData 필드 획득
    var laneDataField = sxgtDataType.GetField("laneData", 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    var laneData = laneDataField.GetValue(sxgtDataInstance);
    var itemProp = laneData.GetType().GetProperty("Item");

    var stats = new NoteRemovalStats();

    // 레인 0~9 순회
    for (int lane = 0; lane <= 9; lane++)
    {
        try
        {
            var noteList = itemProp.GetValue(laneData, new object[] { lane });
            if (noteList == null) continue;

            // 1. 타입 추출 (제거 전!)
            ExtractNoteTypesFromLane(noteList, lane, stats);

            // 2. 노트 제거
            RemoveNotesFromLane(noteList, lane, stats);
        }
        catch (Exception ex)
        {
            MelonLogger.Warning($"[SXGTDataHook] 레인 {lane} 처리 실패: {ex.Message}");
        }
    }

    // 3. 결과 로깅
    MelonLogger.Msg($"[SXGTDataHook] 노트 제거 완료:");
    MelonLogger.Msg($"  - 총 제거: {stats.TotalRemoved}개");
    MelonLogger.Msg($"  - 클리어한 레인: {stats.ClearedLanes}개");
    MelonLogger.Msg($"  - 발견된 타입: {string.Join(", ", stats.FoundNoteTypes)}");
    MelonLogger.Msg($"  - Hold 노트: {stats.HoldNoteCount}개");
}
```

### 제거 실패 케이스와 해결

| 증상 | 원인 | 해결 방법 |
|------|------|----------|
| Clear 후에도 노트 남음 | 읽기 전용 컬렉션 | RemoveAt 폴백 자동 실행 |
| RemoveAt도 실패 | 컬렉션이 수정 불가능 | 게임 버전 확인, laneData 접근 방식 변경 필요 |
| 일부 레인만 제거 안 됨 | 특정 레인의 컬렉션 타입이 다름 | 레인별 로그 확인 후 해당 레인 처리 로직 추가 |
| 제거 중 예외 발생 | 게임 상태 변경 중 제거 시도 | 적절한 Hook 타이밍으로 변경 (로딩 완료 후) |

### 제거 검증 방법

```csharp
// 제거 후 레인 상태 확인
private static void VerifyAllLanesCleared(object sxgtData)
{
    bool allCleared = true;
    
    for (int lane = 0; lane <= 9; lane++)
    {
        object noteList = GetNoteListForLane(sxgtData, lane);
        int count = GetCount(noteList);
        
        if (count > 0)
        {
            Logger.Warning($"레인 {lane}에 {count}개 노트 잔존");
            allCleared = false;
        }
    }
    
    if (allCleared)
    {
        Logger.Info("✓ 모든 레인이 성공적으로 비워짐");
    }
}
```

---

## 6) 레인 범위는 왜 0~9인가?

### 고정된 레인 범위

현재 코드에서 레인 번호는 **하드코딩된 상수**입니다:

```csharp
for (int lane = 0; lane <= 9; lane++)
{
    // 레인별 처리
}
```

**사용 위치**:
- [`NoteDataExtractor.cs`](../sxtg2-mod/Processors/NoteDataExtractor.cs) - `ExtractNoteData`
- [`SXGTDataHook.cs`](../sxtg2-mod/Hooks/SXGT/SXGTDataHook.cs) - `ClearAllNotes`
- [`CustomChartInjector.cs`](../sxtg2-mod/Processors/CustomChartInjector.cs) - 노트 주입

### 레인 범위 선택 근거

| 이유 | 설명 |
|------|------|
| **게임 사양** | 대부분의 리듬 게임은 5~10개 레인 사용 |
| **BMS 표준** | BMS는 최대 36개 채널이지만 실제 노트는 10개 레인 내 |
| **안전 범위** | 0~9로 고정하면 대부분의 곡을 커버 가능 |
| **성능** | 10개 레인만 순회하므로 오버헤드 최소 |

### 레인 수 확장이 필요한 경우

게임이 10개 이상의 레인을 사용한다면:

```csharp
// 동적 레인 범위 구하기
private static int GetMaxLaneCount(object sxgtData)
{
    var laneData = GetLaneDataDictionary(sxgtData);
    
    // Dictionary의 Keys 프로퍼티로 실제 레인 번호 확인
    var keysProperty = laneData.GetType().GetProperty("Keys");
    var keys = keysProperty.GetValue(laneData);
    
    // 최대 키 값 찾기
    int maxLane = 0;
    foreach (object key in (IEnumerable)keys)
    {
        int lane = (int)key;
        if (lane > maxLane) maxLane = lane;
    }
    
    return maxLane;
}

// 사용
int maxLane = GetMaxLaneCount(sxgtData);
for (int lane = 0; lane <= maxLane; lane++)
{
    // ...
}
```

### BMS와 레인 매핑

| BMS 채널 | 의미 | laneData 레인 |
|----------|------|---------------|
| `#xxx11` | Player 1, Lane 1 | 0 |
| `#xxx12` | Player 1, Lane 2 | 1 |
| `#xxx13` | Player 1, Lane 3 | 2 |
| `#xxx14` | Player 1, Lane 4 | 3 |
| `#xxx15` | Player 1, Lane 5 | 4 |
| `#xxx16` | Player 1, Lane 6 | 5 |
| `#xxx17` | Player 1, Lane 7 | 6 |
| `#xxx18` | Player 1, Lane 8 | 7 |
| `#xxx19` | Player 1, Lane 9 | 8 |
| `#xxx1A` | Player 1, Lane A (10번째) | 9 |

---

## 7) 디버깅 체크리스트

레인별 추출/제거가 제대로 작동하는지 확인하기 위한 단계별 점검 사항입니다.

### 7.1) 레인 접근 확인

**확인 항목**:
- [ ] `laneData` 필드를 찾았는가?
- [ ] `laneData`가 null이 아닌가?
- [ ] Dictionary의 `Item` 프로퍼티를 획득했는가?

**확인 방법**:
```csharp
Logger.Debug($"laneData 타입: {laneData?.GetType().FullName ?? "null"}");
Logger.Debug($"laneData Keys 개수: {GetDictionaryKeyCount(laneData)}");
```

**예상 로그**:
```
[DEBUG] laneData 타입: System.Collections.Generic.Dictionary`2[System.Int32,System.Collections.Generic.List`1[NoteData]]
[DEBUG] laneData Keys 개수: 10
```

### 7.2) 타입 추출 확인

**확인 항목**:
- [ ] `_actualNoteType`이 캐시되었는가?
- [ ] `_shortNoteType` / `_holdNoteType`이 캐시되었는가?
- [ ] 생성자들이 모두 캐시되었는가?

**확인 방법**:
```csharp
Logger.Info($"[타입 캐시 상태]");
Logger.Info($"  actualNoteType: {_actualNoteType?.FullName ?? "없음"}");
Logger.Info($"  shortNoteType: {_shortNoteType?.FullName ?? "없음"}");
Logger.Info($"  holdNoteType: {_holdNoteType?.FullName ?? "없음"}");
Logger.Info($"  shortConstructor: {_shortNoteConstructor != null}");
Logger.Info($"  holdDurationConstructor: {_holdNoteDurationConstructor != null}");
```

**예상 로그**:
```
[INFO] [타입 캐시 상태]
[INFO]   actualNoteType: Game.NoteData
[INFO]   shortNoteType: Game.ShortNote
[INFO]   holdNoteType: Game.HoldNote
[INFO]   shortConstructor: True
[INFO]   holdDurationConstructor: True
```

### 7.3) 노트 제거 확인

**확인 항목**:
- [ ] 모든 레인의 노트 개수가 0이 되었는가?
- [ ] "레인별 노트 개수" 로그가 찍히는가?
- [ ] RemoveAt 폴백이 실행되었는가?

**확인 방법**:
```csharp
// 제거 전
for (int lane = 0; lane <= 9; lane++)
{
    int count = GetLaneNoteCount(sxgtData, lane);
    Logger.Info($"[제거 전] 레인 {lane}: {count}개");
}

// 제거 수행
ClearAllNotes(sxgtData);

// 제거 후
for (int lane = 0; lane <= 9; lane++)
{
    int count = GetLaneNoteCount(sxgtData, lane);
    Logger.Info($"[제거 후] 레인 {lane}: {count}개");
}
```

**예상 로그**:
```
[INFO] [제거 전] 레인 0: 45개
[INFO] [제거 전] 레인 1: 38개
...
[INFO] [SXGTDataHook] 원본 노트 제거 시작
[INFO] [제거 후] 레인 0: 0개
[INFO] [제거 후] 레인 1: 0개
...
```

### 7.4) NoteDataExtractor 동작 확인

**확인 항목**:
- [ ] 추출된 노트 개수가 원본과 일치하는가?
- [ ] `timing` 값이 0이 아닌 실제 값인가?
- [ ] `nType` / `nColor`가 유효한 값인가?

**확인 방법**:
```csharp
var notes = NoteDataExtractor.ExtractNoteData(sxgtData, _actualNoteType);

Logger.Info($"총 추출된 노트: {notes.Count}개");
Logger.Info($"평균 timing: {notes.Average(n => n.Timing)}ms");
Logger.Info($"Short 노트: {notes.Count(n => n.Type == 0)}개");
Logger.Info($"Hold 노트: {notes.Count(n => n.Type == 1)}개");
```

**예상 로그**:
```
[INFO] 총 추출된 노트: 384개
[INFO] 평균 timing: 125430.5ms
[INFO] Short 노트: 312개
[INFO] Hold 노트: 72개
```

### 7.5) 문제 진단 플로우차트

```
노트가 제대로 추출되지 않음
  ↓
timing이 모두 0인가?
  ├─ Yes → noteDataType 불일치
  │         → note.GetType()으로 실제 타입 확인
  └─ No  → 노트 개수가 0인가?
            ├─ Yes → laneData 접근 실패
            │         → laneData null 체크
            │         → Dictionary Item 프로퍼티 확인
            └─ No  → 일부 레인만 실패?
                      ├─ Yes → 해당 레인 예외 로그 확인
                      └─ No  → 정상 동작
```

### 7.6) 자주 발생하는 오류와 해결

| 오류 메시지 | 원인 | 해결 |
|-------------|------|------|
| `NullReferenceException at laneData` | laneData 필드를 찾지 못함 | 필드명 확인, 게임 버전 체크 |
| `TargetInvocationException` | 잘못된 파라미터로 생성자 호출 | 생성자 시그니처 재확인 |
| `ArgumentOutOfRangeException` | 존재하지 않는 레인 접근 | Dictionary ContainsKey 체크 |
| `InvalidCastException` | 타입 변환 실패 | 실제 타입 확인, 캐스트 제거 |

---

## 8) 성능 최적화 팁

### 8.1) 불필요한 리플렉션 호출 줄이기

```csharp
// ❌ 매번 리플렉션
for (int i = 0; i < count; i++)
{
    var note = noteItemProp.GetValue(noteList, new object[] { i });
    var timing = note.GetType().GetField("timing").GetValue(note);  // 매번 GetField!
}

// ✅ 필드 정보 캐싱
var timingField = noteType.GetField("timing");
for (int i = 0; i < count; i++)
{
    var note = noteItemProp.GetValue(noteList, new object[] { i });
    var timing = timingField.GetValue(note);  // 캐시된 FieldInfo 사용
}
```

### 8.2) 빈 레인 스킵

```csharp
for (int lane = 0; lane <= 9; lane++)
{
    var noteList = GetNoteListForLane(sxgtData, lane);
    int count = GetCount(noteList);
    
    if (count == 0)
    {
        continue;  // 빈 레인은 처리 안 함
    }
    
    // 실제 처리
}
```

### 8.3) 병렬 처리 (주의해서 사용)

```csharp
// 읽기 전용 작업만 병렬화 가능
Parallel.For(0, 10, lane =>
{
    try
    {
        var notes = ExtractNotesFromLane(sxgtData, lane);
        ProcessNotes(notes);
    }
    catch (Exception ex)
    {
        Logger.Error($"레인 {lane} 처리 오류: {ex}");
    }
});

// ⚠️ 주의: laneData 수정 작업은 병렬화하면 안 됨!
```

### 8.4) 메모리 효율

```csharp
// ❌ 매번 새 배열 생성
for (int i = 0; i < count; i++)
{
    var note = noteItemProp.GetValue(noteList, new object[] { i });  // 새 배열!
}

// ✅ 배열 재사용
object[] indexArgs = new object[1];
for (int i = 0; i < count; i++)
{
    indexArgs[0] = i;
    var note = noteItemProp.GetValue(noteList, indexArgs);
}
```

---

## 9) 요약

### 작업별 비교표

| 작업 | 담당 클래스 | 핵심 메서드 | laneData 수정 여부 | 주요 목적 |
|------|------------|-------------|-------------------|----------|
| **타입 정보 수집** | `SXGTDataHook` | `ClearAllNotes` | ❌ (읽기 전) | 런타임 타입 캐싱 |
| **노트 제거** | `SXGTDataHook` | `ClearAllNotes` | ✅ (수정) | 원본 노트 삭제 |
| **노트 분석/추출** | `NoteDataExtractor` | `ExtractNoteData` | ❌ (읽기만) | 통계/분석 |
| **커스텀 노트 주입** | `CustomChartInjector` | `InjectNotes` | ✅ (추가) | BMS 노트 추가 |

### 핵심 원칙

1. **리플렉션 패턴**
   - 모든 레인 접근은 리플렉션 + indexer 패턴 사용
   - 난독화 대응을 위해 런타임 타입 정보 활용

2. **타입 추출**
   - 타입 정보는 런타임에 실제 객체에서 추출
   - 버전 변경/난독화에 자동 대응

3. **안전 제거**
   - 노트 제거는 2단계 안전 제거 (Clear → RemoveAt 폴백)
   - 역순 제거로 인덱스 오류 방지

4. **고정 레인 범위**
   - 레인 범위는 0~9 고정 (필요시 동적 확장 가능)
   - BMS 채널 `#xxx11`~`#xxx1A`에 대응

5. **오류 격리**
   - 개별 레인 오류는 격리 처리 (다른 레인에 영향 없음)
   - try-catch로 각 레인 독립 처리

### 전체 처리 흐름

```
1. 곡 선택/로딩
   ↓
2. SXGTData 객체 생성 (게임 내부)
   ↓
3. laneData 접근 (리플렉션)
   ↓
4. 타입 정보 수집 (첫 노트에서)
   ├─ _shortNoteType 캐시
   ├─ _holdNoteType 캐시
   └─ 생성자 캐시
   ↓
5. 원본 노트 제거 (Clear/RemoveAt)
   ├─ 레인 0: Clear
   ├─ 레인 1: Clear
   └─ ...
   ↓
6. 커스텀 노트 주입 (BMS 파싱 결과)
   ├─ ShortNote 생성 → 레인에 추가
   └─ HoldNote 생성 → 레인에 추가
   ↓
7. 게임 플레이 시작
```

### 개선 방향 제안

1. **동적 레인 범위**: 게임이 10개 이상의 레인을 사용할 경우를 대비한 동적 범위 설정
2. **타입 검증**: 캐시된 타입 정보의 유효성 검사 강화
3. **성능 프로파일링**: 병목 구간 식별 및 최적화
4. **오류 복구**: 제거 실패 시 재시도 로직 추가
5. **로깅 개선**: 더 상세한 디버그 정보 제공
