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

### Transpiler Hook

메서드 **안에 리터럴로 박힌 상수나 로직 자체**를 바꿔야 할 때 씁니다. Prefix/Postfix는 원본 메서드
바깥에서만 개입할 수 있어서, `Mathf.Min(JudgeScore, 1000000f)` 같은 하드코딩 값은 손댈 수 없습니다.

```csharp
[HarmonyTranspiler]
private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
{
    var codes = new List<CodeInstruction>(instructions);
    foreach (var code in codes)
    {
        if (code.opcode != OpCodes.Ldc_R4 || !(code.operand is float value)) continue;
        // opcode/operand만 갈아끼우면 라벨과 예외 블록이 그대로 보존된다
        code.opcode = OpCodes.Call;
        code.operand = AccessTools.Method(typeof(MyHook), nameof(MyHook.GetValue));
    }
    return codes;
}
```

상수를 다른 상수로 굽는 대신 **static 메서드 호출로 교체**하면(스택 효과가 같아 안전) 런타임
설정값을 그대로 반영할 수 있고, 패치 적용 시점과 설정 로드 순서에 영향받지 않습니다.

> 어떤 값을 바꾸고 싶을 때는 먼저 디컴파일 원본(`sxtg2/` 폴더)에서 **그 값이 필드에서 읽히는지,
> 메서드 안에 리터럴로 박혀 있는지**부터 확인하세요. 후자면 리플렉션 필드 수정은 무조건 무효입니다.

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

        // 3. 주입된 노트 수를 기준으로 종료/스코어 판정 값 갱신
        ApplyNoteCountsToSxgtData(_currentSXGTData);
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

### NoteSpriteHook

`InventoryPopup` 모드(`H:\source\repos\InventoryPopup`)에서 이식한, 노트의 **시각적 스킨**만 교체하는 후킹입니다. 노트 데이터 자체를 다루는 `SXGTDataHook`/`CustomChartInjector`와는 별개입니다.

```csharp
public class NoteSpriteHook
{
    public static void Initialize()
    {
        var noteGeneratorType = TypeFinderHelper.FindType("RhythmGame.NoteGenerator");
        var generateMethod = noteGeneratorType.GetMethod("Generate", BindingFlags.Public | BindingFlags.Instance);
        harmony.Patch(generateMethod, postfix: new HarmonyMethod(...GeneratePostfix...));
    }

    // __result: RhythmGame.NoteGenerator.Generate가 반환한 RG_NoteObject
    private static void GeneratePostfix(object __result)
    {
        var noteObject = ReflectionHelper.GetFirstMemberValueSafe(__result, "gameObject") as GameObject;
        // shortNote/tailNote/holdTexture 필드에 CustomNotes 폴더의 스프라이트 적용
        // 이후 NoteRendererRecovery로 UI 강제 갱신
    }
}
```

자세한 내용은 `02-systems/NOTE_SYSTEM.md`의 "노트 스킨(커스텀 스프라이트)" 절 참고.

### JudgeScoreMaxHook

`SaveCustomKey/config.txt`의 `MaxScore` 값으로 점수 상한을 바꾸는 Transpiler 훅입니다
(`Hooks/GameplayHooks.cs`).

```csharp
[HarmonyPatch]
public static class JudgeScoreMaxHook
{
    // 기본값이면 패치 자체를 붙이지 않는다. EnsureInitialized로 설정 로드 순서 문제도 해소.
    private static bool Prepare()
    {
        SaveCustomKeyConfig.EnsureInitialized();
        return SaveCustomKeyConfig.IsMaxScoreCustom;
    }

    // RG_PS_Judgement.Update() / CalculateJudgeScore(float)
    private static IEnumerable<MethodBase> TargetMethods() { ... }

    public static float GetMaxScore() => SaveCustomKeyConfig.MaxScore;
}
```

자세한 내용은 `02-systems/SCORE_SYSTEM.md`의 "점수 상한 설정" 절 참고.

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
        NoteSpriteHook.Initialize();
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
