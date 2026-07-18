# 현재 상태

기준일: 2026-07-18

## 현재 결론

프로젝트는 폐기 상태가 아니라 정리 가능한 상태입니다. 최근 정리로 미사용 파일과 진단용 후킹을 제거했고, 빌드/로직 테스트/실게임 동작 확인까지 완료했습니다.

## 검증된 상태

- `dotnet build sxtg2-mod/sxtg2.csproj --configuration Debug` 성공 (경고 0개)
- `sxtg2.LogicTests` 4개 통과
- 실제 게임에서 커스텀 차트 흐름 정상 동작 확인

## 2026-07-18 추가: InventoryPopup 모드의 노트 스킨 기능 이식

- **배경**: 별도 저장소 `H:\source\repos\InventoryPopup`에 있던 커스텀 노트 스프라이트(스킨) 교체
  기능을 sxtg2-mod로 통합해달라는 요청.
- **가져온 것**: `RhythmGame.NoteGenerator.Generate` 후킹 → 생성된 `RG_NoteObject`의
  `shortNote`/`tailNote`/`holdTexture` 필드에 `CustomNotes` 폴더의 PNG 스프라이트를 적용 → UI 렌더러
  강제 갱신. 새 파일: `Loaders/CustomNoteSpriteLoader.cs`, `Hooks/Note/NoteSpriteHook.cs`,
  `Helpers/UI/NoteRendererRecovery.cs`.
- **가져오지 않은 것**: `InventoryPopup`에 있던 진단용 코드(모든 public 메서드를 무차별 후킹해서 로깅,
  `NoteAnalyzer`/`SceneAnalyzer`/`UIRendererAnalyzer`, `Setter_NoteSpeed` 후킹 등)는 실제 기능이 아니라
  분석 도구였으므로 이식하지 않음. 하드코딩된 게임 경로(`H:\Sixtar Gate STARTRAIL custom mode`)도
  sxtg2 관례대로 `Application.dataPath` 기반 동적 경로로 교체.
- **검증**: `dotnet build` 성공(경고 0개). 실게임 동작 확인은 아직 안 됨 — `CustomNotes` 폴더에 PNG를
  넣고 플레이해서 확인 필요.
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`("노트 스킨" 절), `02-systems/HOOK_SYSTEM.md`(NoteSpriteHook),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(5절, CustomNotes 폴더 규칙)

## 2026-07-18 추가: SaveCustomKey 폴더 생성 로직

- `{게임 설치 폴더}\SaveCustomKey\` 폴더를 모드 초기화 시 자동 생성하는 로직만 추가함
  (`Helpers/SaveCustomKeyFolderHelper.cs`). 아직 이 폴더를 실제로 읽고 쓰는 기능은 없음 — 향후
  커스텀 키 프리셋 파일 저장/불러오기 기능을 위한 준비 단계.
- 자세한 내용: `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

## 2026-07-18 수정: 게임 종료 시 "씬 감지 모드 정리 중 오류" 에러

- **증상**: 게임을 종료할 때마다 `MelonLoader\Latest.log`에 빨간 ERROR 줄로
  `씬 감지 모드 정리 중 오류 발생: Operation is not valid due to the current state of the object.`가 찍힘.
- **근본 원인**: 스택 트레이스를 추가해 확인한 결과, `System.MulticastDelegate.RemoveImpl` →
  `SceneManager.remove_sceneLoaded`에서 발생하는 `InvalidOperationException`. Unity가 사용하는 구형
  Mono 런타임에서 `OnApplicationQuit` 시점에 정적 이벤트 구독을 해제하려 할 때 나타나는 런타임 레벨
  특성으로, 모드 로직 버그가 아님.
- **판단**: `SceneDetector.Cleanup()`은 `Main.OnApplicationQuit()` 단 한 곳에서만 호출되고 있었고,
  프로세스가 종료되는 시점에 이벤트 구독을 해제하는 것 자체가 아무 효과가 없음(그 이후 `sceneLoaded`가
  다시 발화할 일이 없음). 즉 이 호출은 아무 이득 없이 Mono의 취약한 내부 경로만 건드리고 있었음.
- **수정**: 로그 레벨을 낮추거나 예외를 조용히 삼키는 방식 대신, 애초에 의미 없던
  `Main.OnApplicationQuit()`과 `SceneDetector.Cleanup()`을 완전히 제거함.

## 2026-07-18 수정: 커스텀 차트 스코어/클리어 사운드 타이밍 버그

- **증상**: 커스텀 차트 플레이 중 곡이 다 끝나기도 전에(또는 이상한 시점에) `Clear_FullCombo`/
  `Clear_Normal` 사운드가 재생됨.
