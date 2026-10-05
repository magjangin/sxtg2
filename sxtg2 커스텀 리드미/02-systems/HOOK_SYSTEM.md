# 🎣 Hook 시스템

기준일: 2026-09-28 (v1.1.0)

**Harmony를 사용한 런타임 메서드 패칭 시스템**

---

## 패치가 적용되는 방식

- 모든 훅은 클래스/메서드에 `[HarmonyPatch]` 속성을 붙인 **선언형**입니다(2026-07-23 `53f4174`에서 전환).
- MelonLoader가 모드를 로드할 때 어셈블리 전체를 자동으로 `PatchAll` 합니다. 그래서 `Main.OnInitializeMelon`에는
  훅을 거는 코드가 없고, `harmony.Patch(...)`를 직접 부르는 곳도 없습니다.
- 게임 어셈블리를 직접 참조하므로 대상은 대부분 `typeof(ManagerPlay)`처럼 타입으로 지정합니다.
  이름으로 찾는 경우는 `TargetMethods()`에서 `AccessTools.TypeByName`/`AccessTools.Method`를 씁니다.
- 파일 이름이 `...Hook`이어도 Harmony 패치가 **아닌** 클래스가 있습니다: `BGMPlayerHook`, `BGAPlayerHook`,
  `BGABGMSyncHook`(`Hooks/AudioHooks.cs`)은 다른 훅이나 `Main.OnUpdate`가 호출하는 일반 헬퍼입니다.

## Harmony 패칭 기본

### Prefix Hook

```csharp
[HarmonyPatch(typeof(ManagerPlay))]
public static class ManagerPlayHook
{
    [HarmonyPatch("CheckBGMStart")]
    [HarmonyPrefix]
    private static bool CheckBGMStartPrefix(TrackData ___playTrack)   // ___필드명 = private 필드 주입
    {
        // return false: 원본 메서드 실행 안 함 / return true: 원본 실행
        return !(___playTrack is CustomTrackData) || !BGMPlayerHook.IsLoading();
    }
}
```

### Postfix Hook

```csharp
[HarmonyPatch("Generate")]
[HarmonyPostfix]
private static void GeneratePostfix(RG_NoteObject __result)   // __result = 원본 반환값
{
    // 원본 메서드 실행 후
}
```

이 프로젝트에서 쓰는 주입 인자: `__instance`, `__result`, `___필드명`(private 필드), `__0`/`__1`(n번째 인자),
`__args`(전체 인자 배열), `__originalMethod`.

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

> 어떤 값을 바꾸고 싶을 때는 먼저 디컴파일 원본(저장소 루트의 `sxtg2/` 폴더, git 제외)에서 **그 값이
> 필드에서 읽히는지, 메서드 안에 리터럴로 박혀 있는지**부터 확인하세요. 후자면 리플렉션 필드 수정은 무조건 무효입니다.

---

## 전체 훅 목록

