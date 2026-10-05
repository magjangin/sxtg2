# 🐛 디버깅 가이드

기준일: 2026-10-05 (v1.1.0)

**sxtg2 모드 디버깅 방법 및 도구**

---

## MelonLoader 로그

### 로그 위치

```
게임폴더/MelonLoader/Latest.log
```

### 모드 로그 레벨 (`LogLevel`)

모드는 `ModLog`(`Helpers/ModHelpers.cs`)를 통해 로그를 남기고, MelonPreferences 값으로 양을 조절합니다.
게임을 한 번 실행하면 `게임폴더/UserData/MelonPreferences.cfg`에 다음 항목이 생깁니다.

```toml
[sxtg2]
LogLevel = 1   # 0 = 오류만, 1 = 보통(기본), 2 = 상세/대량 덤프
```

| 메서드 | 출력 조건 |
| --- | --- |
| `ModLog.Error`, `ModLog.Exception` | 항상 |
| `ModLog.Msg`, `ModLog.Warning` | `LogLevel >= 1` |
| `ModLog.Verbose` | `LogLevel == 2` |
| `MelonLogger.*` 직접 호출 | 항상 (레벨 무관) |

`LogLevel = 2`에서만 나오는 대표 로그: 노트마다 `[NoteSpriteHook] 노트 생성: name=...`, 판정마다
`[JudgmentBar] 히트 감지: ...`, `[BGABGMSyncHook]` 싱크 보정, 설정 재로드 `변경 없음`, 키뷰어 키 바인딩, 곡 정보 파일 파싱 로그, 업적 요청 차단
(`[ResultTaint]`), 그리고 **진단용 훅**(`MusicSelectDiagnosticsHook`, `OperatorCharacterHook`, `ManagerResultHook`)이 남기는 곡 선택 확인창/
오퍼레이터 계층/결과 화면 덤프. 진단용 훅은 `LogLevel`이 2가 아니면 아무것도 하지 않으므로(결과 화면의 전체 `GameObject` 스캔도 안 함)
평소에는 로그가 조용합니다. 대량으로 찍히므로 조사할 때만 켜세요.

기능이 조용히 꺼졌을 때 찾아볼 로그 접두어: `[SafeAccess]`(private 필드를 못 찾음 — 게임 업데이트 의심), `[ResultSaveBlock]`(저장 차단 대상 메서드를
못 찾음), `[ResultTaint]`(결과 화면 사전 처리/플레이 횟수 되돌리기 실패), `[SaveCustomKey]`(모르는 키, 값이 틀린 줄 등 설정 경고).

`ModLog.BeginCorrelation(operation, hint)`를 `using`으로 감싸면 그 안의 로그 앞에 `[cid:작업:힌트]`가 붙습니다
(현재 코드에서 사용하는 곳은 없음).

### 로그 필터링

MelonLoader는 모드 로그 앞에 `[sxtg2]`를 붙이고, 모드 코드는 그 뒤에 `[클래스명]` 접두어를 씁니다.

```bash
# Windows PowerShell
Get-Content "MelonLoader/Latest.log" | Select-String "sxtg2"
Get-Content "MelonLoader/Latest.log" | Select-String "CustomChartInjector|ManagerPlayHook|BGMPlayerHook"

# CMD
findstr "sxtg2" MelonLoader\Latest.log
```

---

## 먼저 확인할 로그 (정상 흐름)

| 시점 | 로그 |
| --- | --- |
| 게임 시작 | `[SaveCustomKey] 설정 로드 완료 - ...`, `[CustomNoteSpriteLoader] 총 N개 ...`, `[Main] sxtg2 모드 초기화 완료` |
| 곡 선택 진입 | `[TrackDataAnalyzer] 커스텀 트랙 N개 추가 완료` |
| 플레이 진입 | `[SaveCustomKey] 설정 재로드 #n ...`(바뀐 항목이 있을 때), `[CustomChartInjector] N개 주입, totalNotes=...`, `[BGMPlayerHook] BGM 교체 완료: ...` |
| 결과 화면 | (저장 차단 시) `[차단] 하이스코어 및 랭킹 저장 차단(사유): ...`, (커스텀 곡/오토/올퍼펙트/점수 상한 변경 시) `[ResultTaint] 이번 결과는 업적/플레이 횟수에 반영하지 않습니다 (...)` |

증상별 확인 방법은 [../01-user-guide/TROUBLESHOOTING.md](../01-user-guide/TROUBLESHOOTING.md)에 모았습니다.

---

## 디버거 연결

일반 배포용 Unity 게임은 Visual Studio의 "프로세스에 연결"로 관리 코드 중단점이 바로 잡히지 않는 경우가 많습니다
(Unity 플레이어의 Mono 디버그 에이전트가 꺼져 있음). 이 프로젝트는 주로 **로그 + 디컴파일 소스 대조**로 디버깅합니다.
디버거가 꼭 필요하면 dnSpy의 Unity 디버그용 Mono 교체 방식 등 별도 준비가 필요합니다.

