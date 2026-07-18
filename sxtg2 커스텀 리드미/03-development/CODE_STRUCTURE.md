# 코드 구조

기준일: 2026-05-15

이 문서는 현재 `sxtg2-mod/` 코드 기준입니다. `bin`, `obj`, `.vs` 산출물은 제외합니다.

## 파일 수

- C# 파일: 87개
- 주요 소스 루트: `sxtg2-mod/`

## 현재 디렉터리 구조

```text
sxtg2-mod/
├── Main/
│   ├── Main.cs
│   ├── Main.Initialization.cs
│   ├── Main.UpdateLoop.cs
│   └── Main.BmsBootstrap.cs
├── Features/
│   ├── CustomPlayContext.cs
│   ├── CustomPlayStartupFlow.cs
│   ├── MusicSelectAnalyzer.cs
│   ├── TrackDataAnalyzer*.cs
│   └── SceneDetector/
├── Hooks/
│   ├── Audio/
│   ├── Manager/
│   ├── SXGT/
│   └── Text/
├── Helpers/
│   ├── Finders/
│   ├── Game/
│   ├── Pause/
│   ├── Reflection/
│   ├── Screen/
│   └── Track/
├── Loaders/
├── Processors/
└── Properties/
```

## 제거된 과거 구조

다음 파일/영역은 현재 코드에 없습니다.

- `Hooks/Note/NumberInterpolatorHook.cs`
- `Hooks/SXGT/SXGTReaderHook*.cs`
- `Hooks/Audio/InitialMediaFileScanner.cs`
- `Features/CustomAlbumManager.cs`
- `Features/MusicSelectAnalyzer.Diagnostics.cs`
- `Hooks/SXGT/SXGTDataHook.Diagnostics.cs`
- `Features/ThumbnailProcessor.cs`
- `Features/ThumbnailResourceProbe.cs`
- `Models/*`
- `Manipulators/*`
- `Processors/NoteDataExtractor.cs`
- `Processors/NoteGroupProcessor.cs`
- `Processors/NoteMatcher.cs`
- `Helpers/DictionaryHelper.cs`
- `Helpers/InitReport.cs`
- `Helpers/ResourceManagerHelper.cs`
- `Helpers/Finders/TrackListRetriever.cs`
- `Helpers/Finders/TrackLoaderFinder.cs`
- `Helpers/Game/GameObjectPathHelper.cs`

## Main

`Main`은 MelonLoader 진입점입니다.

```text
Main/
├── Main.cs                 # 필드와 MelonInfo 대상 클래스
├── Main.Initialization.cs  # Hook/Feature 초기화
├── Main.UpdateLoop.cs      # SceneDetector, BGA/BGM sync
└── Main.BmsBootstrap.cs    # 시작 시 BMS 스캔/통계/기본 차트 설정
```

현재 초기화 순서:

```text
ModLog.RegisterPreferences
TextHook.Initialize
SteamManifestLock.Unlock
BGAPlayerHook.Initialize
BGMPlayerHook.Initialize
ManagerMusicSelectHook.Initialize
ManagerPlayHook.Initialize
SXGTDataHook.Initialize
ScanAndParseBmsFiles
SceneDetector.Initialize
```

## Features

```text
Features/
├── CustomPlayContext.cs
├── CustomPlayStartupFlow.cs
├── MusicSelectAnalyzer.cs
├── TrackDataAnalyzer.cs
├── TrackDataAnalyzer.AlbumFolders.cs
├── TrackDataAnalyzer.Difficulty.cs
├── TrackDataAnalyzer.Inject.cs
├── TrackDataAnalyzer.TrackInfo.cs
├── TrackDataAnalyzer.TrackList.cs
└── SceneDetector/
```

핵심:

