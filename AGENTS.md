# sxtg2 — 인수인계 문서 (AGENTS.md)

> 작성일 2026-11-15. 폴더의 코드·프로젝트 파일·기존 문서와 실제 게임 설치 폴더(Steam `.acf`, `UnityPlayer.dll`, `MelonLoader.dll`)를 읽어 정리했다.
> 이 문서 작성 시점에 인게임 재검증은 하지 않았다. **"실측"** 표시는 디스크에서 직접 읽은 값, **"미확인"**은 다음 세션에서 확인할 항목이다.

## 0. 프로젝트 개요

- **무엇인가:** Sixtar Gate STARTRAIL 커스텀 차트 모드(v1.4.0). 로컬 `hwa` 폴더의 BMS 차트·미디어를 불러와 노트 주입, 음악 선택 흐름에 커스텀 TrackData 추가, BGA/BGM/미리듣기/썸네일 교체. 모드 소스는 `sxtg2-mod/`, `sxtg2/`는 게임 Assembly-CSharp 디컴파일본, 테스트 `sxtg2.LogicTests`. 문서 `sxtg2 커스텀 리드미/`.
- **종류:** 게임 모드
- **최근 수정일(파일 기준):** 2026-11-05
- **git 원격:** `H:\local-repos\sxtg2.git`
- **최근 커밋:**
  - `a6a848a Add GitHub Sponsors funding configuration`
  - `963736f v1.4.0: Custom notes in the inventory, fade in/out, and a sectioned config`
  - `d662a72 feat: custom notes as an inventory skin, fade in/out, sectioned config`
  - `51e5b25 v1.3.1: Shuffle the custom marathon order without repeats`
  - `f48ec76 v1.3.0: Sudden/Hidden lane cover and a once-through custom track marathon`
- **먼저 읽을 기존 문서:**
  - [.cursor\skills\build-deploy-mod-bat\SKILL.md](.cursor/skills/build-deploy-mod-bat/SKILL.md)
  - [README.md](README.md)
  - [sxtg2 커스텀 리드미\00-overview\ARCHITECTURE.md](sxtg2%20커스텀%20리드미/00-overview/ARCHITECTURE.md)
  - [sxtg2 커스텀 리드미\00-overview\DATA_FLOW.md](sxtg2%20커스텀%20리드미/00-overview/DATA_FLOW.md)
  - [sxtg2 커스텀 리드미\00-overview\DOCUMENTATION.md](sxtg2%20커스텀%20리드미/00-overview/DOCUMENTATION.md)
  - [sxtg2 커스텀 리드미\00-overview\GLOSSARY.md](sxtg2%20커스텀%20리드미/00-overview/GLOSSARY.md)
  - [sxtg2 커스텀 리드미\00-overview\PROJECT_OVERVIEW.md](sxtg2%20커스텀%20리드미/00-overview/PROJECT_OVERVIEW.md)
  - [sxtg2 커스텀 리드미\01-user-guide\INSTALL_AND_LAYOUT.md](sxtg2%20커스텀%20리드미/01-user-guide/INSTALL_AND_LAYOUT.md)
  - [sxtg2 커스텀 리드미\01-user-guide\TROUBLESHOOTING.md](sxtg2%20커스텀%20리드미/01-user-guide/TROUBLESHOOTING.md)
  - [sxtg2 커스텀 리드미\02-systems\AUDIO_SYSTEM.md](sxtg2%20커스텀%20리드미/02-systems/AUDIO_SYSTEM.md)
  - [sxtg2 커스텀 리드미\02-systems\BMS_FORMAT.md](sxtg2%20커스텀%20리드미/02-systems/BMS_FORMAT.md)
  - [sxtg2 커스텀 리드미\02-systems\BMS_PARSING.md](sxtg2%20커스텀%20리드미/02-systems/BMS_PARSING.md)

## 1. 게임 버전 및 고정 여부

| 게임 | 설치 경로 | AppID | Steam Build ID (실측) | AutoUpdateBehavior | .acf 읽기 전용 |
| --- | --- | --- | --- | --- | --- |
| Sixtar Gate STARTRAIL custom mode | `H:\Sixtar Gate STARTRAIL custom mode` | - | 스팀 라이브러리 밖(수동 사본) | - | - |

