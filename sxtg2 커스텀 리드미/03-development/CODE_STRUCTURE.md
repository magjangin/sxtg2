# 코드 구조

기준일: 2026-10-05 (v1.1.0, 일괄 수정 브랜치 `fix/known-issues-batch`)

이 문서는 현재 `sxtg2-mod/` 코드 기준입니다. `bin`, `obj`, `.vs` 산출물은 제외합니다.

## 한눈에 보기

- C# 파일: **16개**, 약 5,100줄 (`Properties/AssemblyInfo.cs` 포함)
- 대상 프레임워크: .NET Framework 4.7.2, C# 9.0, x64
- 게임 어셈블리(`Assembly-CSharp.dll`)를 **직접 참조**합니다. 그래서 게임 타입(`ManagerPlay`, `SXGTData`,
  `RG_NoteObject` 등)을 이름 그대로 쓰고, 리플렉션은 private 필드/메서드 접근에만 씁니다
  (`AccessTools.FieldRefAccess`, `AccessTools.Method` 등). 필드 접근은 `SafeAccess`(`GameplayHooks.cs`)를 거쳐 실패해도 경고만 남깁니다.
- Harmony 패치는 전부 `[HarmonyPatch]` 속성으로 선언되어 있고, MelonLoader가 모드 로드 시 어셈블리 전체를
  자동으로 `PatchAll` 합니다. 수동 `harmony.Patch(...)` 호출이나 `Initialize()`에서 훅을 거는 코드는 없습니다.

## 디렉터리 구조

```text
sxtg2-mod/
├── Main/Main.cs                         # MelonMod 진입점, 씬 전환 감지, OnUpdate/OnGUI 분배
├── Features/
│   ├── MusicSelectFeature.cs            # 커스텀 트랙 주입(TrackDataAnalyzer), 미리듣기 교체
│   ├── DiagnosticHooks.cs               # 진단용 로깅 훅 (LogLevel=2일 때만 동작)
│   ├── JudgmentBarFeature.cs            # 판정바 오버레이 + OnGetJudge 후킹
│   └── KeyViewerFeature.cs              # 키뷰어 오버레이
├── Hooks/
│   ├── GameplayHooks.cs                 # 플레이 씬 훅 모음 (차트/미디어 교체, 노트 연출, 오토플레이, 점수 상한 등) + SafeAccess
│   ├── ResultGuardHooks.cs              # 기록·업적 차단 (저장/랭킹 전송, 플레이 횟수·업적, 서버 요청)
│   └── AudioHooks.cs                    # BGM/BGA 파일 탐색·로드·동기화├── Loaders/
│   ├── BmsParser.cs                     # BMS 텍스트 → ParsedNote
│   ├── TrackInfoParser.cs               # trackinfo.txt 등 → 제목/아티스트/난이도
│   └── CustomNoteLoaders.cs             # CustomNotes/*.png 로드, 노트 Image 갱신
├── Processors/CustomChartInjector.cs    # ParsedNote → 게임 ShortNote/HoldNote, SXGTData 갱신
├── Models/CustomTrackData.cs            # TrackData 파생형 (앨범 폴더/BMS 경로/도너 트랙 보관)
├── Helpers/
│   ├── ModHelpers.cs                    # 로그, config.txt 설정, 썸네일 로더
│   └── ConfigParsing.cs                 # 설정 값 파서 (Unity 비의존, LogicTests가 링크)
└── Properties/AssemblyInfo.cs           # 어셈블리 정보 + ModInfo.Version(버전 문자열의 단일 출처)
```

## 파일별 내용

