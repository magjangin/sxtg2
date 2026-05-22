# BMS 파일 파싱 상세

이 문서는 BMS 파일 파싱 과정을 상세히 설명합니다.

## 목차

1. [초기화 및 파일 스캔](#초기화-및-파일-스캔)
2. [BMS 파일 파싱 상세](#bms-파일-파싱-상세)
3. [Tick 계산](#tick-계산)
4. [시간 계산 로직](#시간-계산-로직)
5. [홀드 노트 길이 계산](#홀드-노트-길이-계산)
6. [파싱 결과 저장](#파싱-결과-저장)
7. [현재 구현의 제한사항](#현재-구현의-제한사항)

---

## 초기화 및 파일 스캔

**위치:** `sxtg2-mod/Main/Main.BmsBootstrap.cs`, `sxtg2-mod/Hooks/Text/TextHook.BmsLoader.cs`

### 1. hwa 폴더 준비 (중요)

현재 `sxtg2` 구현은 **`hwa` 폴더를 자동 생성하지 않습니다.**  
따라서 사용자가 게임 설치 폴더에 직접 `hwa` 폴더를 만들어야 합니다.

- **경로**: `{게임_설치_폴더}/hwa`
- 폴더가 없으면 `Main.ScanAndParseBmsFiles()`는 조용히 종료합니다(로그만 출력).

### 2. BMS 파일 스캔(+ 기본 차트 선택)

`Main.ScanAndParseBmsFiles()`는 아래 위치에서 BMS 파일을 찾습니다.

- **지원 확장자**: `*.bms`, `*.bme`, `*.bml`
- **검색 범위(중요)**:
  - `hwa` **루트 폴더**: `TopDirectoryOnly`
  - `hwa` 아래의 **1단계 앨범 폴더들**: 각 폴더에서 `TopDirectoryOnly`
- **재귀 검색은 하지 않습니다.** (단, `TextHook`의 TrackId 기반 검색은 별도 로직을 가집니다)

### 3. BMS 파일 파싱(통계) 및 적용 대상

`Main.ScanAndParseBmsFiles()`는 발견된 모든 BMS에 대해 아래를 수행합니다.

- 각 BMS 파일을 `BmsParser.ParseBmsFileWithStatistics(...)`로 파싱
- 노트 통계(총 노트/일반/홀드/오픈 및 끝노트 누락)를 로그로 출력
- **기본 차트**로는 “발견된 첫 번째 파일”의 파싱 결과를 `CustomChartInjector.SetParsedBmsNotes(...)`에 세팅

그리고 플레이 진입 시점에는 다음이 추가로 발생할 수 있습니다.

- `TextHook.LoadAndInjectBmsForTrack(trackId, displayName)`가 현재 트랙에 맞는 BMS를 다시 찾아 파싱하고, `CustomChartInjector.SetParsedBmsNotes(...)`를 **덮어쓸 수 있습니다.**

---

## BMS 파일 파싱 상세

**위치:** `sxtg2-mod/Loaders/BmsParser.cs`

파싱 과정은 한 줄씩 읽으면서 두 가지 타입의 라인을 처리합니다.

### 헤더 라인 파싱 (공백 포함 라인)

**형식:** `#KEY VALUE` (예: `#BPM 120`, `#BPM01 180`)

```csharp
if (line.Contains(' '))
{
    var split = line.Substring(1).Split(new[] { ' ' }, 2);
    if (split.Length >= 2)
    {
        var key = split[0];
        var value = split[1];
        
        if (key.Contains("BPM"))
        {
            // BPM 정보 추출
        }
    }
}
```

**처리 내용:**

- `#BPM`: 기본 BPM 설정
  - `bpmDict["00"]`에 저장
  - `dataList`에 `BpmData` 객체 추가 (Tick=0, Freq=60/BPM)
- `#BPMXX`: 특정 BPM 인덱스 정의 (현재는 저장만 하고 사용하지 않음)
  - `bpmDict[XX]`에 저장

**참고:** 현재 구현에서는 **BPM 변화 채널(03, 04, 08)을 감지하지 않습니다**. 헤더의 `#BPMXX`만 파싱하며, 실제 BPM 변화는 노트 데이터 채널에서 처리되지 않습니다.

### 노트 데이터 라인 파싱 (콜론 포함 라인)

**형식:** `#MMMCC:데이터` 또는 `#CC:데이터`
- `MMM`: Measure(마디) 번호 (3자리, 선택적)
- `CC`: Channel(채널) 번호 (2자리)
- `데이터`: 16진수 노트 데이터 (2자리씩 쌍으로 읽음)

**파싱 과정:**

1. **Measure와 Channel 추출**
   ```csharp
   // 5자리 이상: measure + channel (예: 00116)
   if (channel.Length >= 5)
   {
       var measureStr = channel.Substring(0, channel.Length - 2);
       var channelNum = channel.Substring(channel.Length - 2);
       if (int.TryParse(measureStr, out int m))
       {
           measure = m;
       }
   }
   // 2자리: channel만 (measure 없음, measure=0으로 처리)
   else if (channel.Length >= 2)
   {
       var channelNum = channel.Substring(channel.Length - 2);
       measure = 0;
   }
   ```

2. **채널 필터링(중요)**
   - 지원 채널: 11, 12, 13, 14, 15, 16, 18
   - **예외적으로 채널 04, 05 라인도 처리**합니다.
   - 그 외 채널(03, 08 등 BPM 변화 채널 포함)은 현재 구현에서 무시됩니다.
   - 노트 값 04, 05는 **(처리 대상 라인 안에서)** OPEN/CLOSE 노트로 인식되어 레인 9로 변환됩니다.

3. **노트 데이터 파싱** (`ParseNoteData()`)
   - 데이터를 2자리씩 읽어서 노트 값 추출
   - `00`은 빈 슬롯으로 무시
   - 각 노트에 대해:
     - **Tick 계산**: `tick = measure + (i / objLength)`
     - **시간 계산**: `CalculateTime(tick, dataList)` 호출
     - **노트 타입 확인**: `NoteTypeMapping`에서 노트 값(01, 02, 03, 04, 05)을 `NoteType`으로 변환
     - **레인 매핑**: `LaneMapping`을 사용하여 BMS 채널을 게임 레인으로 변환
     - **OPEN/CLOSE 노트**: 레인 9로 강제 설정

**레인 매핑:**
```
BMS 채널 → 게임 레인
16 → 0
11 → 1
12 → 2
13 → 3
14 → 4
15 → 5
18 → 6
노트 값 04/05 → 9 (오픈/클로즈 노트: “처리 대상 채널”에서만)
```

**노트 타입 매핑:**
```
BMS 노트 값 → NoteType
01 → Normal (일반 노트)
02 → Long (홀드 시작)
03 → HoldEnd (홀드 끝)
04 → Open (오픈 노트)
05 → Close (클로즈 노트)
```

---

## Tick 계산

**위치:** `BmsParser.ParseNoteData()`

```csharp
// tick 계산: measure + measure 내 위치 (0.0 ~ 1.0)
var tick = (float)measure + ((float)(i / 2) / objLength);
```

**설명:**
- `measure`: 마디 번호 (정수)
- `i`: 데이터 문자열 내 인덱스 (2자리씩 읽으므로 `i / 2`가 슬롯 인덱스)
- `objLength`: measure의 총 슬롯 수 (데이터 길이 / 2)
- `tick`: measure 단위의 실수 값
  - 예: measure 1의 첫 번째 슬롯 = 1.0
  - 예: measure 1의 중간 슬롯 = 1.5
  - 예: measure 2의 마지막 슬롯 = 2.999...

---

## 시간 계산 로직

**위치:** `BmsParser.CalculateTime()`

BMS 파일의 노트 위치(tick)를 실제 시간(초)으로 변환하는 핵심 로직입니다.

### 기본 개념

- BMS에서 노트 위치는 `measure`(마디)와 `measure 내 위치`로 표현됩니다
- `tick = measure + (measure 내 위치 / measure 길이)`
- 예: measure 1의 중간 지점 = 1.5 tick
- **1 measure = 4 beats** (4/4 박자 기준)

### BPM 처리

```csharp
// 기본 BPM 설정 (#BPM 헤더)
var bpm = float.Parse(value);
var freq = 60f / bpm; // 1분음표 기준 (4분음표가 아님)
// freq = 1분음표의 길이(초)
```

**중요:** `freq`는 **1분음표(whole note)**의 길이입니다. 4분음표가 아닙니다.

### 시간 계산 알고리즘

**1. BPM 데이터가 없는 경우:**
```csharp
if (dataList.Count == 0)
{
    return tick * 0.4f; // 기본 시간 계산 (1 measure = 4 beats)
}
```

**2. BPM 변화가 없는 경우:**
```csharp
var data = dataList.FindAll(d => d.Tick < tick);
if (data.Count == 0)
{
    // BPM 변화가 없으면 첫 번째 BPM 사용
    var firstBpm = dataList[0];
    time = tick * 4f * firstBpm.Freq; // 1 measure = 4 beats
    return time;
}
```

**3. BPM 변화가 있는 경우:**

현재 구현은 복잡한 알고리즘을 사용합니다:

```csharp
// 현재 tick보다 작은 BPM 변화들 찾기
var data = dataList.FindAll(d => d.Tick < tick);

// 역순으로 순회하면서 각 BPM 구간별 시간 누적
for (var j = data.Count - 1; j >= 0; j--)
{
    var obj = data[j];
    var offset = 0f;
    var freq = obj.Freq;
    
    // 이전 BPM 변화 지점과의 offset 계산
    if (j - 1 >= 0)
    {
        var prevObj = data[j - 1];
        offset = prevObj.Tick - obj.Tick;
    }
    
    // 마지막 구간은 현재 tick까지
    else
    {
        offset = tick - obj.Tick;
    }
    
    // offset을 beat 단위로 변환하여 시간 계산
    time += offset * 4f * freq;
}
```

**구체적인 예시(기준값):**
```
기본 BPM: BASE_BPM (freq = 60/BASE_BPM)
중간 BPM: FAST_BPM (freq = 60/FAST_BPM)

노트가 measure 3.5에 있다면:
- 초반 구간: base BPM 구간 누적 시간
- 이후 구간: 변경된 BPM 구간 누적 시간
- 총 시간: 각 구간 시간을 합산
```

**참고:** 현재 구현에서는 BPM 변화가 measure 단위로만 처리됩니다. 노트 데이터 채널(03, 04, 08)에서의 BPM 변화는 감지하지 않습니다.

---

## 홀드 노트 길이 계산

**위치:** `BmsParser.CalculateHoldNoteLengths()`

파싱이 완료된 후, 홀드 노트의 길이를 계산합니다.

### 일반 홀드 노트 (02-03 쌍)

1. 레인별로 노트를 그룹화
2. 시간 순으로 정렬
3. 각 레인에서:
   - `02`(홀드 시작) 노트를 찾으면
   - 같은 레인에서 다음 `03`(홀드 끝) 노트를 찾음
   - 길이 = `endNote.Time - note.Time`
   - `note.Length`에 저장
   - `03` 노트는 나중에 제거됨

**중요:** `09` 노트는 지원하지 않습니다. `02-03` 쌍만 홀드로 처리됩니다.

### 이벤트 홀드 노트 (04-05 쌍)

1. 레인 9에서만 처리 (OPEN/CLOSE 노트는 모두 레인 9)
2. `04`(OPEN) 노트를 찾으면
3. 같은 레인에서 다음 `05`(CLOSE) 노트를 찾음
4. 길이 = `closeNote.Time - openNote.Time`
5. `openNote.Length`에 저장
6. `05` 노트는 나중에 제거됨

### 끝 노트 제거

```csharp
// 길이 계산 후 HoldEnd와 Close 노트 제거 (게임에 표시되지 않도록)
notes.RemoveAll(n => n.NoteType == NoteType.HoldEnd || n.NoteType == NoteType.Close);
```

홀드 끝 노트(`03`, `05`)는 길이 계산에만 사용되고, 실제 게임에는 표시되지 않습니다.

---

## 파싱 결과 저장

**위치:** `sxtg2-mod/Main/Main.BmsBootstrap.cs` → `ScanAndParseBmsFiles()`, `sxtg2-mod/Hooks/Text/TextHook.BmsLoader.cs` → `ParseAndInjectBms(...)`

```csharp
// 커스텀 차트 주입을 위해 파싱된 노트 저장
sxtg2.Processors.CustomChartInjector.SetParsedBmsNotes(parsedNotes);
```

파싱된 노트 리스트는 `CustomChartInjector`에 저장되어, 나중에 게임의 `SXGTData`에 주입됩니다.

**ParsedNote 구조:**
```csharp
public class ParsedNote
{
    public float Time { get; set; }        // 시간(초)
    public int Lane { get; set; }          // 게임 레인 (0-6, 9)
    public NoteType NoteType { get; set; } // 노트 타입
    public float Length { get; set; }      // 홀드 노트 길이(초)
    public string OriginalNoteValue { get; set; } // 원본 노트 값 (01, 02, 03 등)
}
```

---

## 현재 구현의 제한사항

### 1. BPM 변화 채널 미지원
- 채널 03, 04, 08 (BPM 변화 채널)을 감지하지 않음
- 헤더의 `#BPMXX`만 파싱하지만, 실제 사용은 기본 BPM만 사용
- measure 단위의 BPM 변화는 지원하지 않음

### 2. 단일 BMS 파일만 사용
- 초기화 시점(`Main.ScanAndParseBmsFiles`)에는 **첫 번째로 발견된 BMS가 “기본 차트”**로 세팅됩니다.
- 하지만 실제 플레이에서는 `TextHook/ManagerPlayHook` 경로로 **트랙 기반 BMS를 재탐색/재파싱하여 덮어쓸 수 있습니다.**
  - 즉 “항상 첫 파일만”은 아니고, **최종적으로는 플레이 진입 시 선택된 트랙에 의해 바뀔 수 있습니다.**

### 3. 스캔 범위가 제한됨
- 초기화 시점의 스캔은 `hwa` 루트 + 1단계 앨범 폴더까지만 `TopDirectoryOnly`로 찾습니다(재귀 아님).
- TrackId 기반 검색(`TextHook`)은 별도의 방식으로 더 넓게 찾을 수 있으나, 이것도 “정확한 파일명(trackId.*) 우선 + 폴백” 로직입니다.

### 4. BMS 확장 기능 미지원
- STOP 채널, BGA 채널 등은 처리하지 않음
- 노트 채널만 처리

### 5. 파일 인코딩
- 특별한 인코딩 처리를 하지 않음
- UTF-8 또는 Shift-JIS 권장
- 한글 파일명이나 주석이 있는 경우 인코딩 문제가 발생할 수 있음

---

## 관련 문서

- [GAME_LOGIC.md](GAME_LOGIC.md): 게임 로직 분석 및 노트 생성 과정
- [IMPLEMENTATION.md](IMPLEMENTATION.md): 구현 상세 및 후킹 과정
- [DOCUMENTATION.md](DOCUMENTATION.md): 종합 참조 문서






