| 클래스 (파일) | 대상 메서드 | 종류 | 동작 조건 | 하는 일 |
| --- | --- | --- | --- | --- |
| `ManagerPlayHook` (GameplayHooks) | `ManagerPlay.FetchBMSToModules(SXGTData)` | Prefix | 커스텀 트랙 | BMS 파싱 → 노트 주입, BGM/BGA 교체 |
| | `ManagerPlay.CheckBGMStart` | Prefix | 커스텀 트랙 BGM 로딩 중 | BGM 시작을 보류(원본 실행 건너뜀) |
| `NoteSpriteHook` (GameplayHooks) | `NoteGenerator.Generate` | Postfix | 커스텀 스프라이트가 있을 때 | 커스텀 노트 스프라이트 적용, 그 이미지에만 `SetNativeSize` + `noteSize` 재적용 |
| `NoteSwayHook` (GameplayHooks) | `RG_NoteObject.CalculatePosition` | Postfix | `NoteSway=1` | 노트 루트 x를 사인파로 흔듦 |
| `NoteSpeedChaosHook` (GameplayHooks) | `RG_NoteObject.CalculatePosition` | Postfix | `NoteSpeedChaos=1` | 자식 y/홀드 길이에 배율 적용 |
| | `NoteGenerator.Start` | Postfix | `NoteSpeedChaos=1`, 최저배율 < 1 | `notePreGenerateTime`을 `3 / 최저배율`로 늘림 |
| `AutoPlayHook` (GameplayHooks) | `ManagerPlay.InitializePlayScene` | Postfix | 오토플레이 켜짐 | 게임의 `ManagerPlay.autoPlay` 플래그를 켬 |
| `AllPerfectJudgeHook` (GameplayHooks) | `TargetMethods()` 참고 | Prefix | 올퍼펙트 켜짐 | 첫 인자 `EJudges`를 `BLUESTAR`로 바꿈 |
| `JudgeScoreMaxHook` (GameplayHooks) | `RG_PS_Judgement.Update`, `CalculateJudgeScore(float)` | Transpiler | 항상 | `ldc.r4 1000000` → `GetMaxScore()` 호출 |
| `TrackDataMediaHook` (GameplayHooks) | `TrackData.GetJacketSprite()`, `GetThumbSprite` | Prefix | 커스텀 트랙 | 앨범 폴더 자켓 PNG 반환(곡마다 한 번만 찾음, 없으면 원본) |
| | `TrackData.GetAudioClip()`, `GetLoadingAnimation` | Prefix | 커스텀 트랙 | 도너 트랙 리소스 반환 |
| | `TrackData.GetSixtarPatternDirectory` (2개 오버로드) | Prefix | 커스텀 트랙 | 도너 트랙 패턴 경로 반환 |
| `ResultSaveBlockHook` (ResultGuardHooks) | `UserAccountModule.SavePlayData`, `LyrebirdServer.PostUserScore` | Prefix | 오토/올퍼펙트/점수 상한 변경/커스텀 곡 또는 `BlockSave` | 원본 실행 건너뜀(하이스코어 저장·서버 전송 차단) |
| `ResultTaintHook` (ResultGuardHooks) | `ManagerResult.Start` | Prefix + Finalizer | 오토/올퍼펙트/점수 상한 변경/커스텀 곡 | 플레이 횟수/실패 횟수/마지막 플레이 곡을 되돌림 |
| | `UserAccountModule.RequestAchievementUnlock` | Prefix | 위와 같은 결과 화면 중 | Steam 업적 해금 요청을 막음 |
| `ServerGuardHook` (ResultGuardHooks) | `LyrebirdServer.IncreaseTrackPlayCount` | Prefix | 커스텀 곡 ID | 서버로 플레이 카운트를 보내지 않음 |
| | `LyrebirdServer.GetHighScoreList` | Prefix | 커스텀 곡 ID | 서버에 묻지 않고 빈 랭킹 목록을 바로 돌려줌 |
| | `Util.FindTrackByID` | Postfix | 못 찾았고 커스텀 곡 ID일 때 | 커스텀 곡 목록에서 찾아 줌(랭킹 창 NRE 방지) |
| `ManagerMusicSelectHook` (MusicSelectFeature) | `ManagerMusicSelect.Awake` | Postfix | 항상 | `TrackDataAnalyzer.InjectCustomTracks` |
| | `ManagerMusicSelect.PlayPreview` | Prefix | 커스텀 트랙 | 앨범 폴더 오디오로 미리듣기 (원본 실행 건너뜀) |
| `MusicSelectDiagnosticsHook` (DiagnosticHooks) | `ManagerMusicSelect.OpenConfirmWindow`, `instantiateOperatorCharacter` | Postfix | `LogLevel=2` | **진단 로깅**: 확인창 `characterLayer` 계층 |
| `OperatorCharacterHook` (DiagnosticHooks) | `OperatorCharacter.SetUp`, `ShowDialogue` | Prefix | `LogLevel=2` | **진단 로깅** |
| `ManagerResultHook` (DiagnosticHooks) | `ManagerResult.Start` | Postfix | `LogLevel=2` | **진단 로깅**: 오퍼레이터 계층 + `FindObjectsOfType<GameObject>()` 전체 스캔 |
| `FastSlowMeter_OnGetJudge_Patch` (JudgmentBarFeature) | `OnGetJudge(EJudges, float)` | Postfix | 판정바 켜짐 | 판정 등급·오차를 `JudgmentBar.RegisterHit`로 전달(같은 프레임 중복은 한 번만) |

"진단 로깅" 훅 3개 클래스는 2026-08-03 시이(Shii) 조사용으로 추가한 것으로 게임 동작은 바꾸지 않습니다. 2026-10-05부터 `DiagnosticHooks.cs`로
분리했고 **`LogLevel=2`(상세)일 때만** 동작합니다(예전에는 로그 레벨과 무관하게 확인창을 열 때마다 수백 줄, 결과 화면마다 전체 스캔과 수백 줄이
찍혔음). 핵심 훅(`ManagerMusicSelectHook`)과 분리돼 있어서 진단용 필드/메서드가 게임 업데이트로 바뀌어도 커스텀 곡 등록과 미리듣기가 같이
깨지지 않습니다.

