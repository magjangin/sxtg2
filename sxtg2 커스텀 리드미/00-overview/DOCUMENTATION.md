# sxtg2 종합 문서 (전체 흐름)

기준일: 2026-09-28 (v1.1.0, 커밋 `2093826`)

모드가 게임 실행부터 결과 화면까지 **언제 무엇을 하는지**를 시간 순서로 정리합니다.
구조는 [ARCHITECTURE.md](ARCHITECTURE.md), 데이터 변환은 [DATA_FLOW.md](DATA_FLOW.md)를 보세요.

## 현재 코드의 핵심 원칙

- 실제 빌드 대상은 `sxtg2-mod/`(C# 13개 파일)입니다.
- 게임 어셈블리를 직접 참조하고, 훅은 전부 `[HarmonyPatch]` 선언형(MelonLoader 자동 적용)입니다.
- 커스텀 트랙은 **도너 트랙**(곡 목록 첫 곡)의 리소스를 빌려 정상 흐름을 타고, 노트/미디어만 교체합니다.
- 설정은 `SaveCustomKey/config.txt` 하나, 플레이 씬에 들어갈 때마다 다시 읽습니다.

## 1. 게임 실행 — 모드 초기화

```text
(MelonLoader) 어셈블리 PatchAll            // 모든 [HarmonyPatch] 적용
  └─ JudgeScoreMaxHook.Prepare → SaveCustomKeyConfig.EnsureInitialized (설정을 먼저 읽을 수 있음)

Main.OnInitializeMelon
  ├─ ModLog.RegisterPreferences            // MelonPreferences [sxtg2] 항목 등록
  ├─ SaveCustomKeyConfig.Initialize        // config.txt 없으면 생성, 읽기, 누락 항목 추가
  ├─ {게임}\hwa 폴더 생성
  ├─ NoteSpriteHook.Initialize → CustomNoteSpriteLoader.Initialize   // CustomNotes\*.png 로드
  ├─ SceneManager.activeSceneChanged 구독
  └─ 현재 씬으로 UpdatePlaySceneState (이때는 설정 재로드 안 함)
```

## 2. 곡 선택 화면 (`MusicSelect`)

```text
ManagerMusicSelect.Awake (원본: 곡 목록 새로 만듦)
  └─ ManagerMusicSelectHook.AwakePostfix
       └─ TrackDataAnalyzer.InjectCustomTracks
            hwa 루트(BMS 있을 때) + 1단계 하위 폴더마다 첫 BMS → CustomTrackData 추가
            로그: [TrackDataAnalyzer] 커스텀 트랙 N개 추가 완료

곡 커서 이동 → ManagerMusicSelect.PlayPreview
  └─ PlayPreviewPrefix: 커스텀 트랙이면 demo.* → music.* → 첫 오디오를 재생 (원본 미리듣기 차단)

곡 목록/확인창이 자켓 요청 → TrackData.GetJacketSprite()/GetThumbSprite()
  └─ TrackDataMediaHook: 앨범 폴더 thumb.png 등 (없으면 원본 자켓 + 경고)

확인창 열기 → OpenConfirmWindowPostfix (진단 로깅만)
```

자세한 규칙: [../02-systems/BMS_SELECTION.md](../02-systems/BMS_SELECTION.md), [../02-systems/MEDIA_SYSTEM.md](../02-systems/MEDIA_SYSTEM.md)

## 3. 로딩 → 플레이 시작 (`PlayLoading` → `Play`)

```text
activeSceneChanged("PlayLoading"), activeSceneChanged("Play")
  └─ Main.UpdatePlaySceneState: 이름에 "play"가 들어 있어 둘 다 플레이 씬으로 판정
       ├─ SaveCustomKeyConfig.Reload(...)   // 바뀐 항목만 로그: [SaveCustomKey] 설정 재로드 #n ...
       └─ KeyViewer/NoteSway/NoteSpeedChaos 상태 초기화

ManagerPlay.Set(track, lv, ps)              // PlayLoading이 Play 씬 로드 콜백에서 호출
  ├─ GetSixtarPatternDirectory → (커스텀이면 TrackDataMediaHook이 도너 경로 반환) → 도너 패턴으로 SXGTData 생성
  ├─ SetBGM(track) → GetAudioClip → (도너 오디오)
  └─ SetJudgeRange(난이도별 판정 범위)

ManagerPlay 초기화 → FetchBMSToModules(bms)
  └─ ManagerPlayHook.FetchBMSToModulesPrefix (커스텀 트랙만)
       ├─ BmsParser.ParseBmsFileWithStatistics(BmsPath)
       │    └─ 실패/노트 0개면 경고 후 도너 패턴으로 플레이
       ├─ CustomChartInjector.InjectBmsNotesToLaneData(bms)
       │    레인 비우기 → 노트 추가 → 정렬 → bpm/totalNotes/totalNoteWithTicks 갱신
       │    로그: [CustomChartInjector] N개 주입, totalNotes=..., totalNoteWithTicks=..., BPM=...
       ├─ BGMPlayerHook.ReplacePlaySceneBGM → music.* 비동기 로드 시작
       └─ (게임 BGA 설정 ON) BGAPlayerHook.ReplacePlaySceneBGA → 첫 *.mp4로 VideoPlayer.url 교체

NoteGenerator.Start → NoteSpeedChaosHook (켜져 있으면 선행 생성 시간 연장)
```

## 4. 플레이 중 (매 프레임)

```text
ManagerPlay.Update
  ├─ CheckBGMStart → CheckBGMStartPrefix: 커스텀 BGM 로드 중이면 시작 보류
  └─ CheckGameFinished → AutoPlayHook Prefix: CurrentTimeSeconds 기록

NoteGenerator.Update → Generate → NoteSpriteHook.GeneratePostfix (스킨 + SetNativeSize)
RG_NoteObject.Update → CalculatePosition → NoteSwayHook / NoteSpeedChaosHook Postfix
RG_PS_Judgement.Update (JudgeScoreMaxHook으로 만점 상수 교체됨)
  └─ AutoPlayHook Postfix: 오토플레이면 레인마다 AutoPlayJudge
판정 발생 → AllPerfectJudgeHook (올퍼펙트면 BLUESTAR로) → OnGetJudge → 판정바 RegisterHit

Main.OnUpdate: BGABGMSyncHook.CheckAndSync, KeyViewer.Poll, JudgmentBar.RefreshJudgeRange
Main.OnGUI:    JudgmentBar.DrawJudgmentBar, KeyViewer.Draw
```

곡 종료는 게임 원본이 BGM 길이(`bgm.clip.length`) 기준으로 판단합니다. 리트라이는 `Play` 씬을 다시 로드하므로
3번 흐름(설정 재로드 포함)이 다시 일어납니다.

## 5. 결과 화면 (`Result`)

```text
ManagerResult.Start
  ├─ ComparePlayResultHighScore → ResultSaveBlockHook: 차단 조건이면 건너뜀
  ├─ PostRequestPlayResult      → ResultSaveBlockHook: 차단 조건이면 건너뜀
  └─ ManagerResultHook Postfix  → 진단 로깅(오퍼레이터 계층, 전체 GameObject 스캔)
차단 조건: config BlockSave(또는 MelonPreferences BlockSaveBestRanking) || 오토플레이 || 올퍼펙트
```

## 스코어 보정 요약

- 게임 판정식은 `bms.totalNotes`(점수), `bms.totalNoteWithTicks`(클리어 효과음 시점)를 씁니다.
  `CustomChartInjector`가 커스텀 노트 기준으로 두 값을 다시 계산합니다.
- 만점 상수 `1000000f`는 `JudgeScoreMaxHook`이 `config.txt`의 `MaxScore`로 바꿉니다(기본값이면 원본과 같음).
- `ManagerPlay.targetBestScore`, `SXGTData.maxScore` 필드 보정은 쓰지 않습니다(효과가 없거나 타입 오류).
- 자세한 내용: [../02-systems/SCORE_SYSTEM.md](../02-systems/SCORE_SYSTEM.md)

## 제거된 과거 흐름

`TextHook`("커스텀 차트" 텍스트 감지), `SXGTDataHook`(생성자 후킹 + 지연 주입), `CustomPlayStartupFlow`,
`BmsFileResolver`(트랙ID 기반 BMS 탐색), `SceneDetector`, `Main.BmsBootstrap`(부팅 시 전체 BMS 스캔),
`BgaVideoPlayerFinder`/`BgmAudioSourceFinder`(씬 탐색), Steam 업데이트 차단은 2026-07 리팩터링으로 모두 없어졌습니다.

## 문서 위치

- 현재 상태/알려진 문제: [../CURRENT_STATUS.md](../CURRENT_STATUS.md)
- 코드 구조: [../03-development/CODE_STRUCTURE.md](../03-development/CODE_STRUCTURE.md)
- 설치/배치/설정: [../01-user-guide/INSTALL_AND_LAYOUT.md](../01-user-guide/INSTALL_AND_LAYOUT.md)
- 훅 목록: [../02-systems/HOOK_SYSTEM.md](../02-systems/HOOK_SYSTEM.md)
- 게임 원본 로직: [../02-systems/GAME_LOGIC.md](../02-systems/GAME_LOGIC.md)
