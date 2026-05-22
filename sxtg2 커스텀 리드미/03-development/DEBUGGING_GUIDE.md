# 🐛 디버깅 가이드

**sxtg2 모드 디버깅 방법 및 도구**

---

## MelonLoader 로그

### 로그 위치

```
게임폴더/MelonLoader/Latest.log
```

### 로그 레벨

```csharp
MelonLogger.Msg("일반 메시지");
MelonLogger.Warning("경고 메시지");
MelonLogger.Error("에러 메시지");
```

### 로그 필터링

```bash
# Windows PowerShell
Get-Content "MelonLoader/Latest.log" | Select-String "sxtg2"

# CMD
findstr "sxtg2" MelonLoader\Latest.log
```

---

## Visual Studio 디버거

### 1. 프로젝트 설정

```xml
<!-- sxtg2.csproj -->
<PropertyGroup>
  <DebugType>full</DebugType>
  <DebugSymbols>true</DebugSymbols>
</PropertyGroup>
```

### 2. 디버거 연결

```
1. Visual Studio 열기
2. 디버그 → 프로세스에 연결
3. "Sixtar Gate STARTRAIL.exe" 선택
4. 연결
```

### 3. 중단점 설정

```csharp
public static void InjectBmsNotesToLaneData(object sxgtData)
{
    // 여기에 중단점 설정
    if (_parsedBmsNotes == null)
        return;
    
    // 디버거가 여기서 멈춤
    var laneData = GetLaneData(sxgtData);
}
```

---

## 일반적인 문제 디버깅

### 모드가 로드되지 않음

```csharp
// Main.cs에 로그 추가
public override void OnInitializeMelon()
{
    MelonLogger.Msg("=== sxtg2 초기화 시작 ===");
    
    try
    {
        // 초기화 코드
        MelonLogger.Msg("초기화 완료");
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"초기화 실패: {ex.Message}");
        MelonLogger.Error(ex.StackTrace);
    }
}
```

### BMS 파일이 인식되지 않음

```csharp
public static void ScanAndParseBmsFiles()
{
    var hwaPath = Path.Combine(Application.dataPath, "..", "hwa");
    
    MelonLogger.Msg($"hwa 폴더 경로: {hwaPath}");
    MelonLogger.Msg($"폴더 존재: {Directory.Exists(hwaPath)}");
    
    if (!Directory.Exists(hwaPath))
    {
        MelonLogger.Warning("hwa 폴더가 없습니다");
        return;
    }
    
    var bmsFiles = Directory.GetFiles(hwaPath, "*.bms", SearchOption.AllDirectories);
    MelonLogger.Msg($"발견된 BMS 파일: {bmsFiles.Length}개");
    
    foreach (var file in bmsFiles)
    {
        MelonLogger.Msg($"  - {Path.GetFileName(file)}");
    }
}
```

### 노트가 주입되지 않음

```csharp
public static void InjectBmsNotesToLaneData(object sxgtData)
{
    MelonLogger.Msg("=== 노트 주입 시작 ===");
    
    if (_parsedBmsNotes == null)
    {
        MelonLogger.Error("파싱된 노트가 없음");
        return;
    }
    
    MelonLogger.Msg($"주입할 노트: {_parsedBmsNotes.Count}개");
    
    var laneData = GetLaneData(sxgtData);
    if (laneData == null)
    {
        MelonLogger.Error("laneData를 찾을 수 없음");
        return;
    }
    
    MelonLogger.Msg("laneData 접근 성공");
    
    // 레인별 주입
    foreach (var kvp in notesByLane)
    {
        MelonLogger.Msg($"레인 {kvp.Key}: {kvp.Value.Count}개 노트 주입");
    }
    
    MelonLogger.Msg("=== 노트 주입 완료 ===");
}
```

---

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

## dnSpy 사용

### 1. dnSpy 다운로드
```
https://github.com/dnSpy/dnSpy/releases
```

### 2. 게임 어셈블리 열기
```
파일 → 열기 → Assembly-CSharp.dll
(게임폴더/Sixtar Gate STARTRAIL_Data/Managed/)
```

### 3. 타입 검색
```
Ctrl+Shift+K → 타입 이름 입력
예: "SXGTData", "ManagerPlay"
```

### 4. 디컴파일된 코드 확인
```csharp
// 게임의 실제 코드를 볼 수 있음
public class SXGTData
{
    public Dictionary<int, List<Note>> laneData;
    // ...
}
```

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