게임의 private 필드는 `SafeAccess.FieldRef`로 접근합니다. 필드 이름이 바뀌어 못 찾으면 `[SafeAccess] … 필드를 찾지 못해 …` 경고만 남기고 그 기능만
건너뜁니다(예전에는 `static readonly FieldRefAccess`가 `TypeInitializationException`을 던져 씬 이벤트 구독까지 못 하는 경우가 있었음).

---
## 주요 훅 상세

### ManagerPlayHook — 커스텀 차트와 미디어 교체

게임은 `ManagerPlay.Set(track, lv, ps)`에서 트랙의 패턴 파일을 읽어 `SXGTData`를 만들고, 초기화 중
`FetchBMSToModules(bms)`로 노트 생성기·판정 모듈에 넘깁니다. 커스텀 트랙은 `TrackDataMediaHook` 덕분에
**도너 트랙의 패턴**이 읽힌 상태이므로, 이 Prefix에서 같은 `SXGTData` 인스턴스의 내용을 갈아끼웁니다.

```text
FetchBMSToModulesPrefix(__instance, _bms, ___playTrack, ___bgaPlayer, ___defaultBGAcanvas)
  ├─ ___playTrack이 CustomTrackData가 아니면 즉시 반환
  ├─ BmsParser.ParseBmsFileWithStatistics(customTrack.BmsPath)
  │    └─ 노트가 없으면 경고 후 도너 패턴 유지
  ├─ CustomChartInjector.SetParsedChart / InjectBmsNotesToLaneData(_bms)
  ├─ BGMPlayerHook.ReplacePlaySceneBGM(__instance.bgm, albumFolder)   // 비동기 로드 시작
  └─ userData.bgaMode == ON 이고 BGAPlayerHook.ReplacePlaySceneBGA(...) 성공 시
       bgaPlayer 활성화, defaultBGAcanvas 비활성화
```

`CheckBGMStartPrefix`는 커스텀 BGM이 아직 로드 중이면 `false`를 반환해 원본 `bgm.Play()`를 미룹니다. 로드가 끝나면
(`_isLoading=false`) 다음 프레임에 원본이 정상 실행됩니다. 로드에 실패하면 도너 트랙 BGM이 그대로 재생됩니다.

### TrackDataMediaHook — 커스텀 트랙을 도너처럼 보이게 하기

`CustomTrackData`는 `TrackData`를 상속하지만 게임 리소스(오디오, 로딩 애니메이션, 패턴 파일)가 없습니다.
그래서 이 리소스를 요청하는 메서드를 가로채 `ResourceDonor`(음악 선택 목록의 첫 번째 원본 트랙)의 값을
돌려줍니다. 자켓/썸네일만은 앨범 폴더 PNG(`ThumbnailLoader`)를 우선 사용합니다.
자세한 파일 규칙은 `MEDIA_SYSTEM.md` 참고.

### NoteSpriteHook — 노트 스킨

`NoteGenerator.Generate`가 만든 `RG_NoteObject`의 `shortNote`/`tailNote`/`holdTexture` 자식 `Image.sprite`를
`CustomNotes` 폴더 스프라이트로 바꿉니다. 노트 이름 `_Blue(Clone)`의 `(Clone)` 접미사를 떼고 `Blue`로 찾으며, **스프라이트를 적용한 이미지에만**
`SetNativeSize()`를 호출한 뒤 게임의 노트 크기 옵션(`noteSize/100`, 원본 `RG_NoteObject.SetSize`와 같은 규칙)을 다시 곱합니다. 스킨이 없는 노트는
건드리지 않습니다(예전에는 모든 노트에 `SetNativeSize`를 불러 노트 크기 옵션을 무시했음). 파일 이름 규칙은 `NOTE_SYSTEM.md`의 "노트 스킨" 절 참고.

### AutoPlayHook / AllPerfectJudgeHook

- **오토플레이**: 게임에 원래 있는 `ManagerPlay.autoPlay`(public bool)를 `ManagerPlay.InitializePlayScene` Postfix에서 켭니다. 그러면
  `RG_PS_Judgement.Update`가 `CheckMissBreak` 대신 원본 `AutoPlayJudge`를 부르고, 홀드 틱 판정과 홀드 종료 때의 `OnLaneKeyUp`,
  키 입력 무시(`KeyInputAction`)도 게임 원래 경로로 처리됩니다. 예전에는 private `AutoPlayJudge`를 `RG_PS_Judgement.Update` Postfix에서 직접
  불렀는데, 그러면 이 처리가 빠져 홀드가 끝나도 레인이 눌린 채 남았고 시간 캐시(`CurrentTimeSeconds`)가 리트라이 때 이전 판 값으로 남았습니다.
  게임은 원본 차트의 `98` 오픈 노트로도 이 플래그를 직접 켜고 끕니다(`CheckOpenState`).