- **근본 원인**: `SXGTData.totalNotes`/`totalNoteWithTicks`가 도너(복제 원본) 트랙의 노트 개수로 남아있었고,
  `CustomChartInjector`는 `laneData`(노트 리스트)만 갈아끼울 뿐 이 개수 필드는 갱신하지 않았음. 게임의
  스코어 계산(`JudgeScore`)과 곡 종료 판정(`elapsedNote >= totalNoteWithTicks`)이 전부 이 값을 기준으로
  동작하기 때문에, 실제 커스텀 차트와 도너 트랙의 노트 수가 다르면 판정이 어긋남.
- **기존에 있던(효과 없던) 시도**: `maxScore`/`MaxScore` 필드를 -1로 바꾸는 보정이 있었지만, 애초에
  스코어 계산식이 그 필드를 참조하지 않아 무의미했고, 클리어 사운드를 `KeyBlue_Tam`으로 바꾸는 임시 후킹도
  증상만 가리고 있었음.
- **수정**: `CustomChartInjector`가 노트를 레인에 실제로 주입하면서 성공한 노트 수를 직접 세어(레인 9/10
  제외, 홀드 노트는 `tickLength`만큼 가산) 주입 완료 직후 `SXGTData.totalNotes`/`totalNoteWithTicks`를
  덮어쓰도록 함. 실게임 테스트로 확인 완료.
- **후속**: 근본 원인이 고쳐지면서 `Clear_FullCombo`/`Clear_Normal`을 `KeyBlue_Tam`으로 가리던 임시방편이
  불필요해짐을 실게임에서 확인. `SoundObject.Play`/`PlayAndDestroy`를 후킹하던 클리어 사운드 교체 코드를
  삭제하고, 부트 초반 Resources 전체 스캔도 제거함.
- 자세한 내용: `02-systems/SCORE_SYSTEM.md`, `01-user-guide/TROUBLESHOOTING.md` (5번 항목)

## 최근 정리 내역

- 미사용/비활성 C# 파일 제거
  - `NumberInterpolatorHook`
  - `CustomAlbumManager`
  - `DictionaryHelper`, `InitReport`, `ResourceManagerHelper`
  - `NoteGroupProcessor`, `NoteMatcher`, `NoteDataExtractor`
  - `Models/*`, `Manipulators/*`
- 진단성 후킹 제거
  - `SXGTReaderHook.*`
  - `MusicSelectAnalyzer.Diagnostics`
  - `SXGTDataHook.Diagnostics`
- 초기 진단 스캔 제거
  - `InitialMediaFileScanner`
- 중복 책임 정리
  - `BmsFileResolver` 추가
  - BMS 파일 확장자와 파일 선택 규칙 통합
- 불필요한 경고 제거
  - `ManagerPlay.targetBestScore` 보정 제거
- 2026-07-18 불필요 코드 정리
  - `HighscoreMeterHook` 및 `SoundObjectClipAccessor` 삭제
  - `SXGTDataHook.Score`의 무효한 `maxScore` 보정 삭제
  - ESC 강제 일시정지 호출/탐색 코드 삭제
  - 원본 `PauseGame` 이후 커스텀 자켓 적용만 유지

## 현재 핵심 흐름

```text
MusicSelect
  -> TrackDataAnalyzer가 커스텀 TrackData 주입
  -> ManagerMusicSelectHook이 선택 트랙/썸네일/프리뷰 처리

PlayLoading / PlayStart
  -> TextHook 또는 CustomPlayStartupFlow가 BMS 로드
  -> BmsFileResolver가 BMS 파일 선택
  -> BmsParser가 파싱
  -> CustomChartInjector가 노트 생성 준비

SXGTData / ManagerPlay
  -> SXGTDataHook가 원본 노트 제거
  -> CustomChartInjector가 laneData에 커스텀 노트 주입
  -> BGAPlayerHook/BGMPlayerHook이 미디어 교체
  -> BGABGMSyncHook이 BGA/BGM 동기화
```

## 남은 주의점

- `TextHook` 기반 `"커스텀 차트"` 텍스트 감지는 아직 넓은 후킹입니다.
- `MusicSelectAnalyzer`는 여전히 씬 전체 탐색을 일부 수행합니다.
- `FindObjectsOfType` 기반 탐색은 모딩 특성상 남아 있지만, 자주 호출되는 경로인지 계속 확인해야 합니다.
- `02-systems/`의 일부 상세 문서는 과거 코드 설명이 남아 있을 수 있습니다.
