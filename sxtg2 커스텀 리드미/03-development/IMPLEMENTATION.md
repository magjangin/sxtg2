# 구현 상세

이 문서는 **`sxtg2` 모드(현재 솔루션/빌드 대상)**의 구현 상세와 후킹 과정을 설명합니다.

## 목차

1. [후킹 전략](#후킹-전략)
2. [타입 추출 구현](#타입-추출-구현)
3. [노트 주입 구현](#노트-주입-구현)
4. [생성자 호출 구현](#생성자-호출-구현)
5. [필드 설정 구현](#필드-설정-구현)
6. [BGA/BGM 교체 구현](#bgabgm-교체-구현)

---

## 후킹 전략

### Harmony 패치 사용

모드는 Harmony 라이브러리를 사용하여 게임 메서드를 후킹합니다.

**초기화 패턴:**
```csharp
public static void Initialize()
{
    if (_isInitialized) return;
    
    var harmony = new Harmony("sxtg2.HookName");
    var targetType = TypeFinderHelper.FindType("TargetType");
    var targetMethod = targetType.GetMethod("TargetMethod");
    var postfix = new HarmonyMethod(typeof(HookClass).GetMethod("PostfixMethod"));
    
    harmony.Patch(targetMethod, postfix: postfix);
    _isInitialized = true;
}
```

### 주요 후킹 포인트

1. **SXGTData 생성자**
   - 목적: SXGTData 인스턴스 저장
   - 위치: `SXGTDataHook.SXGTDataConstructorPostfix`
   - 동작: 인스턴스만 저장, 실제 처리는 나중에

2. **ManagerPlay 메서드들**
   - `set_bms`: 차트 데이터 설정 시점
   - `FetchBMSToModules`: BMS 파일 로드 시점
   - `GetPatternFromDir`: 패턴 로드 시점
   - 위치: `ManagerPlayHook`
   - 동작: 원본 노트 제거 및 커스텀 차트 주입 트리거

3. **텍스트 설정 (UnityEngine.UI.Text, TMPro.TextMeshProUGUI)**
   - 목적: "커스텀 차트" 텍스트 감지 및 BMS 자동 주입
   - 위치: `TextHook.TextSetterPrefix`
   - 동작: 플레이 로딩 씬에서 "커스텀 차트" 텍스트 감지 시 BMS 파일 자동 로드 및 주입

4. **ManagerMusicSelect 메서드들**
   - `ChangeTrackCursor`: 트랙 선택 변경 시점
   - `ChangeLevelCursor`: 난이도 선택 변경 시점
   - 위치: `ManagerMusicSelectHook`
   - 동작: 커스텀 트랙 선택 시 썸네일 및 demo.ogg 주입

5. **씬 로드 이벤트**
   - 목적: 씬 변경 감지 및 자동 분석
   - 위치: `SceneDetector.OnSceneLoaded`
   - 동작: MusicSelect 씬 로드 시 TrackData 자동 분석 및 주입

---

## 타입 추출 구현

### laneData에서 실제 타입 추출

**위치:** `SXGTDataHook.ClearAllNotes()`

**과정:**

1. **laneData 접근**
   ```csharp
   var laneDataField = sxgtDataType.GetField("laneData", ...);
   var laneData = laneDataField.GetValue(sxgtDataInstance);
   ```

2. **각 레인의 노트 확인**
   ```csharp
   for (int lane = 0; lane <= 9; lane++)
   {
       var noteList = itemProp.GetValue(laneData, new object[] { lane });
       var itemProperty = noteList.GetType().GetProperty("Item");
       
       for (int i = 0; i < beforeCount; i++)
       {
           var note = itemProperty.GetValue(noteList, new object[] { i });
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

3. **타입 저장**
   ```csharp
   private static Type _shortNoteType = null;
   private static Type _holdNoteType = null;
   ```

4. **타입 반환 메서드**
   ```csharp
   public static Type GetShortNoteType()
   {
       return _shortNoteType ?? _actualNoteType;
   }
   
   public static Type GetHoldNoteType()
   {
       return _holdNoteType ?? _actualNoteType;
   }
   ```

---

## 노트 주입 구현

### 주입 과정

**위치:** `CustomChartInjector.InjectBmsNotesToLaneData()`

**1단계: 실제 노트 타입 확인**
```csharp
// laneData에서 추출한 실제 노트 타입 사용
var actualNoteType = Hooks.SXGT.SXGTDataHook.GetActualNoteType();
if (actualNoteType != null)
{
    noteDataType = actualNoteType;
}

// **최적화 (2025.12)**: 
// 반복적인 Reflection(`Assembly.GetTypes`)을 피하기 위해 `NoteFactory`에서 Enum 타입과 값(SHORT, HOLD, RED 등)을 캐싱합니다.
CustomChartInjector.CacheEnums(actualNoteType);
```

**2단계: laneData의 실제 제네릭 타입 확인**
```csharp
var noteList = itemProp.GetValue(laneData, new object[] { lane });
var noteListType = noteList.GetType();
Type actualListElementType = null;

if (noteListType.IsGenericType)
{
    var genericArgs = noteListType.GetGenericArguments();
    if (genericArgs.Length > 0)
    {
        actualListElementType = genericArgs[0];  // List<T>의 T
    }
}
```

**3단계: 노트 생성 및 추가**
```csharp
foreach (var parsedNote in notes)
{
    // 노트 타입에 따라 적절한 타입으로 생성
    var gameNote = CreateGameNote(parsedNote, noteDataType);
    
    // laneData에 추가 (자동 캐스팅됨)
    addMethod.Invoke(noteList, new object[] { gameNote });
}
```

---

## 생성자 호출 구현

### 타입별 생성자 찾기

**위치:** `CustomChartInjector.CreateGameNote()`

**1단계: 노트 타입 확인**
```csharp
bool isHoldNote = parsedNote.NoteType == BmsParser.NoteType.Long || 
                 parsedNote.NoteType == BmsParser.NoteType.Open;
```

**2단계: 적절한 타입 선택**
```csharp
Type actualNoteType = noteDataType;
if (isHoldNote)
{
    var holdNoteType = Hooks.SXGT.SXGTDataHook.GetHoldNoteType();
    if (holdNoteType != null)
    {
        actualNoteType = holdNoteType;
    }
}
else
{
    var shortNoteType = Hooks.SXGT.SXGTDataHook.GetShortNoteType();
    if (shortNoteType != null)
    {
        actualNoteType = shortNoteType;
    }
}
```

**3단계: 홀드 노트 생성자 우선 찾기**
```csharp
if (isHoldNote && parsedNote.Length > 0f)
{
    var tickTimeArray = GenerateTickTimeArray(parsedNote.Time, parsedNote.Length);
    
    // duration 또는 tickTime을 받는 생성자 찾기
    var holdCtor = constructors.FirstOrDefault(c =>
    {
        var parameters = c.GetParameters();
        if (parameters.Length >= 5)
        {
            var lastParam = parameters[parameters.Length - 1];
            return lastParam.ParameterType == typeof(float) || 
                   lastParam.ParameterType == typeof(float[]);
        }
        return false;
    });
    
    if (holdCtor != null)
    {
        // 생성자에 duration 또는 tickTime 전달
        var parameters = holdCtor.GetParameters();
        var paramValues = new List<object> { parsedNote.Time, nTypeValue, nColorValue, parsedNote.Lane };
        
        var lastParam = parameters[parameters.Length - 1];
        if (lastParam.ParameterType == typeof(float))
        {
            paramValues.Add(parsedNote.Length);  // duration 전달
        }
        else if (lastParam.ParameterType == typeof(float[]))
        {
            paramValues.Add(tickTimeArray);  // tickTime 전달
        }
        
        noteInstance = holdCtor.Invoke(paramValues.ToArray());
    }
}
```

**4단계: 기본 생성자 시도**
```csharp
// 홀드 노트 생성자가 없으면 기본 생성자 사용
if (noteInstance == null)
{
    var ctor2 = constructors.FirstOrDefault(c => c.GetParameters().Length == 2);
    if (ctor2 != null)
    {
        noteInstance = ctor2.Invoke(new object[] { parsedNote.Time, parsedNote.Lane });
    }
}
```

---

## 필드 설정 구현

### 대소문자 무시 필드 찾기

**위치:** `CustomChartInjector.FindFieldCaseInsensitive()`

```csharp
private static FieldInfo FindFieldCaseInsensitive(Type type, string fieldName)
{
    if (type == null || string.IsNullOrEmpty(fieldName))
        return null;

    // 먼저 정확한 이름으로 찾기
    var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    if (field != null)
        return field;

    // 대소문자 무시하고 찾기
    var allFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    return allFields.FirstOrDefault(f => 
        string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));
}
```

### 필드 설정

**위치:** `CustomChartInjector.SetNoteField()`

```csharp
private static void SetNoteField(object instance, Type type, string fieldName, object value)
{
    try
    {
        if (instance == null || type == null || value == null)
            return;

        // 대소문자 무시하고 필드 찾기
        var field = FindFieldCaseInsensitive(type, fieldName);
        if (field != null)
        {
            field.SetValue(instance, value);
        }
    }
    catch
    {
        // 필드 설정 실패는 무시
    }
}
```

### 홀드 노트 필드 설정

```csharp
// duration
SetNoteField(noteInstance, actualNoteType, "duration", parsedNote.Length);

// tickTime 배열
var tickTimeArray = GenerateTickTimeArray(parsedNote.Time, parsedNote.Length);
SetNoteField(noteInstance, actualNoteType, "tickTime", tickTimeArray);
SetNoteField(noteInstance, actualNoteType, "tickLength", tickTimeArray.Length);

// tickJudge 배열
var tickJudgeArray = new bool[tickTimeArray.Length];
SetNoteField(noteInstance, actualNoteType, "tickJudge", tickJudgeArray);

// 상태 필드
SetNoteField(noteInstance, actualNoteType, "isFinished", false);
SetNoteField(noteInstance, actualNoteType, "isHeadJudged", false);
SetNoteField(noteInstance, actualNoteType, "elapsedTick", 0);
```

### TrackData 난이도 필드 수정 (✅ 구현 완료)

**위치:** `TrackDataAnalyzer.SetDifficultyFields()`

**목적:** 
- `track_info.txt`에서 파싱한 커스텀 난이도를 게임의 TrackData 객체에 반영
- 곡 선택 화면에서 올바른 난이도 표시 보장

**과정:**

**1단계: 난이도 정보 파싱**
```csharp
// TrackInfoParser.cs에서 track_info.txt 파싱
// 예: "난이도: 7, 9, 11, 13" → List<int> { 7, 9, 11, 13 }
private static List<int> ParseDifficulties(string value)
{
    var difficulties = new List<int>();
    var parts = value.Split(new[] { ',', '/' }, StringSplitOptions.RemoveEmptyEntries);
    
    foreach (var part in parts)
    {
        string trimmed = part.Trim();
        if (int.TryParse(trimmed, out int difficulty))
        {
            difficulties.Add(difficulty);
        }
    }
    
    return difficulties;
}
```

**2단계: TrackData 필드 탐색**
```csharp
// 리플렉션으로 TrackData의 난이도 관련 필드 찾기
// 주요 타겟 필드: Level, Level_LITE
string[] targetFieldNames = { "Level", "Level_LITE" };

foreach (var fieldName in targetFieldNames)
{
    FieldInfo field = trackDataType.GetField(fieldName, 
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    
    if (field != null)
    {
        // 필드 타입에 따라 처리
    }
}
```

**3단계: 배열 타입 변환 및 할당**

난이도 필드는 다양한 타입을 가질 수 있습니다:

```csharp
// A. String 배열인 경우 (게임의 일반적인 형식)
if (elementType == typeof(string))
{
    // 2자리 패딩 형식으로 변환: 7 → "07", 11 → "11"
    string[] difficultyArray = new string[parsedDifficulties.Count];
    for (int i = 0; i < parsedDifficulties.Count; i++)
    {
        difficultyArray[i] = parsedDifficulties[i].ToString("D2");
    }
    
    // TrackData 객체에 할당
    field.SetValue(trackData, difficultyArray);
    MelonLogger.Msg($"  난이도 배열 설정: {field.Name} (String[]) = [{string.Join(", ", difficultyArray)}]");
}

// B. 숫자 배열인 경우 (int[], byte[], short[] 등)
else if (elementType == typeof(int) || elementType == typeof(byte) || elementType == typeof(short))
{
    // 적절한 숫자 타입의 배열 생성
    Array difficultyArray = Array.CreateInstance(elementType, parsedDifficulties.Count);
    for (int i = 0; i < parsedDifficulties.Count; i++)
    {
        difficultyArray.SetValue(Convert.ChangeType(parsedDifficulties[i], elementType), i);
    }
    
    field.SetValue(trackData, difficultyArray);
    MelonLogger.Msg($"  난이도 배열 설정: {field.Name} ({elementType.Name}[]) = [{string.Join(", ", parsedDifficulties)}]");
}
```

**4단계: 추가 난이도 필드 처리**
```csharp
// Level, Level_LITE 외에도 다른 난이도 관련 필드가 있을 수 있음
foreach (var field in fields)
{
    string fieldName = field.Name.ToLower();
    
    // "level" 또는 "difficulty"를 포함하는 필드 찾기
    if ((fieldName.Contains("level") || fieldName.Contains("difficulty")) && 
        !targetFieldNames.Contains(field.Name, StringComparer.OrdinalIgnoreCase))
    {
        // 위와 동일한 변환 로직 적용
    }
}
```

**실행 타이밍:**
```csharp
// MusicSelect 씬 로드 시 자동 실행
// SceneDetector → MusicSelectAnalyzer → TrackDataAnalyzer
// 1. CloneAndModifyTrackData()에서 원본 TrackData 복제
// 2. SetDifficultyFields()로 난이도 수정
// 3. 수정된 TrackData를 TrackList에 다시 설정
```

**로그 예시:**
```
[SetDifficultyFields] 난이도 배열 할당 시작: [7, 9, 11, 13]
  난이도 배열 설정: Level (String[]) = [07, 09, 11, 13]
  난이도 배열 설정: Level_LITE (String[]) = [07, 09, 11, 13]
```

**주의사항:**
- ⚠️ **참조 타입 특성**: TrackData는 참조 타입이므로, 복제된 객체의 필드를 수정하면 TrackList에 반영됨
- ⚠️ **배열 길이**: 난이도 배열의 길이는 원본과 달라질 수 있음 (4개 → 5개 등)
- ⚠️ **타입 안전성**: `Convert.ChangeType()`으로 다양한 숫자 타입 지원
- ✅ **2자리 패딩**: String 배열은 "07", "09" 형식으로 게임 UI와 호환

---

## BGA/BGM 교체 구현

### 플레이 시작 오케스트레이션

**위치:** `CustomPlayStartupFlow.cs`

`ManagerPlayHook.PlayStart.cs`의 `OnPlaySceneStart(...)`는 이제 커스텀 플레이 시작 흐름을 직접 길게 처리하지 않고 `CustomPlayStartupFlow.Run(managerPlayInstance, sourceMethodName)`에 위임합니다.

**과정:**
1. `CustomPlayContext.TryResolveCurrentCustomTrack(...)`로 현재 선택된 커스텀 트랙과 앨범 폴더를 확정합니다.
2. 일반 트랙이면 선택 상태와 대기 중인 `SXGTData`를 정리하고 종료합니다.
3. 커스텀 트랙이면 `CustomTrackHelper.SetSelectedTrack(...)`로 현재 트랙 상태를 갱신합니다.
4. `TextHook.LoadAndInjectBmsForTrack(trackId, displayName)`로 플레이 직전 BMS를 다시 로드합니다.
5. BGA/BGM 교체 플래그를 리셋하고 `ManagerPlay.bgm` 필드가 있으면 `BGMPlayerHook`에 넘깁니다.
6. `BGAPlayerHook.ReplacePlaySceneBGA(albumFolder)` / `BGMPlayerHook.ReplacePlaySceneBGM(albumFolder)`를 호출합니다.
7. `HighscoreMeterHook.ApplyForCustomChart()`와 `SXGTDataHook.ProcessPendingNoteRemovalAndInjection()`을 순서대로 실행합니다.

### BGA 교체

**위치:** `BGAPlayerHook.cs`

**과정:**
1. 초기화 시 `InitialMediaFileScanner.FindFirstBga(...)`로 hwa 루트의 첫 BGA를 스캔합니다.
2. 플레이 시작 흐름(`CustomPlayStartupFlow`)에서 `ResetReplacementFlag()` 호출로 상태를 리셋합니다.
3. `BgaFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false)`로 현재 커스텀 앨범 폴더의 `*.mp4`를 선택합니다.
4. `BgaVideoPlayerFinder.Find()`로 교체 대상 `VideoPlayer`를 찾고, 아래 값을 설정합니다.
   - `videoPlayer.url = "file://..."`  
   - `videoPlayer.source = VideoSource.Url`  
   - `videoPlayer.clip = null` (URL 스트리밍 사용)
5. 교체 성공 시 내부 플래그를 세팅하여 중복 교체를 막습니다.

**동기화(중요):**
- 교체 이후에는 `Main.OnUpdate()`에서 `BGABGMSyncHook.CheckAndSync()`가 **짧은 내부 간격(기준값)**으로 BGA/BGM 재생 시간을 비교합니다.
- **오차 보정 로직 (Soft/Hard Sync)**:
  - **소-중간 오차 구간**: `playbackSpeed`를 조절하여 부드럽게 맞추는 **Soft Sync**를 사용 (화면 튐 방지).
  - **큰 오차 구간**: `time`을 강제로 맞추는 **Hard Sync**를 사용 (즉시 동기화).

### BGM 교체

**위치:** `BGMPlayerHook.cs`

**과정:**
1. 초기화 시 `InitialMediaFileScanner.FindFirstBgm(...)`로 hwa 루트의 첫 BGM을 스캔합니다.
2. 플레이 시작 흐름(`CustomPlayStartupFlow`)에서 `ResetReplacementFlag()`로 상태를 리셋합니다.
3. `BgmFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false)`로 현재 커스텀 앨범 폴더의 오디오 파일을 선택합니다.
   - 우선순위: `*.ogg` → `*.mp3` → `*.wav`
4. `BgmAudioSourceFinder.Find(_managerPlayBGM)`로 대상 `AudioSource`를 선택합니다.
   - 가능하면 `ManagerPlay`의 `bgm` 필드를 우선 사용
   - 없으면 씬 내 `AudioSource`를 탐색(재생 중/클립 보유 우선)
5. `BGMPlayerHook.Playback.cs`의 코루틴으로 비동기 로드 및 교체를 수행합니다.
   - `UnityWebRequest` + `DownloadHandlerAudioClip(streamAudio=true)`로 스트리밍 로드
   - 성공하면 `audioSource.clip` 교체, `loop=true`, `Play()`
6. 로딩 중에는 `_isLoading` 플래그로 중복 교체를 막습니다.

## 텍스트 후킹 구현

### 텍스트 설정 감지

**위치:** `TextHook.cs`

**과정:**
1. 초기화 시 `UnityEngine.UI.Text`와 `TMPro.TextMeshProUGUI`의 `text` 속성 setter 후킹
2. 텍스트 설정 시 현재 씬 확인
3. 플레이 로딩 씬에서만 처리 (씬 이름에 "Play" 또는 "Loading" 포함)
4. "커스텀 차트" 텍스트 감지
5. `FindCurrentTrackId()`로 현재 선택된 Track ID 가져오기
   - `ManagerMusicSelect` 인스턴스 찾기
   - `currentSelectedTrack` 속성/필드에서 TrackData 가져오기
   - TrackData의 `ID` 필드/속성 가져오기
6. `LoadAndInjectBmsForTrack()` 호출
   - DisplayName 기반으로 앨범 폴더 찾기
   - BMS 파일 찾기 (앨범 폴더 우선, Track ID 기반)
   - `BmsParser.ParseBmsFile()` 호출
   - `CustomChartInjector.SetParsedBmsNotes()` 설정

**코드 예시:**
```csharp
public static bool TextSetterPrefix(ref object __instance, ref string __0)
{
    string currentScene = SceneManager.GetActiveScene().name;
    bool isPlayLoadingScene = currentScene.Contains("Play") || 
                             currentScene.Contains("Loading");
    
    if (isPlayLoadingScene && __0.Contains("커스텀 차트"))
    {
        string trackId = FindCurrentTrackId();
        LoadAndInjectBmsForTrack(trackId);
    }
    
    return true;
}
```

## 씬 감지 구현

### 씬 변경 감지

**위치:** `sxtg2-mod/Features/SceneDetector/`

**과정:**
1. 초기화 시 `SceneManager.sceneLoaded` 이벤트 구독
2. 씬 로드 시 `OnSceneLoaded()` 호출
3. MusicSelect 씬 감지 시 `MusicSelectAnalyzer.AnalyzeMusicSelectScene()` 호출 (짧은 지연)
4. 주기적으로 씬 체크 (짧은 간격, 이벤트가 작동하지 않는 경우 대비)

**코드 예시:**
```csharp
private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
{
    _currentSceneName = scene.name;
    
    if (scene.name.Contains("MusicSelect"))
    {
        _musicSelectAnalysisDelay = SceneDetectionDefaults.AnalysisDelaySeconds; // 기준 지연 후 분석
    }
}
```

## Steam 업데이트 차단 구현

### Steam 매니페스트 잠금

**위치:** `SteamManifestLock.cs`

**목적:**
- Steam이 게임을 자동으로 업데이트하는 것을 방지
- 게임 버전이 변경되면 모드 호환성 문제가 발생할 수 있으므로 업데이트 차단

**과정:**
1. 게임 설치 경로에서 `steamapps` 폴더 찾기
   - `Application.dataPath`의 상위 폴더가 `common` 폴더인지 확인
   - `common` 폴더의 상위 폴더가 `steamapps` 폴더
2. Steam 매니페스트 파일 경로 구성
   - 파일명: `appmanifest_1802720.acf` (App ID: 1802720)
   - 경로: `{steamapps 폴더}\appmanifest_1802720.acf`
3. 파일 속성을 읽기 전용으로 설정
   - 파일이 존재하는 경우에만 처리
   - 이미 읽기 전용이면 로그만 출력
   - 읽기 전용이 아니면 `FileAttributes.ReadOnly` 추가

**코드 예시:**
```csharp
public static void Lock()
{
    // 게임 설치 경로 가져오기
    string gamePath = Path.GetDirectoryName(Application.dataPath);
    
    // steamapps 폴더 찾기
    DirectoryInfo commonDir = Directory.GetParent(gamePath);
    if (commonDir == null || commonDir.Name != "common") return;
    
    DirectoryInfo steamappsDir = commonDir.Parent;
    if (steamappsDir == null) return;
    
    string manifestPath = Path.Combine(steamappsDir.FullName, ManifestFileName);
    
    if (File.Exists(manifestPath))
    {
        FileAttributes attributes = File.GetAttributes(manifestPath);
        if ((attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
        {
            File.SetAttributes(manifestPath, attributes | FileAttributes.ReadOnly);
            MelonLogger.Msg($"[SteamManifestLock] 매니페스트 파일을 읽기 전용으로 잠갔습니다: {manifestPath}");
        }
    }
}
```

**호출 시점:**
- `Main.OnInitializeMelon()`에서 초기화 시점에 호출
- TextHook 초기화 직후, 다른 Hook 초기화 전에 실행

**참고:**
- 매니페스트 파일을 읽기 전용으로 설정하면 Steam이 게임을 업데이트하지 않습니다
- 수동으로 업데이트하려면 파일 속성에서 읽기 전용을 해제해야 합니다

## TrackData 주입 구현

### TrackData 복사 및 주입

**위치:** `TrackDataAnalyzer.cs` 계열

**과정:**
1. MusicSelect 씬에서 첫 번째 TrackData 복사
2. 기본 생성자로 새 인스턴스 생성
3. 모든 필드 복사 (배열, Dictionary 포함)
4. `TrackInfoParser.ParseTrackInfo()`로 txt 파일 파싱
5. DisplayName, Artist, 난이도 필드 설정
6. `trackDatas` 리스트에 추가

**분리된 파일:**
- `TrackDataAnalyzer.AlbumFolders.cs`: 앨범 폴더 열거 및 선택
- `TrackDataAnalyzer.TrackInfo.cs`: `track_info.txt` 파싱 결과를 TrackData에 적용
- `TrackDataAnalyzer.TrackList.cs`: TrackList/trackDatas 반영
- `TrackDataAnalyzer.Difficulty.cs`: 난이도 배열 변환 및 설정
- `TrackDataAnalyzer.Inject.cs`: TrackData 복제 및 주입 흐름

### TrackData.DisplayName (필드·읽기)

- **주입(쓰기):** `sxtg2-mod/Features/TrackDataAnalyzer.Inject.cs`의 `ApplyTrackInfo()`는 인스턴스 **필드** `DisplayName`만 `GetField`로 찾아 `SetValue` 한다. 프로퍼티 setter는 쓰지 않는다. Sixtar Gate STARTRAIL 대상 빌드에서 런타임으로 필드 존재·설정이 확인되었다.
- **읽기:** `ReflectionHelper.GetFirstMemberValueSafe` 등은 후보 이름(`DisplayName`, `displayName`, `Title`)마다 **필드를 먼저**, 없으면 **동일 이름 프로퍼티 getter**를 시도한다. 예: `ManagerMusicSelectHook.Preview.cs`의 `PlayPreviewPrefix`도 필드 우선·없으면 프로퍼티.

**txt 파일 형식:**
```
제목: 곡 제목
아티스트: 작곡가 이름
난이도: 1,2,3
```

## 썸네일 및 데모 주입 구현

### 썸네일 주입

**위치:** `ManagerMusicSelectHook.Thumbnail.cs`

**과정:**
1. 트랙 선택 변경 시 `ChangeTrackCursorPostfix()` 호출
2. 커스텀 트랙 확인 (`CustomTrackHelper.IsCustomTrack()`)
3. `ThumbnailLoader.LoadThumbnail()` 호출
   - 앨범 폴더 우선 검색
   - 파일명 패턴: `thumb.png`, `thumbnail.png`, `jacket.png`, `{trackId}_thumb.png` 등
   - `Texture2D.LoadImage()`로 이미지 로드
   - `Sprite.Create()`로 Sprite 생성
4. `FindFullsizeJacketImages()`로 타겟 이미지 찾기
   - "Jacket Image" 이름을 가진 이미지 중
   - 경로에 "Fullsize Jacket"가 포함된 경우만 선택
5. `ApplyThumbnailToImages()`로 썸네일 설정
   - `targetImage.sprite = sprite` (크기 조정 없음, 원본 크기 그대로)

### 데모 음악 주입

**위치:** `ManagerMusicSelectHook.Demo.cs` 계열

**과정:**
1. `ManagerMusicSelectHook.Demo.FileResolver.cs`가 커스텀 앨범 폴더에서 `demo.*` 파일을 찾습니다.
2. `ManagerMusicSelectHook.Demo.AudioSource.cs`가 재생 대상 `AudioSource`를 찾습니다.
3. `ManagerMusicSelectHook.Demo.cs`가 코루틴으로 파일을 로드하고 `AudioSource.clip` 설정 및 재생을 수행합니다.
4. preview 쪽은 `ManagerMusicSelectHook.Preview.FileResolver.cs`, `.Preview.AudioSources.cs`, `.Preview.Audio.cs`로 각각 파일 선택, AudioSource 탐색/뮤트, 비동기 로드/재생을 나눠 처리합니다.

---

## 주요 코드 위치

### 타입 추출
- `SXGTDataHook.ClearAllNotes()`: laneData에서 실제 타입 추출
- `SXGTDataHook.GetShortNoteType()`: ShortNote 타입 반환
- `SXGTDataHook.GetHoldNoteType()`: HoldNote 타입 반환
- `SXGTDataHook.ExtractNoteDataTypeFromLaneData()`: laneData에서 Note 타입 추출

### 노트 생성
- `CustomChartInjector.CreateGameNote()`: 게임 노트 생성 고수준 흐름
- `CustomChartInjector.NoteConstructors.cs`: Short/Hold 생성자 선택 및 호출
- `CustomChartInjector.NoteEnums.cs`: 게임 enum 타입/값 캐시
- `CustomChartInjector.FindFieldCaseInsensitive()`: 대소문자 무시 필드 찾기
- `CustomChartInjector.SetNoteField()`: 필드 설정
- `CustomChartInjector.GenerateTickTimeArray()`: tickTime 배열 생성

### 후킹
- `SXGTDataHook.SXGTDataConstructorPostfix`: SXGTData 생성자 후킹
- `SXGTDataHook.ProcessPendingNoteRemovalAndInjection()`: 원본 노트 제거 및 주입 실행
- `ManagerPlayHook.SetBmsPostfix`: set_bms 메서드 후킹
- `ManagerPlayHook.FetchBMSToModulesPostfix`: FetchBMSToModules 메서드 후킹
- `ManagerPlayHook.GetPatternFromDirPostfix`: GetPatternFromDir 메서드 후킹

### 주요 파일 정보

- **SXGTDataHook.cs 계열** - 핵심 후킹 파일
  - SXGTData 생성자 후킹
  - 커스텀 차트 주입 트리거
  - 타입 추출은 `SXGTDataHook.NoteTypeExtraction.cs`, 스코어 처리는 `SXGTDataHook.Score.cs`

- **TrackDataAnalyzer.cs 계열**
  - TrackData 분석 및 주입
  - 다중 앨범 지원
  - 난이도 필드 설정

- **BGMPlayerHook.cs 계열**
  - BGM 파일 찾기 및 교체
  - 앨범 폴더 기반 BGM 검색
  - UnityWebRequest로 비동기 로드

- **PauseMethodHelper.cs 계열**
  - `CallPauseMenu()`: ESC 키로 일시정지 메뉴 호출
    - `LogSXGTDataDetails()`: 씬의 모든 Image 컴포넌트 열거
    - `SetEyecatchImage()`: Eyecatch Image에 썸네일 설정
    - `RG_PS_Pause.Show()` 호출하여 일시정지 메뉴 표시
  - `SetEyecatchImage()`: Eyecatch 이미지 설정
    - ManagerPlay에서 TrackData 찾기 (playTrack → trackData → bms 순서)
    - Track ID와 DisplayName 추출
    - "Eyecatch Image" 이름을 가진 Image 컴포넌트 찾기
    - 앨범 폴더에서 썸네일 로드 (ThumbnailLoader 사용, DisplayName 기반)
    - 앨범 폴더에서 못 찾으면 hwa 루트 폴더에서 검색
    - Eyecatch Image에 sprite 설정

- **ManagerMusicSelectHook.Preview.cs 계열**
  - 커스텀 트랙 Preview 재생
  - demo/music 파일 찾기 및 재생

- **SteamManifestLock.cs**
  - Steam 매니페스트 파일 읽기 전용 설정
  - 게임 업데이트 자동 차단
  - steamapps 폴더 자동 탐색

---

## 어셈블리 어트리뷰트

모드는 다음 어셈블리 어트리뷰트를 사용합니다:

```csharp
[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", "1.0.0", "Meowzter")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)] // Purple (R, G, B, A)
```

**위치**: `sxtg2-mod/Main/` (엔트리: `Main.cs`)

## DLL 참조

프로젝트는 다음 DLL을 참조합니다:

### MelonLoader 관련
- **MelonLoader.dll**: MelonLoader 프레임워크
  - 경로: `{게임 설치 폴더}\MelonLoader\net35\MelonLoader.dll`
- **0Harmony.dll**: Harmony 패칭 라이브러리
  - 경로: `{게임 설치 폴더}\MelonLoader\net35\0Harmony.dll`

### Unity Engine 관련
- **UnityEngine.dll**: Unity 엔진 핵심
- **UnityEngine.CoreModule.dll**: Unity 코어 모듈
- **UnityEngine.AudioModule.dll**: 오디오 모듈 (BGM 교체에 사용)
- **UnityEngine.VideoModule.dll**: 비디오 모듈 (BGA 교체에 사용)
- **UnityEngine.UnityWebRequestModule.dll**: 웹 요청 모듈
- **UnityEngine.UnityWebRequestAudioModule.dll**: 오디오 웹 요청 모듈 (BGM 스트리밍에 사용)
- **UnityEngine.UnityWebRequestTextureModule.dll**: 텍스처 웹 요청 모듈 (썸네일 로드에 사용)
- **UnityEngine.InputLegacyModule.dll**: 입력 모듈 (ESC 키 감지에 사용)
- **UnityEngine.ImageConversionModule.dll**: 이미지 변환 모듈 (썸네일 로드에 사용)
- **UnityEngine.UI.dll**: Unity UI 모듈 (텍스트 후킹에 사용)

**참고**: 모든 Unity DLL은 `{게임 설치 폴더}\Sixtar Gate STARTRAIL_Data\Managed\` 경로에 있습니다.

**프로젝트 파일**: `sxtg2-mod/sxtg2.csproj`

---

## 관련 문서

- [GAME_LOGIC.md](GAME_LOGIC.md): 게임 로직 분석 및 노트 생성 과정
- [BMS_PARSING.md](BMS_PARSING.md): BMS 파일 파싱 상세
- [DOCUMENTATION.md](DOCUMENTATION.md): 종합 참조 문서