- **올퍼펙트**: `TargetMethods()`가 아래 타입×메서드 이름 조합 중 **첫 인자가 `EJudges`인 메서드**만 골라 패치합니다.
  - 타입: `RhythmGame.Play.RG_PS_Judgement`, `JudgeCounter`, `JudgeTextViewer`, `RedStarCounter`, `FastSlowMeter`
  - 메서드: `JudgeAction`, `JudgeDivergence`, `TryJudgeShortNote`, `AddJudge`, `OnGetJudge`
  - `TryJudgeShortNote(float, Note)`는 첫 인자가 `float`라 실제로는 제외됩니다. 파생 위젯 타입에서 `GetMethods()`로 찾기 때문에
    상속된 `PlayWidget.OnGetJudge` 베이스 메서드도 대상에 들어가며, 같은 메서드가 여러 타입에서 잡혀도 **한 번만** 패치합니다.
  - 시작 시 `[AllPerfect] 판정 메서드 N개를 패치합니다`를 남기고, 하나도 못 찾으면 경고를 남깁니다.

### ResultSaveBlockHook / ResultTaintHook / ServerGuardHook — 기록 보호 (`Hooks/ResultGuardHooks.cs`)

결과 화면과 서버로 나가는 것을 막는 훅 모음입니다(2026-10-05 정리).

- **저장/전송 차단 (`ResultSaveBlockHook`)**: `UserAccountModule.SavePlayData`(하이스코어 파일 저장)와 `LyrebirdServer.PostUserScore`(서버 전송)를
  건너뜁니다. 예전에는 이 두 메서드를 부르는 래퍼 `ManagerResult.ComparePlayResultHighScore`/`PostRequestPlayResult`를 통째로 건너뛰어, 래퍼 끝의
  베스트 점수 표시 갱신(`ManagerResult.cs:300-301`)까지 사라졌습니다. 이제 결과 화면은 원본처럼 베스트 점수를 표시하고(메모리에서만 계산, 저장 안 함),
  두 말단 메서드는 원본에서 `ManagerResult` 말고는 부르는 곳이 없습니다.
  - 차단 사유 우선순위: 오토플레이 → 올퍼펙트 → 점수 상한 변경(`MaxScore`≠1000000) → **커스텀 곡**(`SavePlayData`의 `CustomTrackData` 또는
    `PostUserScore`의 `CUSTOM_…` ID) → `BlockSave`. 앞의 네 가지는 `BlockSave`와 무관하게 항상 막고, 원본 곡은 `config.txt`의 `BlockSave`만 따릅니다.
    MelonPreferences의 `BlockSaveBestRanking`은 더 이상 읽지 않습니다.
  - 차단되면 `[차단] 하이스코어 및 랭킹 저장 차단(사유): …` 로그가 남습니다(`SavePlayData`는 새 기록이 있을 때만 불림). 대상 메서드를 못 찾으면
    시작 시 경고 로그가 남습니다. 두 대상은 `void`라 Prefix로 건너뛰어도 반환값 문제가 없습니다.
- **업적/카운터 보호 (`ResultTaintHook`)**: `ManagerResult.Start`는 `playCount++`, `failCount++`, `lastPlayedTrackID`/`sameTrackPlayCount` 갱신과
  Steam 업적(`PUREBLUE_FIRST`, `FULLCOMBO_FIRST` 등)을 `ResultSaveBlockHook` 밖에서 처리합니다. 오토/올퍼펙트/점수 상한 변경/커스텀 곡일 때
  Prefix에서 값을 기록해 두고 **Finalizer**(예외가 나도 실행됨)에서 되돌리며, 그동안 `UserAccountModule.RequestAchievementUnlock`을 막습니다.
  평범한 원본 곡 플레이는 건드리지 않습니다. 로그: `[ResultTaint] 이번 결과는 업적/플레이 횟수에 반영하지 않습니다`.
- **서버 보호 (`ServerGuardHook`)**: 곡을 시작할 때 원본이 서버에 보내는 `IncreaseTrackPlayCount`에서 커스텀 곡 ID를 막고, 랭킹 조회
  (`GetHighScoreList`)는 서버에 묻지 않고 빈 목록을 바로 돌려줍니다. `Util.FindTrackByID`가 커스텀 ID를 못 찾아 랭킹 창
  (`RG_RankingView.ShowFetch`)에서 `NullReferenceException`이 나던 것은 Postfix로 커스텀 곡 목록에서 찾아 주어 해결했습니다.