| 파일 | 네임스페이스 | 주요 타입 | 역할 |
| --- | --- | --- | --- |
| `Main/Main.cs` | `sxtg2` | `Main` | 초기화(`OnInitializeMelon`), `activeSceneChanged`로 씬 판별(`IsPlayScene`/`IsGameplayScene`)과 설정 재로드, 매 프레임 `BGABGMSyncHook`/`KeyViewer`/`JudgmentBar` 호출 |
| `Features/MusicSelectFeature.cs` | `sxtg2.Features` | `TrackDataAnalyzer`, `ManagerMusicSelectHook` | `hwa` 폴더 스캔 → `CustomTrackData` 생성 → `trackDatas`에 추가(+ 커스텀 트랙 등록부), 커스텀 트랙 미리듣기 교체 |
| `Features/DiagnosticHooks.cs` | `sxtg2.Features` | `MusicSelectDiagnosticsHook`, `OperatorCharacterHook`, `ManagerResultHook` | **조사용 로깅**만 함. `LogLevel=2`(상세)가 아니면 아무것도 하지 않음 |
| `Features/JudgmentBarFeature.cs` | `sxtg2.Features` | `JudgmentBar`, `FastSlowMeter_OnGetJudge_Patch`, `HitTick` | 판정 오차 수집(`OnGetJudge` Postfix)과 IMGUI 렌더링 |
| `Features/KeyViewerFeature.cs` | `sxtg2.Features` | `KeyViewer` | 7레인 입력 상태 폴링/렌더링 |
| `Hooks/GameplayHooks.cs` | `sxtg2.Hooks` | `ManagerPlayHook`, `NoteSpriteHook`, `NoteSwayHook`, `NoteSpeedChaosHook`, `AutoPlayHook`, `AllPerfectJudgeHook`, `JudgeScoreMaxHook`, `TrackDataMediaHook`, `SafeAccess` | 플레이 관련 Harmony 훅 (자세한 목록은 `02-systems/HOOK_SYSTEM.md`) |
| `Hooks/ResultGuardHooks.cs` | `sxtg2.Hooks` | `ResultSaveBlockHook`, `ResultTaintHook`, `ServerGuardHook` | 저장/랭킹 전송, 플레이 횟수·업적, 커스텀 곡의 서버 요청 차단 |
| `Hooks/AudioHooks.cs` | `sxtg2.Hooks.Audio` | `MediaUrl`, `BgaFileResolver`, `BgmFileResolver`, `BGAPlayerHook`, `BGMPlayerHook`, `BGABGMSyncHook` | 앨범 폴더에서 `.mp4`/`.ogg`·`.mp3`·`.wav` 찾기, `UnityWebRequest`로 BGM 로드, BGA URL 교체, BGA↔BGM 싱크 |
| `Loaders/BmsParser.cs` | `sxtg2.Loaders` | `BmsParser` (+ `ParsedNote`, `ParseResult`, `ParseStatistics`) | BMS 파싱, 홀드 짝 맞추기, 파일 캐시 |
| `Loaders/TrackInfoParser.cs` | `sxtg2.Loaders` | `TrackInfoParser` | 곡 정보 txt 파싱 |
| `Loaders/CustomNoteLoaders.cs` | `sxtg2.Loaders` | `CustomNoteSpriteLoader` | `CustomNotes/*.png` → `Sprite` 캐시 (노트 이름의 `(Clone)` 접미사는 떼고 비교) |
| `Processors/CustomChartInjector.cs` | `sxtg2.Processors` | `CustomChartInjector` | 임시 리스트에 노트 생성 → 성공하면 레인에 적용 → `bpm`/`totalNotes`/`totalNoteWithTicks`/`scorePerNote`/`trackStartTiming` 갱신 (실패하면 게임 데이터는 그대로) |
| `Models/CustomTrackData.cs` | `sxtg2.Models` | `CustomTrackData` | `AlbumFolder`, `BmsPath`, `ResourceDonor`, `CustomJacket`, `JacketSearched`, ID 접두 `CUSTOM_`(`IsCustomId`) |
| `Helpers/ModHelpers.cs` | `sxtg2.Helpers`, `sxtg2.Helpers.Track` | `ModLog`, `SaveCustomKeyConfig`, `ThumbnailLoader` | MelonPreferences 로그 레벨, `SaveCustomKey/config.txt` 생성·파싱(표 기반)·재로드, 자켓 PNG 로드 |
| `Helpers/ConfigParsing.cs` | `sxtg2.Helpers` | `ConfigParsing` | 설정 값 파서(주석 제거, 불리언·실수·모양·위치). Unity 없이 컴파일되어 테스트에서 그대로 씀 |

## 모드가 만드는 폴더/파일

