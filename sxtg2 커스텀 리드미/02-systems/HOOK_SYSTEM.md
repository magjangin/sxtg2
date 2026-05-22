# 🎣 Hook 시스템

**Harmony를 사용한 런타임 메서드 패칭 시스템**

---

## Harmony 패칭 기본

### Prefix Hook

```csharp
[HarmonyPrefix]
[HarmonyPatch(typeof(TargetClass), "MethodName")]
private static bool MethodPrefix(TargetClass __instance, ref ReturnType __result)
{
    // 원본 메서드 실행 전
    // return false: 원본 메서드 실행 안 함
    // return true: 원본 메서드 실행
    return true;
}
```

### Postfix Hook

```csharp
[HarmonyPostfix]
[HarmonyPatch(typeof(TargetClass), "MethodName")]
private static void MethodPostfix(TargetClass __instance, ReturnType __result)
{
    // 원본 메서드 실행 후
    // __result 수정 가능
}
```

---

## 주요 Hook 구현

### SXGTDataHook

```csharp
public class SXGTDataHook
{
    private static object _currentSXGTData;
    private static Type _shortNoteType;
    private static Type _holdNoteType;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SXGTData), MethodType.Constructor)]
    private static void SXGTDataConstructorPostfix(ref object __instance)
    {
        _currentSXGTData = __instance;
        MelonLogger.Msg("SXGTData 인스턴스 저장");
    }

    public static void ProcessPendingNoteRemovalAndInjection()
    {
        if (_currentSXGTData == null) return;

        // 1. 원본 노트 제거 및 타입 추출
        ClearAllNotes(_currentSXGTData);

        // 2. 커스텀 차트 주입
        CustomChartInjector.InjectBmsNotesToLaneData(_currentSXGTData);

        // 3. 스코어 제한 해제
        FixMaxScoreField(_currentSXGTData);
    }

    private static void ClearAllNotes(object sxgtData)
    {
        var laneDataField = sxgtData.GetType().GetField("laneData");
        var laneData = laneDataField.GetValue(sxgtData);

        for (int lane = 0; lane <= 9; lane++)
        {
            var noteList = laneData[lane];
            
            // 타입 추출 (첫 번째 노트에서)
            if (noteList.Count > 0 && _shortNoteType == null)
            {
                var firstNote = noteList[0];
                var noteType = firstNote.GetType();
                
                if (noteType.Name.Contains("ShortNote"))
                    _shortNoteType = noteType;
                else if (noteType.Name.Contains("HoldNote"))
                    _holdNoteType = noteType;
            }

            // 모든 노트 제거
            noteList.Clear();
        }
    }
}
```

### ManagerPlayHook

```csharp
public class ManagerPlayHook
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ManagerPlay), "set_bms")]
    private static void SetBmsPostfix(object __instance)
    {
        OnPlaySceneStart(__instance, "set_bms");
    }
}
```

현재 `OnPlaySceneStart(...)`는 긴 처리 흐름을 직접 들고 있지 않고 `CustomPlayStartupFlow.Run(...)`에 위임합니다. 이 클래스가 커스텀 트랙 확정, 플레이 직전 BMS 로드, BGA/BGM 교체, 스코어 제한 해제, 노트 제거/주입 순서를 관리합니다.

### BGMPlayerHook

```csharp
public class BGMPlayerHook
{
    private static bool _bgmReplaced = false;

    public static void ReplacePlaySceneBGM(string customAlbumFolder)
    {
        if (_bgmReplaced) return;

        var bgmPath = BgmFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false);
        var source = BgmAudioSourceFinder.Find(_managerPlayBGM);

        if (bgmPath != null && source != null)
        {
            GetOrCreateCoroutineRunner().StartCoroutine(LoadAndReplaceBGM(source, bgmPath));
        }
    }

    private static IEnumerator LoadAndReplaceBGM(AudioSource source, string path)
    {
        var uri = "file://" + path;
        var www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.OGGVORBIS);
        
        // 스트리밍 모드
        var handler = (DownloadHandlerAudioClip)www.downloadHandler;
        handler.streamAudio = true;

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            source.clip = handler.audioClip;
            source.Play();
            MelonLogger.Msg($"BGM 교체 완료: {path}");
        }
    }
}
```

BGA도 같은 방향으로 분리되어 있습니다. `BgaFileResolver`가 mp4를 선택하고, `BgaVideoPlayerFinder`가 대상 `VideoPlayer`를 찾으며, `BGAPlayerHook`은 실제 URL 교체와 상태 관리에 집중합니다.

---

## Hook 초기화

```csharp
public class Main : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Hook 초기화
        SXGTDataHook.Initialize();
        ManagerPlayHook.Initialize();
        BGMPlayerHook.Initialize();
        BGAPlayerHook.Initialize();
        TextHook.Initialize();
    }
}
```

---

## 타입 추출 패턴

```csharp
// 런타임에 타입 찾기
private static Type FindNoteType(string typeName)
{
    var assemblies = AppDomain.CurrentDomain.GetAssemblies();
    
    foreach (var assembly in assemblies)
    {
        var types = assembly.GetTypes();
        
        foreach (var type in types)
        {
            if (type.Name == typeName)
                return type;
        }
    }
    
    return null;
}

// 제네릭 타입 추출
private static Type ExtractGenericType(object list)
{
    var listType = list.GetType();
    
    if (listType.IsGenericType)
    {
        var genericArgs = listType.GetGenericArguments();
        return genericArgs[0];
    }
    
    return null;
}
```

---

## 리플렉션 활용

```csharp
// 필드 접근
var field = type.GetField("fieldName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
var value = field.GetValue(instance);
field.SetValue(instance, newValue);

// 메서드 호출
var method = type.GetMethod("methodName", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
var result = method.Invoke(instance, new object[] { param1, param2 });

// 생성자 호출
var constructor = type.GetConstructor(new Type[] { typeof(float), typeof(int) });
var instance = constructor.Invoke(new object[] { 1.0f, 5 });