csproj는 Debug 빌드에서 `DebugType=portable` PDB를 만듭니다. 빌드 스크립트가 `x64` 플랫폼으로 빌드하므로 csproj의
`Debug|x64` 블록(`DEBUG` 상수, `DebugSymbols`)과 `Release|x64`의 최적화가 적용됩니다(`CODE_STRUCTURE.md` 참고).

---

## 일반적인 문제 디버깅

### 모드가 로드되지 않음

- `Latest.log`에 `sxtg2` 모드 이름과 버전(`1.1.0`)이 나오는지 확인합니다.
- `[Main] 초기화 실패: ...` 오류가 있으면 예외 전체가 함께 찍힙니다.
- Harmony 패치 실패는 MelonLoader가 모드 로드 단계에서 오류로 남깁니다. 게임 업데이트로 대상 메서드가 사라졌을 가능성이 큽니다.

### 커스텀 곡이 인식되지 않음

`TrackDataAnalyzer`는 곡 선택 씬 `Awake` 때만 스캔합니다. 스캔 규칙을 코드 밖에서 확인하려면:

```powershell
# hwa 루트 + 1단계 하위 폴더의 BMS (모드와 같은 범위, 폴더마다 첫 파일이 등록됨)
Get-ChildItem "게임폴더\hwa\*" -Include *.bms,*.bme,*.bml -File
Get-ChildItem "게임폴더\hwa" -Directory | ForEach-Object { Get-ChildItem "$($_.FullName)\*" -Include *.bms,*.bme,*.bml -File }
```

### 노트가 주입되지 않거나 이상함

파서만 따로 돌려 보면 게임 없이 원인을 좁힐 수 있습니다. `sxtg2.LogicTests`처럼 `BmsParser.cs`를 링크한 콘솔 프로젝트에서:

```csharp
var result = BmsParser.ParseBmsFileWithStatistics(@"H:\...\hwa\Album_A\chart.bms");
Console.WriteLine($"BPM={result.BaseBpm}, notes={result.Notes.Count}");
foreach (var m in result.Statistics.MissingEndNotes)
    Console.WriteLine($"끝 누락: lane={m.Lane} time={m.Time:F3} type={m.NoteType}");
foreach (var n in result.Notes.OrderBy(n => n.Time).Take(20))
    Console.WriteLine($"{n.Time:F3}s lane={n.Lane} {n.NoteType} len={n.Length:F3} val={n.OriginalNoteValue}");
```

`MissingEndNotes`가 비어 있지 않으면 게임에서 판정이 멈출 수 있습니다(`02-systems/BMS_FORMAT.md`).

---

## 아래 코드 조각에 대해

이하 절의 코드는 조사할 때 임시로 붙여 쓰는 **예시**입니다. 현재 모드 코드에 들어 있는 기능은 아닙니다.

## 타입 디버깅

```csharp
public static void DebugTypeInfo(object obj)
{
    var type = obj.GetType();
    
    MelonLogger.Msg($"=== Type: {type.FullName} ===");
    
    // 필드
    MelonLogger.Msg("Fields:");
    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
    {
        var value = field.GetValue(obj);
        MelonLogger.Msg($"  {field.Name} = {value}");
    }
    
    // 프로퍼티
    MelonLogger.Msg("Properties:");
    foreach (var prop in type.GetProperties())
    {
        try
        {
            var value = prop.GetValue(obj);
            MelonLogger.Msg($"  {prop.Name} = {value}");
        }
        catch
        {
            MelonLogger.Msg($"  {prop.Name} = (접근 불가)");
        }
    }
}
```

---

## 성능 프로파일링

```csharp
public class Profiler
{
    private static Dictionary<string, Stopwatch> _timers = new Dictionary<string, Stopwatch>();
    
    public static void Start(string name)
    {
        if (!_timers.ContainsKey(name))
            _timers[name] = new Stopwatch();
        
        _timers[name].Restart();
    }
    
    public static void Stop(string name)
    {
        if (_timers.ContainsKey(name))
        {
            _timers[name].Stop();
            MelonLogger.Msg($"[Profiler] {name}: {_timers[name].ElapsedMilliseconds}ms");
        }
    }
}

// 사용 예시
Profiler.Start("BMS 파싱");
var notes = BmsParser.ParseBmsFile(path);
Profiler.Stop("BMS 파싱");
```

---

## 메모리 디버깅

```csharp
public static void LogMemoryUsage()
{
    var totalMemory = GC.GetTotalMemory(false);
    var mb = totalMemory / (1024.0 * 1024.0);
    
    MelonLogger.Msg($"메모리 사용량: {mb:F2} MB");
}
```

---

## Unity 객체 디버깅

