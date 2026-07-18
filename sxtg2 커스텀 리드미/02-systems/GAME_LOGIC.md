# 게임 로직 분석

이 문서는 **Sixtar Gate STARTRAIL** 게임의 실제 로직을 분석한 것입니다. 실제 게임 타입 구조, 노트 생성 과정, 타입 추출 방법, 실행 시점 등을 상세히 설명합니다.

## 목차

1. [게임 타입 구조](#게임-타입-구조)
2. [laneData 구조](#lanedata-구조)
3. [노트 생성 과정](#노트-생성-과정)
4. [타입 추출 과정](#타입-추출-과정)
5. [실행 시점](#실행-시점)
6. [주요 메서드 호출 순서](#주요-메서드-호출-순서)

---

## 게임 타입 구조

### Note 타입 계층

게임은 다음과 같은 노트 타입 계층 구조를 사용합니다:

```
RhythmGame.Note (base 클래스)
├── RhythmGame.ShortNote (일반 노트)
└── RhythmGame.HoldNote (홀드 노트)
```

### ShortNote 클래스

**용도**: 일반 노트 (BMS 값 01)

**주요 필드**:
- `timing` (float): 노트 타이밍 (초)
- `nType` (NoteType): SHORT
- `nColor` (NoteColor): BLUE 또는 RED  
  - **코드 기준 규칙**: 레인 4, 5 → `RED`, 그 외 → `BLUE`
- `targetLane` (int): 레인 번호 (0-9)
- `referObject` (GameObject): 참조 게임 오브젝트
- `luckyScore` (int): 행운 점수
- `nAction` (NoteAction): 노트 액션

**생성자**:
- `ShortNote(float timing, int targetLane)`: 기본 생성자
- 생성 후 `nType`, `nColor` 필드를 설정

### HoldNote 클래스

**용도**: 홀드 노트 (BMS 값 02-03, 04-05)

**주요 필드**:
- `timing` (float): 노트 타이밍 (초)
- `nType` (NoteType): HOLD
- `nColor` (NoteColor): BLUE, RED, 또는 OPEN  
  - **코드 기준 규칙**:
    - OPEN/CLOSE(레인 9) → `OPEN`
    - 레인 4, 5 → `RED`
    - 그 외 → `BLUE`
- `targetLane` (int): 레인 번호 (0-9)
- `duration` (float): 홀드 지속 시간 (초) ⭐ **중요**
- `tickTime` (float[]): 틱 시간 배열 ⭐ **중요**
- `tickLength` (int): 틱 개수
- `tickJudge` (bool[]): 틱 판정 배열
- `isFinished` (bool): 완료 여부
- `isHeadJudged` (bool): 헤드 판정 여부
- `elapsedTick` (int): 경과 틱 수
- `referObject` (GameObject): 참조 게임 오브젝트
- `luckyScore` (int): 행운 점수
- `nAction` (NoteAction): 노트 액션

**생성자 옵션**:
1. `HoldNote(float timing, int targetLane)` + 필드 설정
2. `HoldNote(float timing, NoteType nType, NoteColor nColor, int targetLane, float duration)`
3. `HoldNote(float timing, NoteType nType, NoteColor nColor, int targetLane, float[] tickTime)`

**중요**: 홀드 노트 생성 시 **생성자에 duration 또는 tickTime을 전달**해야 합니다!

---

## laneData 구조

### 타입 정의

```csharp
laneData: Dictionary<int, List<RhythmGame.Note>>
```

- **키**: 레인 번호 (0-9)
- **값**: 해당 레인의 노트 리스트
- **제네릭 타입**: `List<Note>` (base 타입)

### 타입 호환성

**중요**: `List<Note>`에 파생 타입(`ShortNote`, `HoldNote`)을 추가할 수 있습니다!

```csharp
// 가능: 파생 타입을 base 타입으로 자동 캐스팅
List<Note> noteList = ...;
noteList.Add(new ShortNote(...));  // ✅ 가능
noteList.Add(new HoldNote(...));    // ✅ 가능
```

### 실제 타입 확인

laneData에서 실제 노트 타입을 확인하려면:

```csharp
var noteList = laneData[0];  // List<Note>
var firstNote = noteList[0];
var actualType = firstNote.GetType();  // 실제 타입: ShortNote 또는 HoldNote
```

---

## 노트 생성 과정

### ShortNote 생성

**1단계: 생성자 호출**
```csharp
var note = new ShortNote(timing, targetLane);
```

**2단계: 필드 설정**
```csharp
note.nType = NoteType.SHORT;
note.nColor = NoteColor.BLUE;  // 또는 RED
```

**참고(코드 기준 색상 규칙):**
- 레인 4, 5 → `RED`
- 레인 9(OPEN/CLOSE) → `OPEN` (HoldNote)
- 그 외 → `BLUE`

**3단계: laneData에 추가**
```csharp
laneData[lane].Add(note);
```

### HoldNote 생성

**방법 1: 생성자에 duration 전달 (권장)**
```csharp
var note = new HoldNote(timing, NoteType.HOLD, NoteColor.BLUE, targetLane, duration);
// tickTime은 생성자 내부에서 자동 생성됨
```

**방법 2: 생성자에 tickTime 전달**
```csharp
var tickTimeArray = GenerateTickTimeArray(timing, duration);
var note = new HoldNote(timing, NoteType.HOLD, NoteColor.BLUE, targetLane, tickTimeArray);
```

**방법 3: 기본 생성자 + 필드 설정**
```csharp
var note = new HoldNote(timing, targetLane);
note.nType = NoteType.HOLD;
note.nColor = NoteColor.BLUE;
note.duration = duration;
note.tickTime = GenerateTickTimeArray(timing, duration);
note.tickLength = note.tickTime.Length;
note.tickJudge = new bool[note.tickLength];
note.isFinished = false;
note.isHeadJudged = false;
note.elapsedTick = 0;
```

### tickTime 배열 생성

**규칙**:
- 첫 틱 시작: `timing + 0.188초`
- 틱 간격: `0.094초`
- 마지막 틱: `timing + duration` 이전

**예시**:
```
timing = 91.500초
duration = 1.500초

첫 틱: 91.688초 (91.500 + 0.188)
틱 간격: 0.094초
틱들: 91.688, 91.782, 91.876, ..., 92.813
마지막 틱: 92.813초 (91.500 + 1.500 = 93.000 이전)
```

---

## 타입 추출 과정

### laneData에서 실제 타입 추출

**1단계: SXGTData 생성자 호출 시점에 접근**
```csharp
// SXGTData 생성자 후킹
private static void SXGTDataConstructorPostfix(ref object __instance)
{
    var laneData = __instance.laneData;
    // 이 시점에 laneData에 원본 노트가 있음
}
```

**2단계: 각 레인의 노트 확인**
```csharp
for (int lane = 0; lane <= 9; lane++)
{
    var noteList = laneData[lane];
    for (int i = 0; i < noteList.Count; i++)
    {
        var note = noteList[i];
        var actualType = note.GetType();  // 실제 타입 추출
        
        // 타입별로 저장
        if (actualType.Name.Contains("ShortNote"))
        {
            _shortNoteType = actualType;
        }
        else if (actualType.Name.Contains("HoldNote"))
        {
            _holdNoteType = actualType;
        }
    }
}
```

**3단계: 타입별 생성자 찾기**
```csharp
// ShortNote 생성자
var shortNoteConstructors = _shortNoteType.GetConstructors();

// HoldNote 생성자
var holdNoteConstructors = _holdNoteType.GetConstructors();
```

### 타입별 노트 생성

**일반 노트 (Normal) → ShortNote**
```csharp
var noteType = Hooks.SXGT.SXGTDataHook.GetShortNoteType();
var note = CreateGameNote(parsedNote, noteType);
```

**홀드 노트 (Long, Open) → HoldNote**
```csharp
var noteType = Hooks.SXGT.SXGTDataHook.GetHoldNoteType();
var note = CreateGameNote(parsedNote, noteType);
```

---

## 실행 시점

### BMS 파일 주입 타이밍

**✅ 올바른 시점: 플레이 로딩 씬에서 "커스텀 차트" 텍스트 감지 시**
```csharp
// TextHook.TextSetterPrefix에서 자동 감지
private static bool TextSetterPrefix(ref object __instance, ref string __0)
{
    // 플레이 로딩 씬에서만 처리
    if (currentScene.Contains("Play") || currentScene.Contains("Loading"))
    {
        if (__0.Contains("커스텀 차트"))
        {
            // Track ID 기반으로 BMS 파일 찾기 및 주입
            LoadAndInjectBmsForTrack(trackId);
        }
    }
}
```

**중복 주입 방지(코드 기준):**
- `TextHook`에는 `_bmsInjected` 플래그가 있어, 같은 씬에서 `"커스텀 차트"` 텍스트가 여러 번 세팅되어도 **BMS 파싱/세팅을 1회로 제한**합니다.
- 이 플래그는 `SceneDetector`가 씬 변경 시 `TextHook.ResetInjectionFlag()`로 리셋합니다.

### 원본 노트 제거 타이밍

**❌ 잘못된 시점: SXGTData 생성자 직후**
```csharp
// 문제: 노트가 아직 완전히 초기화되지 않음
private static void SXGTDataConstructorPostfix(ref object __instance)
{
    ClearAllNotes(__instance);  // ❌ 너무 이르다!
}
```

**✅ 올바른 시점: ManagerPlayHook 메서드 호출 시**
```csharp
// ManagerPlayHook.set_bms 호출 시
private static void SetBmsPostfix()
{
    SXGTDataHook.ProcessPendingNoteRemovalAndInjection();  // ✅ 올바른 시점
}

// ManagerPlayHook.FetchBMSToModules 호출 시
private static void FetchBMSToModulesPostfix()
{
    SXGTDataHook.ProcessPendingNoteRemovalAndInjection();  // ✅ 올바른 시점
}

// ManagerPlayHook.GetPatternFromDir 호출 시
private static void GetPatternFromDirPostfix()
{
    SXGTDataHook.ProcessPendingNoteRemovalAndInjection();  // ✅ 올바른 시점
}
```

### 썸네일 및 데모 주입 타이밍

**✅ 올바른 시점: MusicSelect 씬에서 트랙 선택 변경 시**
```csharp
// ManagerMusicSelectHook.ChangeTrackCursorPostfix
private static void ChangeTrackCursorPostfix(object __instance, int delta)
{
    // 커스텀 트랙인 경우 썸네일 및 demo.ogg 주입
    InjectThumbnailAndDemo(__instance);
}
```

### 실행 순서

```
1. SXGTData 생성자 호출
   └─ SXGTDataHook.SXGTDataConstructorPostfix
      └─ 인스턴스만 저장 (제거는 하지 않음)

2. ManagerPlay.set_bms 호출
   └─ ManagerPlayHook.SetBmsPostfix
      ├─ (커스텀 트랙인 경우) TextHook.LoadAndInjectBmsForTrack(...)로 BMS 재탐색/재파싱
      ├─ BGAPlayerHook / BGMPlayerHook로 미디어 교체 1회 시도
      └─ SXGTDataHook.ProcessPendingNoteRemovalAndInjection
         ├─ 원본 노트 제거 (ClearAllNotes) + 타입/생성자 캐시
         ├─ 커스텀 차트 주입 (InjectBmsNotesToLaneData)
         └─ totalNotes/totalNoteWithTicks 갱신
```

---

## 주요 메서드 호출 순서

### MusicSelect 씬 → 트랙 주입

```
1. MusicSelect 씬 로드
   ↓
2. SceneDetector.OnSceneLoaded (씬 변경 감지)
   └─ MusicSelectAnalyzer.AnalyzeMusicSelectScene (짧은 지연)
      ├─ ManagerMusicSelect 인스턴스 찾기
      ├─ trackDatas 리스트에서 첫 번째 TrackData 복사
      ├─ TrackInfoParser.ParseTrackInfo() (txt 파일 파싱)
      │  ├─ 제목 파싱
      │  ├─ 아티스트 파싱
      │  └─ 난이도 파싱
      ├─ TrackData 필드 설정
      │  ├─ DisplayName 설정 (인스턴스 필드 `DisplayName`에 SetValue, `TrackDataAnalyzer.Inject`)
      │  ├─ Artist 설정
      │  └─ Level/Level_LITE 배열 설정
      └─ trackDatas 리스트에 추가
   ↓
3. 트랙 선택 변경 (ManagerMusicSelectHook.ChangeTrackCursorPostfix)
   ├─ 현재 선택된 트랙 정보 확인
   └─ 커스텀 트랙인 경우 썸네일 주입
   ↓
4. PlayPreview 호출 (게임 내부)
   └─ ManagerMusicSelectHook.PlayPreviewPrefix (후킹)
      ├─ 커스텀 트랙 확인
      ├─ 원래 preview 재생 차단 (return false)
      └─ 커스텀 음악 재생
         ├─ BGM 소스 찾기 및 뮤트
         ├─ 앨범 폴더에서 음악 파일 찾기
         │  ├─ 1순위: demo.ogg, demo.mp3, demo.wav
         │  └─ 2순위: music.ogg, music.mp3, music.wav
         └─ 코루틴으로 비동기 로드 및 재생
```

### 게임 시작 → 노트 생성

```
4. 플레이 로딩 씬
   ↓
5. TextHook.TextSetterPrefix (텍스트 설정 감지)
   └─ "커스텀 차트" 텍스트 감지 시
      ├─ ManagerMusicSelect.currentSelectedTrack.ID 가져오기
      ├─ hwa 폴더에서 BMS 파일 찾기 (Track ID 기반)
      ├─ BmsParser.ParseBmsFile() 호출
      └─ CustomChartInjector.SetParsedBmsNotes() 설정
   ↓
6. ManagerPlay.FetchBMSToModules() 또는 GetPatternFromDir()
   ↓
7. SXGTReader.ReadBMSFile()
   ↓
8. SXGTData 생성자 호출
   ├─ laneData 초기화
   ├─ 원본 노트 추가
   └─ SXGTDataHook.SXGTDataConstructorPostfix (후킹)
      └─ 인스턴스 저장 (제거는 하지 않음)
   ↓
9. ManagerPlay.set_bms(SXGTData)
   └─ ManagerPlayHook.SetBmsPostfix (후킹)
      └─ SXGTDataHook.ProcessPendingNoteRemovalAndInjection
         ├─ 원본 노트 제거
         │  ├─ laneData에서 실제 타입 추출
         │  └─ ShortNote/HoldNote 타입 저장
         └─ 커스텀 차트 주입
            ├─ 타입별 노트 생성
            │  ├─ ShortNote: GetShortNoteType() 사용
            │  └─ HoldNote: GetHoldNoteType() 사용
            └─ laneData에 추가
```

### 노트 생성 상세 과정

```
CreateGameNote(parsedNote, noteDataType)
├─ 노트 타입 확인 (Normal vs Long/Open)
├─ 타입 선택
│  ├─ Normal → ShortNote 타입
│  └─ Long/Open → HoldNote 타입
├─ 생성자 찾기
│  ├─ 홀드 노트: duration/tickTime 받는 생성자 우선
│  └─ 일반 노트: 기본 생성자
├─ 생성자 호출
│  ├─ ShortNote: (timing, targetLane)
│  └─ HoldNote: (timing, nType, nColor, targetLane, duration) 또는 (timing, targetLane)
├─ 필드 설정
│  ├─ nType, nColor
│  └─ 홀드 노트: duration, tickTime, tickLength, tickJudge 등
└─ 반환
```

---

## 핵심 포인트 요약

### 1. 타입 구조
- **base 타입**: `RhythmGame.Note`
- **파생 타입**: `RhythmGame.ShortNote`, `RhythmGame.HoldNote`
- **laneData**: `Dictionary<int, List<Note>>` (base 타입 사용)

### 2. 타입 추출
- **시점**: SXGTData 생성자 호출 후, 원본 노트 제거 전
- **방법**: `note.GetType()`으로 실제 타입 확인
- **저장**: 타입별로 정적 변수에 저장

### 3. 노트 생성
- **일반 노트**: `ShortNote` 타입 사용
- **홀드 노트**: `HoldNote` 타입 사용
- **생성자**: 홀드 노트는 duration 또는 tickTime을 생성자에 전달

### 3-1. 노트 색상 규칙(코드 기준)
- 레인 9(OPEN/CLOSE) → `OPEN`
- 레인 4, 5 → `RED`
- 그 외 → `BLUE`

### 4. 실행 시점
- **원본 노트 제거**: ManagerPlayHook 메서드 호출 시점
- **커스텀 차트 주입**: 원본 노트 제거 직후

### 4-1. 스코어/종료 판정 값 갱신(현재 코드 기준)
- `CustomChartInjector`가 주입된 노트 수를 집계합니다.
- `SXGTData.totalNotes`와 `totalNoteWithTicks`를 갱신합니다.
- `MaxScore`, `targetBestScore` 직접 보정은 사용하지 않습니다.

> 참고: `NumberInterpolatorHook`은 현재 코드에서 비활성화되어 있습니다.

### 5. tickTime 생성 규칙
- 첫 틱: `timing + 0.188초`
- 간격: `0.094초`
- 마지막 틱: `timing + duration` 이전

### 6. BGA/BGM 동기화(현재 코드 기준)
- 교체 이후 `BGABGMSyncHook`이 짧은 내부 주기로 BGA/BGM 시간 차이를 측정하고, 오차 범위에 따라 Soft Sync(속도 조절) 또는 Hard Sync(강제 이동)를 적용합니다.

---

## 기술 스택 및 DLL 참조

### 프로젝트 설정

- **타겟 프레임워크**: .NET Framework 4.7.2
- **프로젝트 파일**: `sxtg2-mod/sxtg2.csproj`

### 어셈블리 어트리뷰트

```csharp
[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", "1.0.0", "Meowzter")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)] // Purple (R, G, B, A)
```

**위치**: `sxtg2-mod/Main/` (엔트리: `Main.cs`)

### DLL 참조

#### MelonLoader 관련
- **MelonLoader.dll**: MelonLoader 프레임워크
  - 경로: `{게임 설치 폴더}\MelonLoader\net35\MelonLoader.dll`
- **0Harmony.dll**: Harmony 패칭 라이브러리
  - 경로: `{게임 설치 폴더}\MelonLoader\net35\0Harmony.dll`

#### Unity Engine 관련
모든 Unity DLL은 `{게임 설치 폴더}\Sixtar Gate STARTRAIL_Data\Managed\` 경로에 있습니다.

- **UnityEngine.dll**: Unity 엔진 핵심
- **UnityEngine.CoreModule.dll**: Unity 코어 모듈
- **UnityEngine.AudioModule.dll**: 오디오 모듈
- **UnityEngine.VideoModule.dll**: 비디오 모듈
- **UnityEngine.UnityWebRequestModule.dll**: 웹 요청 모듈
- **UnityEngine.UnityWebRequestAudioModule.dll**: 오디오 웹 요청 모듈
- **UnityEngine.UnityWebRequestTextureModule.dll**: 텍스처 웹 요청 모듈
- **UnityEngine.InputLegacyModule.dll**: 입력 모듈
- **UnityEngine.ImageConversionModule.dll**: 이미지 변환 모듈
- **UnityEngine.UI.dll**: Unity UI 모듈

**프로젝트 파일 참조**: `sxtg2-mod/sxtg2.csproj`

---

## 관련 문서

- [BMS_PARSING.md](BMS_PARSING.md): BMS 파일 파싱 상세
- [IMPLEMENTATION.md](IMPLEMENTATION.md): 구현 상세 및 후킹 과정
- [DOCUMENTATION.md](DOCUMENTATION.md): 종합 참조 문서




