### JudgeScoreMaxHook — 점수 상한

`RG_PS_Judgement.Update()`와 `CalculateJudgeScore(float)` 안의 `ldc.r4 1000000`을 전부
`JudgeScoreMaxHook.GetMaxScore()` 호출로 바꿉니다. `Prepare()`는 설정을 먼저 읽어두고(`EnsureInitialized`)
**항상 `true`**를 반환합니다. 예전에는 기본값이면 패치를 건너뛰었지만, v1.1.0부터 설정을 플레이마다 다시
읽으므로 훅이 항상 붙어 있어야 값 변경이 반영됩니다. 기본값(1000000)이면 동작은 원본과 같습니다.
자세한 내용은 `SCORE_SYSTEM.md`.

### NoteSwayHook / NoteSpeedChaosHook

둘 다 `RG_NoteObject.CalculatePosition(curTime)` Postfix입니다. Sway는 루트 `RectTransform`의 x만,
Chaos는 자식(`shortNote`/`holdMask`/`holdTexture`/`tailNote`)의 y와 홀드 길이만 건드리므로 서로 간섭하지 않습니다.
상태는 노트 인스턴스 ID로 캐시하고 씬 전환 때 `Reset()`으로 비웁니다. 자세한 식은 `NOTE_SYSTEM.md`.

### FastSlowMeter_OnGetJudge_Patch — 판정바 데이터

게임은 판정이 확정되면 `ManagerPlay.WidgeInvoke`로 **장착된 PlayWidget 전부**에 `OnGetJudge(EJudges, float)`를
호출합니다. 이 패치는 `FastSlowMeter`의 오버라이드와, `JudgeTextViewer`를 통해 잡히는 베이스
`PlayWidget.OnGetJudge(EJudges, float)`를 함께 패치합니다(같은 메서드는 한 번만, 하나도 못 찾으면 경고). 그 결과:

- 히트 1회가 **장착한 위젯 수만큼** 들어오지만, `JudgmentBar.RegisterHit`이 **같은 프레임의 같은 (판정, 오차)**를 한 번만 받아서 틱이 겹쳐 진하게
  보이거나 히트 수가 부풀려지지 않습니다.
- 위젯을 **하나도 장착하지 않으면** 호출 자체가 없어 판정바에 틱이 나오지 않습니다(남은 문제).

정확히 노트당 1회가 필요하면 `RG_PS_Judgement.TryJudgeShortNote`(및 홀드 틱 판정 경로)로 옮겨야 합니다.
자세한 내용은 `PLAY_OVERLAY.md`.

---

## 씬 전환과 매 프레임 호출 (훅이 아닌 부분)

`Main`이 `SceneManager.activeSceneChanged`를 구독해 `UpdatePlaySceneState(sceneName)`을 호출합니다.

- 씬 이름을 소문자로 바꿔 `play`/`rhythm`/`game`이 들어 있으면 `Main.IsPlayScene = true`(설정 재로드 대상).
  게임 씬 이름은 `MainTitle`, `MusicSelect`, `PlayLoading`, `Play`, `Result` 등이므로 `PlayLoading`도 포함됩니다. 로딩 씬에서 미리 읽어 두면
  Play 씬의 Awake/Start가 항상 최신 값을 보기 때문에 일부러 그대로 둡니다.
- **오버레이를 그릴 씬은 따로 판단합니다**: 씬 이름이 정확히 `Play`일 때만 `Main.IsGameplayScene = true`. 그래서 로딩 화면(`PlayLoading`)에는
  빈 판정바/키뷰어가 나오지 않습니다.
- 플레이 씬이면 `SaveCustomKeyConfig.Reload(...)`로 `config.txt`를 다시 읽습니다(v1.1.0).
- 매번 `KeyViewer.Reset()`, `NoteSwayHook.Reset()`, `NoteSpeedChaosHook.Reset()` 호출.

`Main.OnUpdate`는 매 프레임 `BGABGMSyncHook.CheckAndSync()`를 호출하고, 오버레이 씬이면 `KeyViewer.Poll()`,
`JudgmentBar.RefreshJudgeRange()`를 호출합니다. `Main.OnGUI`는 오버레이 씬에서 판정바와 키뷰어를 그립니다(`Repaint` 이벤트에서만).

초기화 순서(`Main.OnInitializeMelon`): 설정 읽기 → `hwa` 폴더 → **씬 감지 구독** → 노트 스킨 초기화(별도 `try`). 선택 기능인 노트 스킨 초기화가
실패해도 씬 감지와 오버레이, 설정 재로드는 계속 동작합니다.