| 경로 (게임 설치 폴더 기준) | 만드는 곳 | 용도 |
| --- | --- | --- |
| `hwa\` | `Main.OnInitializeMelon` | 커스텀 곡(앨범 폴더) |
| `CustomNotes\` | `CustomNoteSpriteLoader.Initialize` | 커스텀 노트 스프라이트 PNG |
| `SaveCustomKey\config.txt` | `SaveCustomKeyConfig.LoadFromDisk` | 모드 설정 파일 (없으면 기본값으로 생성) |
| `UserData\MelonPreferences.cfg`의 `[sxtg2]` | `ModLog.RegisterPreferences` | `LogLevel` 등 MelonPreferences 항목 |

## 빌드와 테스트

- `build.bat [Debug|Release]`(기본 Debug): MSBuild로 `sxtg2.sln`을 **x64**로 빌드하고 `{GAME_PATH}\Mods\sxtg2.dll`로 **복사(배포)** 합니다.
  `build-release.bat`은 `build.bat Release`를 부르는 얇은 래퍼입니다. 환경 변수 `GAME_PATH`로 복사 대상 게임 폴더를 바꿀 수 있고
  (기본 `H:\Sixtar Gate STARTRAIL custom mode`), `NO_PAUSE=1`이면 마지막 `pause`를 건너뜁니다. 저장소 위치는 스크립트 위치(`%~dp0`)에서 구합니다.
  빌드 전에 실행 중인 컴파일러 서버를 죽이지 않습니다(`UseSharedCompilation=false`).
- `Platform=x64`로 빌드하므로 csproj의 `Debug|x64`/`Release|x64` 블록이 적용됩니다(Release 최적화, Debug의 `DEBUG` 상수). 예전에는 `Any CPU`로
  빌드해 이 블록이 무시됐습니다.
- 게임 DLL 참조 경로(`H:\Sixtar Gate STARTRAIL custom mode\...`)는 `sxtg2-mod/sxtg2.csproj`의 `HintPath`에 **하드코딩**되어 있습니다.
  `GAME_PATH`/`/p:GamePath`는 **복사 위치**만 바꾸고 참조 DLL은 바꾸지 않으므로, 게임이 다른 경로에 있으면 csproj도 고쳐야 합니다.
  테스트 빌드는 `set GAME_PATH=임시폴더`로 실제 게임 폴더에 배포하지 않고 확인할 수 있습니다.
- `.gitattributes`가 `*.bat`을 CRLF로 고정합니다(작업 트리 줄바꿈이 파일마다 다르던 문제 해결).
- `sxtg2.LogicTests/`: .NET 8 콘솔 프로젝트. `BmsParser.cs`와 `ConfigParsing.cs`를 링크로 컴파일하고 `MelonLoggerStub.cs`로
  MelonLogger를 대체합니다. `run-logic-tests.bat`로 실행하며 현재 **13개** 테스트가 있습니다(BMS 헤더 형식, `WAVCMD` 제외, BPM 범위,
  홀드/오픈 노트 짝 통계, 설정 주석 제거와 값 파싱 등). 설정 파서가 `ConfigParsing`으로 분리되어 이제 **실제 구현**을 테스트합니다(예전엔 함수
  복사본이라 구현이 바뀌어도 따라가지 않았음). `sxtg2.sln`에는 포함하지 않았습니다(Visual Studio 테스트 탐색기에는 안 보임, 빌드 환경 의존을 피하려는 선택).
- `tools/method_length_scan.py`: 모드 코드(`sxtg2-mod/`)의 메서드 길이를 대략 측정합니다(60줄 이상을 보여 줌). 2026-10-05 일괄 수정 후에는
  60줄 이상인 메서드가 없습니다(수정 전에는 `LoadConfigFile` 160, `DrawJudgmentBar` 136, `CheckAndSync` 81, `KeyViewer.Draw` 73,
  `LogResultOperatorLayer` 72, `ParseColorSetting` 67, `InjectBmsNotesToLaneData` 64줄). 일회성 도구였던 `tools/merge_partial_classes.py`와
  `clean.bat`은 삭제했습니다.

### 저장소에 남은 정리 후보

- `release/`에 옛 배포물(`sxtg2-v0.1.0.zip`, `sxtg2-v0.1.1.zip`, `sxtg2.dll`)이 Git에 추적되어 있고, 루트의 `list_managed_games.txt`도 용도가
  불분명합니다. 지울지 여부는 사용자가 정하도록 그대로 두었습니다.
- 버전 문자열은 `AssemblyInfo.cs`의 `ModInfo.Version` 한 곳이 `MelonInfo`와 `AssemblyVersion`/`FileVersion`에 모두 쓰입니다. README의 버전 표기만 따로
  고치면 됩니다.
## 이전 구조와의 관계

2026-05-15 기준 문서들은 87개 파일(`TextHook`, `SXGTDataHook`, `CustomPlayStartupFlow`, `BmsFileResolver`,
`SceneDetector`, `ReflectionHelper` 등)을 설명했습니다. 이 구조는 다음 커밋들을 거치며 13개 파일로 줄었고, 2026-10-05 일괄 수정에서 책임을 나누며(`ConfigParsing`, `ResultGuardHooks`, `DiagnosticHooks`) 16개가 됐습니다.

| 날짜 | 커밋 | 내용 | 이후 C# 파일 수 |
| --- | --- | --- | --- |
| 2026-07-21 | `95674e8` | partial 클래스 병합, 죽은/진단 코드 정리 | 42 |
| 2026-07-23 | `53f4174` | 수동 Harmony 패치를 `[HarmonyPatch]` 선언형으로 전환 | |
| 2026-07-26 | `ba60f89` | 커스텀 차트 파이프라인 단순화 (`TextHook`/`SXGTDataHook` 경로 제거, `FetchBMSToModules` Prefix 하나로 통합) | 20 |
| 2026-07-26 | `12348f5` | 파일 통합(`Hooks/*` 폴더 → `GameplayHooks.cs`/`AudioHooks.cs` 등) | 12 |
| 2026-07-26 | `d25736f` | 키뷰어 추가(`KeyViewerFeature.cs`) | 13 |

옛 구조를 설명하던 상세 문서(`LANE_EXTRACTION`, `TYPE_SYSTEM`, `IMPLEMENTATION`, `PARSING_ALGORITHM`,
`AUDIO_SYSTEM`, `VIDEO_SYSTEM`)는 `90-archive/`로 옮겼습니다.