- 스팀 라이브러리 밖의 수동 사본이라 스팀 자동 업데이트 영향은 받지 않는다. 사본의 원본 버전은 **미확인**.

## 2. 런타임 백엔드 및 디컴파일 경로

- **Sixtar Gate STARTRAIL custom mode:** Unity / **Mono** / Unity `2020.3.9f1` / x64 (실측)
  - 설치된 MelonLoader: `0.7.3.0 (net6)` (실측)
  - 참조 어셈블리: `H:\Sixtar Gate STARTRAIL custom mode\Sixtar Gate STARTRAIL_Data\Managed\`
  - 현재 `Mods/` 내용: `sxtg2.dll`
- 모드/앱 프로젝트 `sxtg2-mod\sxtg2.csproj` 타깃 프레임워크: `v4.7.2` (구형 csproj)
- 모드/앱 프로젝트 `sxtg2.LogicTests\sxtg2.LogicTests.csproj` 타깃 프레임워크: `net8.0` (SDK 스타일)
- 모드/앱 프로젝트 `tools\CsOutlineGenerator\CsOutlineGenerator.csproj` 타깃 프레임워크: `net10.0` (SDK 스타일)
- 보조 프로젝트(디컴파일본·덤퍼): `sxtg2\Assembly-CSharp.csproj`
- 디컴파일본은 이 폴더에 없다. Mono는 `ilspycmd`로 `<Game>_Data/Managed/Assembly-CSharp.dll`을, Il2Cpp는 `MelonLoader/Il2CppAssemblies/`를 SignatureDumper로 덤프해서 본다.

## 3. 설계 의도와 우회 사유 (Rationale)

코드 주석과 기존 문서에서 작성자가 남긴 의도·주의 문장을 그대로 옮겼다(파일:줄).

- `.cursor\skills\build-deploy-mod-bat\SKILL.md:16` — 반드시 다음 정보를 프로젝트에서 확인한 뒤 배치 변수에 반영한다.
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:61` — Minimal Template (핵심만)
- `sxtg2 커스텀 리드미\00-overview\ARCHITECTURE.md:44` — `BmsParser`: BMS 파일을 `ParsedNote`로 변환
- `sxtg2 커스텀 리드미\00-overview\ARCHITECTURE.md:51` — `CustomChartInjector`: `ParsedNote`를 `ShortNote`/`HoldNote` 계열 게임 노트로 변환하고 `laneData`에 추가
- `sxtg2 커스텀 리드미\00-overview\ARCHITECTURE.md:87` — `NoteDataExtractor`, `NoteGroupProcessor`, `NoteMatcher`
- `sxtg2 커스텀 리드미\00-overview\DOCUMENTATION.md:75` — `SXGTReaderHook` 기반 진단 후킹은 제거되었습니다. 현재 핵심 주입 경로는 `SXGTDataHook`와 `ManagerPlayHook`입니다.
- `sxtg2 커스텀 리드미\00-overview\DOCUMENTATION.md:91` — 별도의 `MaxScore` 보정 훅은 제거되었습니다. 원본 판정식은 `bms.totalNotes`와
- `sxtg2 커스텀 리드미\00-overview\DOCUMENTATION.md:92` — `bms.totalNoteWithTicks`를 사용합니다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:16` — CustomNotes 폴더의 PNG를 게임 인벤토리의 노트 스킨 목록에 "커스텀 노트" 항목으로 끼워 넣는다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:19` — 게임은 노트 프리팹(Util.GetNoteSkinDir)과 타격 이펙트(Lane.PlayShortEffect의 애니메이터 상태 이름)를
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:20` — 스킨 ID로 찾는다. "커스텀"이라는 진짜 스킨은 없으므로 userData.noteSkin에는 이 항목의 ID를 절대 남기지 않고
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:21` — 바탕 스킨(Default)의 ID를 넣어 둔다. 커스텀을 골랐는지는 config.txt의 CustomNotes가 기억한다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:44` — Setter_NoteSkin이 열리면서 원래 스킨 위치로 커서를 맞추는 중이면 CustomNotes를 건드리지 않는다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:46` — Setter_NoteSkin.ChangeCursorDelta 안에서 미리보기를 새로 만드는 중인지.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:80` — 원본은 userData.noteSkin 위치로 커서를 맞춘다. 커스텀이 장착돼 있으면 userData에는 바탕 스킨이 들어 있으므로
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:122` — 원본은 고른 항목의 ID를 userData.noteSkin에 그대로 넣는다. 커스텀 ID가 남으면 플레이에서 노트 프리팹을 못 찾으므로
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:123` — 바탕 스킨 ID로 바꾸고, 커스텀을 골랐는지는 CustomNotes(config.txt)에 기록한다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:152` — 인벤토리를 열 때(인자 없는 RefreshNoteGenerate → userData.noteSkin)는 커스텀이 장착돼 있으면 커스텀으로 보여 준다.

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:**
  - `"Lyrebird Studio", "Sixtar Gate STARTRAIL"`
  - `typeof(sxtg2.Main), "sxtg2", "1.4.0", "화영왕"`
