# 데이터 흐름

기준일: 2026-05-15

## BMS 파일에서 게임 노트까지

```text
*.bms / *.bme / *.bml
  -> BmsFileResolver
  -> BmsParser
  -> List<ParsedNote>
  -> CustomChartInjector.SetParsedBmsNotes
  -> SXGTDataHook.ProcessPendingNoteRemovalAndInjection
  -> CustomChartInjector.InjectBmsNotesToLaneData
  -> SXGTData.laneData
```

## TrackData 주입

```text
MusicSelect scene
  -> SceneDetector
  -> MusicSelectAnalyzer
  -> ManagerMusicSelectBridge
  -> TrackDataAnalyzer
     -> TrackDataCloner
     -> TrackInfoParser
     -> BmsFileResolver
     -> trackDatas.Add(clonedTrack)
```

## 커스텀 플레이 시작

```text
ManagerPlay hook
  -> CustomPlayStartupFlow.Run
  -> CustomPlayContext.TryResolveCurrentCustomTrack
  -> CustomTrackHelper.SetSelectedTrack
  -> TextHook.LoadAndInjectBmsForTrack
  -> BGAPlayerHook.ReplacePlaySceneBGA
  -> BGMPlayerHook.ReplacePlaySceneBGM
  -> SXGTDataHook.ProcessPendingNoteRemovalAndInjection
```

## 썸네일 흐름

```text
Track selection / PlayLoading / Result / Pause
  -> CustomTrackHelper.GetSelectedTrack
  -> ThumbnailLoader.LoadThumbnail
  -> UnityEngine.UI.Image.sprite 교체
```

## BGM/BGA 흐름

```text
Album folder
  -> BgaFileResolver / BgmFileResolver
  -> BgaVideoPlayerFinder / BgmAudioSourceFinder
  -> VideoPlayer.url / AudioSource.clip 교체
  -> BGABGMSyncHook.CheckAndSync
```

## 스코어 흐름

```text
CustomPlayStartupFlow
  -> SXGTDataHook.ProcessPendingNoteRemovalAndInjection
     -> CustomChartInjector가 totalNotes/totalNoteWithTicks 갱신

SXGTDataHook
  -> totalNotes/totalNoteWithTicks 갱신
```

`ManagerPlay.targetBestScore` 보정은 제거되었습니다.
