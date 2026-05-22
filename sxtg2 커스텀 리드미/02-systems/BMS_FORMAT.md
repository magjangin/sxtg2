# 📄 BMS 파일 포맷

**BMS 파일 형식 상세 명세**

---

## 파일 구조

```bms
#PLAYER 1
#GENRE Dance
#TITLE Sample Song
#ARTIST Test Artist
#BPM 120
#PLAYLEVEL 5

#WAV01 kick.wav
#WAV02 snare.wav
#BMP01 bg.bmp

#00111:01010000
#00112:00020300
#00116:01000100
```

---

## 헤더 명령어

### 메타데이터

```bms
#PLAYER 1          # 플레이어 수 (1 또는 2)
#GENRE Dance       # 장르
#TITLE Song Name   # 곡 제목
#ARTIST Artist     # 아티스트
#PLAYLEVEL 5       # 난이도
```

### BPM 설정

```bms
#BPM 120           # 기본 BPM
#BPM01 180         # BPM 인덱스 01 = 180
#BPM02 140         # BPM 인덱스 02 = 140
```

**구현:**
```csharp
if (key == "BPM")
{
    if (value.Length == 2) // #BPMXX
    {
        var index = value.Substring(0, 2);
        var bpm = float.Parse(value.Substring(2));
        bpmDict[index] = bpm;
    }
    else // #BPM
    {
        var bpm = float.Parse(value);
        bpmDict["00"] = bpm;
        
        // dataList에 추가
        dataList.Add(new BpmData 
        { 
            Tick = 0, 
            Freq = 60f / bpm 
        });
    }
}
```

### 리소스 정의

```bms
#WAV01 kick.wav    # 음원 01
#WAV02 snare.wav   # 음원 02
#WAVZZ bass.wav    # 음원 ZZ (36진수)

#BMP01 bg.bmp      # 이미지 01
#BMP02 layer.bmp   # 이미지 02
```

**참고:** sxtg2는 리소스 정의를 무시합니다.

---

## 데이터 라인

### 기본 형식

```
#MMMCC:데이터

MMM: Measure (마디 번호, 3자리, 선택)
CC:  Channel (채널 번호, 2자리)
데이터: 16진수 값 (2자리씩)
```

### 예시

```bms
#00111:01010000
  ^^^   ^^^^^^^^
  │││   └─ 데이터 (01, 01, 00, 00)
  ││└─ 채널 11
  └┴─ Measure 001

#16:0203
 ^  ^^^^
 │  └─ 데이터 (02, 03)
 └─ 채널 16 (Measure 생략 = 000)
```

---

## 채널 정의

### 노트 채널 (지원)

```
11: P1 레인 1 → 게임 레인 1
12: P1 레인 2 → 게임 레인 2
13: P1 레인 3 → 게임 레인 3
14: P1 레인 4 → 게임 레인 4
15: P1 레인 5 → 게임 레인 5
16: P1 레인 6 → 게임 레인 0
18: P1 레인 7 → 게임 레인 6
```

**구현:**
```csharp
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

### BGA 채널 (무시)

```
01: BGA 베이스
02: BGA 레이어 1
03: BGA 레이어 2
04: BGA Poor
```

### BPM 변화 채널 (미지원)

```
03: BPM 변화 (16진수)
08: Extended BPM 변화
```

### STOP 채널 (미지원)

```
09: STOP 시퀀스
```

---

## 노트 값

### 일반 노트

```
01: 일반 노트 → SHORT
00: 빈 슬롯
```

### 홀드 노트

```
02: 홀드 시작 → HOLD
03: 홀드 끝 (제거됨)
```

**예시:**
```bms
#00111:02000300
        ^    ^
        │    └─ 홀드 끝 (슬롯 2)
        └─ 홀드 시작 (슬롯 0)
```

### 오픈/클로즈 노트

```
04: OPEN → HOLD (레인 9)
05: CLOSE (제거됨)
```

**예시:**
```bms
#00104:04000500
        ^    ^
        │    └─ CLOSE (슬롯 2)
        └─ OPEN (슬롯 0)
```

---

## Tick 계산

```
tick = measure + (슬롯_인덱스 / 총_슬롯_수)
```

**예시:**
```bms
#00116:01020304
```

- Measure: 001
- 데이터 길이: 8 (4개 값 × 2자리)
- 총 슬롯: 4

```
슬롯 0 (01): tick = 1 + (0 / 4) = 1.000
슬롯 1 (02): tick = 1 + (1 / 4) = 1.250
슬롯 2 (03): tick = 1 + (2 / 4) = 1.500
슬롯 3 (04): tick = 1 + (3 / 4) = 1.750
```

**구현:**
```csharp
var objLength = data.Length / 2; // 총 슬롯 수

for (int i = 0; i < data.Length; i += 2)
{
    var noteValue = data.Substring(i, 2);
    if (noteValue == "00") continue;
    
    var slotIndex = i / 2;
    var tick = (float)measure + ((float)slotIndex / objLength);
    
    // ...
}
```

---

## 시간 계산

```
time = tick × 4 × (60 / BPM)
```

**예시:**
```
BPM: 120
tick: 1.25

time = 1.25 × 4 × (60 / 120)
     = 1.25 × 4 × 0.5
     = 2.5초
```

**구현:**
```csharp
private static float CalculateTime(float tick, List<BpmData> dataList)
{
    if (dataList.Count == 0)
        return tick * 0.4f;
    
    var data = dataList.FindAll(d => d.Tick < tick);
    if (data.Count == 0)
    {
        var firstBpm = dataList[0];
        return tick * 4f * firstBpm.Freq;
    }
    
    float time = 0f;
    for (var j = data.Count - 1; j >= 0; j--)
    {
        var obj = data[j];
        var offset = (j == 0) ? tick - obj.Tick : data[j - 1].Tick - obj.Tick;
        time += offset * 4f * obj.Freq;
    }
    
    return time;
}
```

---

## 파일 예시

### 간단한 BMS

```bms
#BPM 120
#TITLE Simple Song

#00111:01010000
#00112:00020300
#00116:01000100
```

### 홀드 노트 포함

```bms
#BPM 140
#TITLE Hold Test

#00111:02000000
#00111:00000300
#00112:01010101
```

### 오픈/클로즈 포함

```bms
#BPM 160
#TITLE Open Test

#00104:04000000
#00104:00000500
#00111:01010101
```

---

## 파싱 순서

```
1. 파일 읽기
   ↓
2. 라인별 처리
   ├─ 헤더? → BPM/메타데이터 저장
   └─ 데이터? → 노트 파싱
   ↓
3. 노트 정렬 (시간순)
   ↓
4. 홀드 노트 매칭
   ↓
5. 끝 노트 제거
   ↓
6. 검증 및 통계
```

**구현:**
```csharp
public static List<ParsedNote> ParseBmsFile(string filePath)
{
    var lines = File.ReadAllLines(filePath);
    var notes = new List<ParsedNote>();
    var bpmDict = new Dictionary<string, float>();
    var dataList = new List<BpmData>();
    
    // 1. 라인별 파싱
    foreach (var line in lines)
    {
        if (line.StartsWith("#"))
        {
            if (line.Contains(" "))
                ParseHeader(line, bpmDict, dataList);
            else if (line.Contains(":"))
                ParseNoteData(line, notes, dataList, bpmDict);
        }
    }
    
    // 2. 정렬
    notes = notes.OrderBy(n => n.Time).ToList();
    
    // 3. 홀드 매칭
    CalculateHoldNoteLengths(notes);
    
    // 4. 통계
    LogStatistics(notes);
    
    return notes;
}