- **Harmony 대상(소스에서 수집):**
  - `"Awake"`
  - `"BackToMusicSelect"`
  - `"CalculatePosition"`
  - `"CheckBGMStart"`
  - `"FetchBMSToModules"`
  - `"Generate"`
  - `"GetKeyMap"`
  - `"MoveToPlayLoadingScene"`
  - `"OpenConfirmWindow"`
  - `"PlayPreview"`
  - `"SetUp"`
  - `"ShowDialogue"`
  - `"Start"`
  - `"instantiateOperatorCharacter"`
  - `Manual: type, "CalculateJudgeScore", new[] { typeof(float) })`
  - `Manual: type, "ComparePlayResultHighScore")`
  - `Manual: type, "PostRequestPlayResult")`
  - `Manual: type, "Update")`
  - `Manual: typeof(JudgeScoreMaxHook), nameof(GetMaxScore))`
  - `Manual: typeof(ManagerResult), "BackToMusicSelect"))`
  - `Manual: typeof(RG_PS_Judgement), "AutoPlayJudge", new[] { typeof(float), typeof(int) })`
  - `nameof(TrackData.GetLoadingAnimation)`
  - `nameof(TrackData.GetThumbSprite)`
  - `typeof(CSVParser), nameof(CSVParser.GetLocalizedString)`
  - `typeof(ConfirmWindow), nameof(ConfirmWindow.RefreshItemInfo)`
  - `typeof(InventoryItemInfo), nameof(InventoryItemInfo.GetThumbnailSprite)`
  - `typeof(ManagerMusicSelect)`
  - `typeof(ManagerPlay)`
  - `typeof(ManagerResult)`
  - `typeof(NoteGenerator)`
  - 외 13개
- **주요 소스 파일(큰 순):**
  - `sxtg2-mod\Hooks\GameplayHooks.cs`
  - `sxtg2-mod\Helpers\ModHelpers.cs`
  - `sxtg2\RhythmGame\ManagerPlay.cs`
  - `sxtg2-mod\Features\MusicSelectFeature.cs`
  - `sxtg2\RhythmGame.Play\RG_PS_Judgement.cs`
  - `sxtg2\RhythmGame.MusicSelect\ManagerMusicSelect.cs`
  - `sxtg2-mod\Features\JudgmentBarFeature.cs`
  - `sxtg2.LogicTests\Program.cs`
  - `sxtg2-mod\Hooks\AudioHooks.cs`
  - `sxtg2-mod\Features\CustomMarathonFeature.cs`
  - `sxtg2\Live2D.Cubism.Framework.Json\CubismModel3Json.cs`
  - `tools\merge_partial_classes.py`

## 5. 시도했다가 실패한 것들 (Dead Ends)

코드 주석과 기존 문서에서 실패·크래시·금지 관련 문장을 그대로 옮겼다(파일:줄). 같은 방법을 다시 시도하기 전에 해당 위치를 읽을 것.