```csharp
public static void DebugGameObject(GameObject obj)
{
    MelonLogger.Msg($"=== GameObject: {obj.name} ===");
    MelonLogger.Msg($"Active: {obj.activeSelf}");
    MelonLogger.Msg($"Layer: {obj.layer}");
    MelonLogger.Msg($"Tag: {obj.tag}");
    
    // 컴포넌트
    MelonLogger.Msg("Components:");
    foreach (var component in obj.GetComponents<Component>())
    {
        MelonLogger.Msg($"  - {component.GetType().Name}");
    }
    
    // 자식 오브젝트
    MelonLogger.Msg($"Children: {obj.transform.childCount}");
    for (int i = 0; i < obj.transform.childCount; i++)
    {
        var child = obj.transform.GetChild(i);
        MelonLogger.Msg($"  - {child.name}");
    }
}
```

---

## 씬 디버깅

```csharp
public static void DebugCurrentScene()
{
    var scene = SceneManager.GetActiveScene();
    
    MelonLogger.Msg($"=== Scene: {scene.name} ===");
    MelonLogger.Msg($"Path: {scene.path}");
    MelonLogger.Msg($"BuildIndex: {scene.buildIndex}");
    MelonLogger.Msg($"IsLoaded: {scene.isLoaded}");
    
    // 루트 오브젝트
    var rootObjects = scene.GetRootGameObjects();
    MelonLogger.Msg($"Root Objects: {rootObjects.Length}");
    
    foreach (var obj in rootObjects)
    {
        MelonLogger.Msg($"  - {obj.name}");
    }
}
```

---

## 조건부 로깅

```csharp
#if DEBUG
    MelonLogger.Msg("디버그 모드에서만 출력");
#endif

// 또는
private const bool VERBOSE_LOGGING = true;

if (VERBOSE_LOGGING)
{
    MelonLogger.Msg("상세 로그");
}
```

---

## 스택 트레이스

```csharp
public static void LogStackTrace()
{
    var stackTrace = new StackTrace(true);
    
    MelonLogger.Msg("=== Stack Trace ===");
    for (int i = 0; i < stackTrace.FrameCount; i++)
    {
        var frame = stackTrace.GetFrame(i);
        var method = frame.GetMethod();
        var fileName = frame.GetFileName();
        var lineNumber = frame.GetFileLineNumber();
        
        MelonLogger.Msg($"{i}: {method.DeclaringType}.{method.Name}");
        if (fileName != null)
            MelonLogger.Msg($"   at {fileName}:{lineNumber}");
    }
}
```

---

## 게임 코드 읽기 (디컴파일)

### 저장소의 디컴파일 소스

저장소 루트의 `sxtg2/` 폴더에 `Assembly-CSharp.dll`을 ilspycmd로 디컴파일한 C# 소스가 있습니다(`.gitignore`로 제외,
우리 코드가 아님). 게임 로직 확인은 여기서 검색하는 게 가장 빠릅니다.

```bash
# 예: 판정 루프, 노트 생성, 결과 저장
grep -n "private void Update" sxtg2/RhythmGame.Play/RG_PS_Judgement.cs
grep -rn "FinishHoldNote" sxtg2 --include=*.cs
```

게임이 업데이트되면 다시 디컴파일해서 덮어쓰세요.

```bash
ilspycmd -p -o sxtg2 "게임폴더/Sixtar Gate STARTRAIL_Data/Managed/Assembly-CSharp.dll"
```

### dnSpy

GUI로 보고 싶으면 dnSpy(https://github.com/dnSpy/dnSpy/releases)로 같은 DLL을 엽니다.
`Ctrl+Shift+K`로 `SXGTData`, `ManagerPlay`, `RG_PS_Judgement` 같은 타입을 검색할 수 있습니다.

값을 바꾸는 훅을 만들기 전에는 **그 값이 필드에서 읽히는지, 메서드 안에 리터럴로 박혀 있는지**부터 확인하세요.
예: `SXGTData.maxScore` 필드는 점수 계산에 쓰이지 않고, 실제 계산식은 `1000000f` 리터럴을 씁니다(그래서 `JudgeScoreMaxHook`은 Transpiler).

---

## 일반적인 에러 패턴

### NullReferenceException
```csharp
// 나쁜 예
var note = notes[0];
note.Time = 1.0f; // notes가 null이면 에러

// 좋은 예
if (notes != null && notes.Count > 0)
{
    var note = notes[0];
    note.Time = 1.0f;
}
```

### InvalidCastException
```csharp
// 나쁜 예
var shortNote = (ShortNote)note; // note가 HoldNote면 에러

// 좋은 예
if (note is ShortNote shortNote)
{
    // shortNote 사용
}
```

### ReflectionTypeLoadException
```csharp
// 안전한 타입 로드
try
{
    var types = assembly.GetTypes();
}
catch (ReflectionTypeLoadException ex)
{
    MelonLogger.Warning($"일부 타입 로드 실패: {ex.LoaderExceptions.Length}개");
    var loadedTypes = ex.Types.Where(t => t != null);
    // loadedTypes 사용
}
```
