# 코드 구조

기준일: 2026-09-28 (v1.1.0, 커밋 `2093826`)

이 문서는 현재 `sxtg2-mod/` 코드 기준입니다. `bin`, `obj`, `.vs` 산출물은 제외합니다.

## 한눈에 보기

- C# 파일: **13개**, 약 4,370줄 (`Properties/AssemblyInfo.cs` 포함)
- 대상 프레임워크: .NET Framework 4.7.2, C# 9.0, x64
- 게임 어셈블리(`Assembly-CSharp.dll`)를 **직접 참조**합니다. 그래서 게임 타입(`ManagerPlay`, `SXGTData`,
  `RG_NoteObject` 등)을 이름 그대로 쓰고, 리플렉션은 private 필드/메서드 접근에만 씁니다
  (`AccessTools.FieldRefAccess`, `AccessTools.Method` 등).
- Harmony 패치는 전부 `[HarmonyPatch]` 속성으로 선언되어 있고, MelonLoader가 모드 로드 시 어셈블리 전체를
  자동으로 `PatchAll` 합니다. 수동 `harmony.Patch(...)` 호출이나 `Initialize()`에서 훅을 거는 코드는 없습니다.

## 디렉터리 구조

```text
sxtg2-mod/
├── Main/Main.cs                         # MelonMod 진입점, 씬 전환 감지, OnUpdate/OnGUI 분배
├── Features/
│   ├── MusicSelectFeature.cs            # 커스텀 트랙 주입, 미리듣기 교체, (진단용) 오퍼레이터/결과 씬 로깅
│   ├── JudgmentBarFeature.cs            # 판정바 오버레이 + OnGetJudge 후킹
│   └── KeyViewerFeature.cs              # 키뷰어 오버레이
├── Hooks/
│   ├── GameplayHooks.cs                 # 플레이 씬 훅 모음 (차트/미디어 교체, 노트 연출, 오토플레이 등)
│   └── AudioHooks.cs                    # BGM/BGA 파일 탐색·로드·동기화
├── Loaders/
│   ├── BmsParser.cs                     # BMS 텍스트 → ParsedNote
│   ├── TrackInfoParser.cs               # trackinfo.txt 등 → 제목/아티스트/난이도
│   └── CustomNoteLoaders.cs             # CustomNotes/*.png 로드, 노트 Image 갱신
├── Processors/CustomChartInjector.cs    # ParsedNote → 게임 ShortNote/HoldNote, SXGTData 갱신
├── Models/CustomTrackData.cs            # TrackData 파생형 (앨범 폴더/BMS 경로/도너 트랙 보관)
├── Helpers/ModHelpers.cs                # 로그, config.txt 설정, 썸네일 로더
└── Properties/AssemblyInfo.cs
```

## 파일별 내용

| 파일 | 네임스페이스 | 주요 타입 | 역할 |
| --- | --- | --- | --- |
| `Main/Main.cs` | `sxtg2` | `Main` | 초기화(`OnInitializeMelon`), `activeSceneChanged`로 플레이 씬 판별과 설정 재로드, 매 프레임 `BGABGMSyncHook`/`KeyViewer`/`JudgmentBar` 호출 |
| `Features/MusicSelectFeature.cs` | `sxtg2.Features` | `TrackDataAnalyzer`, `ManagerMusicSelectHook`, `OperatorCharacterHook`, `ManagerResultHook` | `hwa` 폴더 스캔 → `CustomTrackData` 생성 → `trackDatas`에 추가, 커스텀 트랙 미리듣기 교체. 오퍼레이터/결과 씬 훅은 **조사용 로깅**만 함 |
| `Features/JudgmentBarFeature.cs` | `sxtg2.Features` | `JudgmentBar`, `FastSlowMeter_OnGetJudge_Patch`, `HitTick` | 판정 오차 수집(`OnGetJudge` Postfix)과 IMGUI 렌더링 |
| `Features/KeyViewerFeature.cs` | `sxtg2.Features` | `KeyViewer` | 7레인 입력 상태 폴링/렌더링 |
| `Hooks/GameplayHooks.cs` | `sxtg2.Hooks` | `ManagerPlayHook`, `NoteSpriteHook`, `NoteSwayHook`, `NoteSpeedChaosHook`, `AutoPlayHook`, `AllPerfectJudgeHook`, `ResultSaveBlockHook`, `JudgeScoreMaxHook`, `TrackDataMediaHook` | 플레이 관련 Harmony 훅 전부 (자세한 목록은 `02-systems/HOOK_SYSTEM.md`) |
| `Hooks/AudioHooks.cs` | `sxtg2.Hooks.Audio` | `BgaFileResolver`, `BgmFileResolver`, `BGAPlayerHook`, `BGMPlayerHook`, `BGABGMSyncHook` | 앨범 폴더에서 `.mp4`/`.ogg`·`.mp3`·`.wav` 찾기, `UnityWebRequest`로 BGM 로드, BGA URL 교체, BGA↔BGM 싱크 |
| `Loaders/BmsParser.cs` | `sxtg2.Loaders` | `BmsParser` (+ `ParsedNote`, `ParseResult`, `ParseStatistics`) | BMS 파싱, 홀드 짝 맞추기, 파일 캐시 |
| `Loaders/TrackInfoParser.cs` | `sxtg2.Loaders` | `TrackInfoParser` | 곡 정보 txt 파싱 |
| `Loaders/CustomNoteLoaders.cs` | `sxtg2.Loaders`, `sxtg2.Helpers.UI` | `CustomNoteSpriteLoader`, `NoteRendererRecovery` | `CustomNotes/*.png` → `Sprite` 캐시, 노트 `Image` 갱신 |
| `Processors/CustomChartInjector.cs` | `sxtg2.Processors` | `CustomChartInjector` | 레인 비우기 → 게임 노트 생성·추가 → 정렬 → `bpm`/`totalNotes`/`totalNoteWithTicks`/`scorePerNote` 갱신 |
| `Models/CustomTrackData.cs` | `sxtg2.Models` | `CustomTrackData` | `AlbumFolder`, `BmsPath`, `ResourceDonor`, `CustomJacket` |
| `Helpers/ModHelpers.cs` | `sxtg2.Helpers`, `sxtg2.Helpers.Track` | `ModLog`, `SaveCustomKeyConfig`, `ThumbnailLoader` | MelonPreferences 로그 레벨, `SaveCustomKey/config.txt` 생성·파싱·재로드, 자켓 PNG 로드 |