- `.cursor\skills\build-deploy-mod-bat\SKILL.md:11` — Builds the mod project with MSBuild (Debug 기본, 필요 시 Release 옵션 제공)
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:32` — `SOURCE_DLL` = `%SOURCE_ROOT%\%PROJECT_DIR%\bin\Debug\%DLL_NAME%` (또는 Release)
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:38` — 없으면 오류 메시지 출력 후 `exit /b 1`
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:43` — `Restore` 후 `Build`, Configuration=Debug(또는 Release), Platform="Any CPU" 또는 x64 등 csproj와 일치
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:44` — `if errorlevel 1`로 실패 시 pause 후 종료
- `.cursor\skills\build-deploy-mod-bat\SKILL.md:53` — 실패 시 `pause`로 창 유지 후 `exit /b 1`
- `sxtg2 커스텀 리드미\01-user-guide\INSTALL_AND_LAYOUT.md:7` — 빌드 결과: `sxtg2-mod\bin\Debug\sxtg2.dll`
- `sxtg2 커스텀 리드미\01-user-guide\TROUBLESHOOTING.md:10` — `{게임 설치 폴더}\hwa\`가 없으면 BMS/미디어 탐색이 실패합니다.
- `sxtg2-mod\Features\CustomNoteSkinFeature.cs:124` — 원본에서 예외가 나도 커스텀 ID가 남지 않도록 Finalizer에서 처리한다.
- `sxtg2-mod\Features\JudgmentBarFeature.cs:288` — 판정 범위가 자잘하게 바뀔 때마다 캐시가 무한정 쌓이는 걸 막는 안전장치
- `sxtg2-mod\Helpers\ModHelpers.cs:652` — 씬 전환 콜백에서 불리므로 여기서 예외가 새어나가면 다른 구독자까지 끊긴다.
- `sxtg2-mod\Helpers\ModHelpers.cs:742` — 읽기 실패 시점에 먼저 리셋해버리면 설정이 날아간다.
- `sxtg2-mod\Hooks\GameplayHooks.cs:218` — 플레이 중 프레임 예외 방지
- `sxtg2-mod\Hooks\GameplayHooks.cs:252` — 노트가 통째로 안 보였다(2026-10-11 실게임). 경계는 매 프레임 다시 계산하고, 계산에 실패하면 가리지 않는다.
- `sxtg2-mod\Hooks\GameplayHooks.cs:294` — 디버그 오버레이용: 마지막으로 계산한 판정선/경계의 월드 좌표와 그 캔버스.
- `sxtg2-mod\Hooks\GameplayHooks.cs:653` — LaneCoverDebug=1이면 계산한 판정선(빨강)·옛 앵커 기준 판정선(노랑)·가림 경계(하늘색)·서서히 구간 끝(연두)을
- `sxtg2-mod\Hooks\GameplayHooks.cs:722` — 첫 노트의 shortNote부터 루트 캔버스까지 배치 정보를 한 번 남긴다(좌표 문제 진단용).
- `sxtg2-mod\Hooks\GameplayHooks.cs:850` — 플레이 중 프레임 예외 방지

## 6. 빌드 명령 및 배포 경로

- 솔루션: `sxtg2.sln`
- `sxtg2-mod\sxtg2.csproj` (구형 csproj → Visual Studio MSBuild 필요):
  ```powershell
  msbuild "sxtg2-mod\sxtg2.csproj" /p:Configuration=Release
  ```
- `sxtg2.LogicTests\sxtg2.LogicTests.csproj`:
  ```powershell
  dotnet build "sxtg2.LogicTests\sxtg2.LogicTests.csproj" -c Release
  ```
- `tools\CsOutlineGenerator\CsOutlineGenerator.csproj`:
  ```powershell
  dotnet build "tools\CsOutlineGenerator\CsOutlineGenerator.csproj" -c Release
  ```
- 빌드/배포 스크립트: `build-release.bat`, `build.bat`, `clean.bat`, `run-logic-tests.bat`
- 배포는 스크립트가 처리한다: `build-release.bat`, `build.bat` (내용에 Mods 경로 복사 포함)
- 배포 위치: `H:\Sixtar Gate STARTRAIL custom mode\Mods\`

## 7. 다음 작업자에게

- 작업 전 1절의 Build ID와 현재 설치본 `.acf`의 `buildid`를 비교한다. 다르면 훅 대상 시그니처부터 다시 확인한다.
- 인게임 동작을 확인하면 3·5절의 **미확인** 항목을 실제 내용으로 바꾼다.