- `CustomPlayStartupFlow`: 플레이 시작 시 커스텀 트랙 확정, BMS 로드, 미디어 교체, 노트 주입을 조율합니다.
- `MusicSelectAnalyzer`: MusicSelect 씬에서 TrackData 리스트를 찾아 커스텀 트랙을 주입합니다.
- `TrackDataAnalyzer`: 원본 TrackData 복제, info.txt 적용, 난이도 설정, trackDatas 리스트 추가를 담당합니다.
- `SceneDetector`: 씬 변경 감지와 지연 실행을 담당합니다.

## Hooks

### Audio

```text
Hooks/Audio/
├── BGABGMSyncHook.cs
├── BGAPlayerHook.cs
├── BgaFileResolver.cs
├── BgaVideoPlayerFinder.cs
├── BGMPlayerHook.cs
├── BGMPlayerHook.Playback.cs
├── BgmAudioSourceFinder.cs
└── BgmFileResolver.cs
```

초기 전체 미디어 스캔은 없습니다. 실제 교체 시점에 앨범 폴더에서 파일을 선택합니다.

### Manager

```text
Hooks/Manager/
├── ManagerMusicSelectHook*.cs
└── ManagerPlayHook/
```

- `ManagerMusicSelectHook`: 선택 변경, 썸네일, preview 음악을 처리합니다.
- `ManagerPlayHook`: 플레이 시작 감지와 원본 `PauseGame` 이후 커스텀 자켓 적용을 담당합니다.

### SXGT

```text
Hooks/SXGT/
├── SXGTDataHook.cs
├── SXGTDataHook.NoteOps.cs
├── SXGTDataHook.NoteOps.Constructors.cs
├── SXGTDataHook.NoteTypeExtraction.cs
└── SXGTDataHook.Pending.cs
```

`SXGTReaderHook`은 제거되었습니다. 현재 주입 핵심은 `SXGTDataHook`입니다.

### Text

```text
Hooks/Text/
├── TextHook.cs
├── TextHook.Initialization.cs
├── TextHook.BmsLoader.cs
├── TextHook.BmsLoader.Search.cs
└── TextHook.TrackFinder.cs
```

`TextHook`는 플레이 로딩 텍스트에서 `"커스텀 차트"`를 감지해 BMS 로드를 트리거하는 보조 경로입니다.

## Helpers

중요 헬퍼:

- `Helpers/Track/BmsFileResolver.cs`: BMS 확장자와 파일 선택 규칙
- `Helpers/Track/CustomTrackHelper.cs`: 현재 선택된 커스텀 트랙 상태
- `Helpers/Track/ThumbnailLoader.cs`: 썸네일 파일 로드
- `Helpers/Track/TrackDataCloner.cs`: TrackData 복제
- `Helpers/ManagerMusicSelectBridge.cs`: ManagerMusicSelect 접근
- `Helpers/Reflection/*`: 타입/필드/메서드 탐색
- `Helpers/Game/GameLaneDataHelper.cs`: `SXGTData.laneData` 접근
- `Helpers/Pause/*`: 원본 일시정지 창의 `jacketImage`에 커스텀 썸네일 적용
- `Helpers/Screen/*`: PlayLoading/Result 자켓 이미지 적용

## Loaders

```text
Loaders/
├── BmsParser.cs
├── BmsParser.Processing.cs
└── TrackInfoParser.cs
```

- `BmsParser`: BMS 텍스트/파일을 `ParsedNote`로 변환합니다.
- `TrackInfoParser`: `info.txt`/`trackinfo.txt` 등에서 제목, 아티스트, 난이도를 읽습니다.

## Processors

```text
Processors/
├── CustomChartInjector.cs
├── CustomChartInjector.FieldSetter.cs
├── CustomChartInjector.NoteConstructors.cs
├── CustomChartInjector.NoteEnums.cs
└── CustomChartInjector.NoteFactory.cs
```

`CustomChartInjector`는 파싱된 BMS 노트를 실제 게임 노트 객체로 만들고 `laneData`에 추가합니다.