## 모드가 만드는 폴더/파일

| 경로 (게임 설치 폴더 기준) | 만드는 곳 | 용도 |
| --- | --- | --- |
| `hwa\` | `Main.OnInitializeMelon` | 커스텀 곡(앨범 폴더) |
| `CustomNotes\` | `CustomNoteSpriteLoader.Initialize` | 커스텀 노트 스프라이트 PNG |
| `SaveCustomKey\config.txt` | `SaveCustomKeyConfig.LoadFromDisk` | 모드 설정 파일 (없으면 기본값으로 생성) |
| `UserData\MelonPreferences.cfg`의 `[sxtg2]` | `ModLog.RegisterPreferences` | `LogLevel` 등 MelonPreferences 항목 |

## 빌드와 테스트

- `build.bat` / `build-release.bat`: MSBuild로 `sxtg2.sln`을 빌드하고 `{GAME_PATH}\Mods\sxtg2.dll`로 복사합니다.
  두 스크립트는 구성(Debug/Release)만 다르고 내용은 같습니다.
- 게임 DLL 참조 경로(`H:\Sixtar Gate STARTRAIL custom mode\...`)는 `sxtg2-mod/sxtg2.csproj`에 **하드코딩**되어
  있습니다. 스크립트가 넘기는 `/p:GamePath`는 csproj에서 쓰이지 않으므로, 게임 경로가 다르면 csproj의
  `HintPath`/`Reference Include`도 함께 고쳐야 합니다.
- 스크립트의 `SOURCE_ROOT`(`H:\source\repos\sxtg2`)도 하드코딩입니다. 저장소를 다른 곳에 두면 빌드는 새
  위치에서 되지만 복사는 `SOURCE_ROOT` 쪽 DLL을 가져가므로 함께 고쳐야 합니다.
- `sxtg2.LogicTests/`: .NET 8 콘솔 프로젝트. `BmsParser.cs`를 링크로 컴파일하고 `MelonLoggerStub.cs`로
  MelonLogger를 대체합니다. `run-logic-tests.bat`로 실행하며 현재 7개 테스트가 있습니다.
  - `ParseFlexibleBool` 테스트는 `ModHelpers.cs`가 Unity에 의존해 링크할 수 없어서 **함수 복사본**을
    테스트합니다. 실제 구현이 바뀌어도 테스트는 따라가지 않으니 주의하세요.
- `tools/method_length_scan.py`: 메서드 길이 대략 측정 스크립트.
  `tools/merge_partial_classes.py`: 2026-07-21 partial 클래스 병합(`95674e8`)에 쓴 일회성 스크립트로,
  대상 파일이 이미 없어 지금은 쓸 일이 없습니다.

### 알려진 빌드 설정 문제 (확인 필요)

`build.bat`/`build-release.bat`는 `/p:Platform="Any CPU"`로 솔루션을 빌드하고, 솔루션은 이를 프로젝트의
`Any CPU` 구성으로 넘깁니다. 그런데 csproj에는 `Debug|x64`, `Release|x64` 조건 블록만 있어서 이 블록이
적용되지 않습니다. 결과적으로 Release 빌드에 `Optimize=true`가 걸리지 않고, Debug 빌드에도
`DEBUG`/`TRACE` 상수가 정의되지 않습니다(출력 경로와 `PlatformTarget=x64`는 공통 블록이라 정상).

## 이전 구조와의 관계

2026-05-15 기준 문서들은 87개 파일(`TextHook`, `SXGTDataHook`, `CustomPlayStartupFlow`, `BmsFileResolver`,
`SceneDetector`, `ReflectionHelper` 등)을 설명했습니다. 이 구조는 다음 커밋들을 거치며 지금의 13개 파일로
바뀌었습니다.

| 날짜 | 커밋 | 내용 | 이후 C# 파일 수 |
| --- | --- | --- | --- |
| 2026-07-21 | `95674e8` | partial 클래스 병합, 죽은/진단 코드 정리 | 42 |
| 2026-07-23 | `53f4174` | 수동 Harmony 패치를 `[HarmonyPatch]` 선언형으로 전환 | |
| 2026-07-26 | `ba60f89` | 커스텀 차트 파이프라인 단순화 (`TextHook`/`SXGTDataHook` 경로 제거, `FetchBMSToModules` Prefix 하나로 통합) | 20 |
| 2026-07-26 | `12348f5` | 파일 통합(`Hooks/*` 폴더 → `GameplayHooks.cs`/`AudioHooks.cs` 등) | 12 |
| 2026-07-26 | `d25736f` | 키뷰어 추가(`KeyViewerFeature.cs`) | 13 |

옛 구조를 설명하던 상세 문서(`LANE_EXTRACTION`, `TYPE_SYSTEM`, `IMPLEMENTATION`, `PARSING_ALGORITHM`,
`AUDIO_SYSTEM`, `VIDEO_SYSTEM`)는 `90-archive/`로 옮겼습니다.
