# sxtg2 종합 문서

이 문서는 2026-05-15 정리 이후의 현재 코드 구조를 기준으로 합니다.

## 현재 코드의 핵심 원칙

- 실제 빌드 대상은 `sxtg2-mod/`입니다.
- BMS 선택은 `Helpers/Track/BmsFileResolver.cs`가 공통으로 담당합니다.
- BMS 파싱은 `Loaders/BmsParser*.cs`가 담당합니다.
- 게임 노트 변환/주입은 `Processors/CustomChartInjector*.cs`가 담당합니다.
- 원본 노트 제거와 `SXGTData` 접근은 `Hooks/SXGT/SXGTDataHook*.cs`가 담당합니다.
- BGA/BGM 교체는 `Hooks/Audio/*`가 담당합니다.

## 초기화 흐름

```text
Main.OnInitializeMelon
  -> ModLog.RegisterPreferences
  -> TextHook.Initialize
  -> SteamManifestLock.Unlock
  -> BGAPlayerHook.Initialize
  -> BGMPlayerHook.Initialize
  -> ManagerMusicSelectHook.Initialize
  -> ManagerPlayHook.Initialize
  -> SXGTDataHook.Initialize
  -> ScanAndParseBmsFiles
  -> SceneDetector.Initialize
```

제거된 초기화 항목:

- `SXGTReaderHook.Initialize`
- `NumberInterpolatorHook.Initialize`
- 초기 미디어 전체 스캔
- 하이스코어/클리어 사운드 교체 후킹은 제거되었습니다.

## BMS 선택과 파싱

```text
TextHook.LoadAndInjectBmsForTrack
  -> BmsFileResolver.FindForTrack
  -> BmsParser.ParseBmsFile
  -> CustomChartInjector.SetParsedBmsNotes
```

초기 부팅 시에는 `Main.BmsBootstrap`이 `BmsFileResolver.FindRootAndAlbumBmsFiles`로 `hwa` 폴더의 BMS를 스캔하고 통계를 출력합니다. 실제 플레이 직전에는 선택된 트랙/앨범 폴더 기준으로 다시 BMS를 선택합니다.

## 커스텀 플레이 시작 흐름

```text
ManagerPlayHook
  -> CustomPlayStartupFlow.Run
     -> CustomPlayContext.TryResolveCurrentCustomTrack
     -> CustomTrackHelper.SetSelectedTrack
     -> TextHook.LoadAndInjectBmsForTrack
     -> BGAPlayerHook.ResetReplacementFlag
     -> BGMPlayerHook.ResetReplacementFlag
     -> BGMPlayerHook.SetManagerPlayBGM
     -> BGAPlayerHook.ReplacePlaySceneBGA
     -> BGMPlayerHook.ReplacePlaySceneBGM
     -> SXGTDataHook.ProcessPendingNoteRemovalAndInjection
```

## 노트 제거와 주입

```text
SXGTDataHook.ProcessPendingNoteRemovalAndInjection
  -> pending SXGTData 사용 또는 fallback 탐색
  -> ExtractNoteDataTypeFromLaneData
  -> ClearAllNotes
  -> CustomChartInjector.InjectBmsNotesToLaneData
  -> CustomChartInjector가 totalNotes/totalNoteWithTicks 갱신
```

`SXGTReaderHook` 기반 진단 후킹은 제거되었습니다. 현재 핵심 주입 경로는 `SXGTDataHook`와 `ManagerPlayHook`입니다.

## 미디어 교체

- BGA 파일 선택: `BgaFileResolver`
- VideoPlayer 탐색: `BgaVideoPlayerFinder`
- BGA 적용: `BGAPlayerHook`
- BGM 파일 선택: `BgmFileResolver`
- AudioSource 탐색: `BgmAudioSourceFinder`
- BGM 로드/재생: `BGMPlayerHook` + `BGMPlayerHook.Playback`
- 싱크 보정: `BGABGMSyncHook`

초기 `hwa` 폴더 미디어 스캔은 제거되었습니다. 실제 교체 시점에 앨범 폴더에서 필요한 파일을 찾습니다.

## 스코어 보정

- 별도의 `MaxScore` 보정 훅은 제거되었습니다. 원본 판정식은 `bms.totalNotes`와
  `bms.totalNoteWithTicks`를 사용합니다.
- `CustomChartInjector`가 커스텀 노트 주입 후 두 값을 다시 계산합니다.
- `ManagerPlay.targetBestScore` 보정도 사용하지 않습니다.

## 최신 문서 위치

- 현재 상태: [../CURRENT_STATUS.md](../CURRENT_STATUS.md)
- 코드 구조: [../03-development/CODE_STRUCTURE.md](../03-development/CODE_STRUCTURE.md)
- 설치/배치: [../01-user-guide/INSTALL_AND_LAYOUT.md](../01-user-guide/INSTALL_AND_LAYOUT.md)
- BMS 선택: [../02-systems/BMS_SELECTION.md](../02-systems/BMS_SELECTION.md)
- 미디어 시스템: [../02-systems/MEDIA_SYSTEM.md](../02-systems/MEDIA_SYSTEM.md)
- 스코어 시스템: [../02-systems/SCORE_SYSTEM.md](../02-systems/SCORE_SYSTEM.md)
