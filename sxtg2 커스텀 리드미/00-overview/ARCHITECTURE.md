# 현재 아키텍처

기준일: 2026-05-15

## 한 줄 요약

`sxtg2`는 게임 내부 객체를 Harmony와 Reflection으로 연결하고, 외부 `hwa` 폴더의 BMS/미디어/메타데이터를 게임 플레이 데이터로 변환해 주입하는 모드입니다.

## 레이어

```text
Game / Unity
  -> Hooks
  -> Features
  -> Loaders / Processors
  -> Helpers
```

## 주요 책임

### Hooks

게임 내부 메서드나 객체를 연결합니다.

- `Hooks/Text`: 플레이 로딩 텍스트 감지와 BMS 로드 보조 경로
- `Hooks/Manager`: MusicSelect/Play 매니저 후킹
- `Hooks/SXGT`: `SXGTData` 생성자 후킹, 원본 노트 제거, 커스텀 노트 수 갱신
- `Hooks/Audio`: BGA/BGM/preview 교체 및 동기화

### Features

게임 흐름 단위의 기능을 조율합니다.

- `CustomPlayStartupFlow`: 커스텀 플레이 시작 흐름의 중심
- `CustomPlayContext`: 현재 선택된 커스텀 트랙 판별
- `MusicSelectAnalyzer`: MusicSelect 씬에서 TrackData 주입
- `TrackDataAnalyzer`: TrackData 복제/수정/추가
- `SceneDetector`: 씬 변경 감지와 지연 실행

### Loaders

외부 파일을 읽고 내부 모델로 바꿉니다.

- `BmsParser`: BMS 파일을 `ParsedNote`로 변환
- `TrackInfoParser`: `info.txt` 등에서 제목/아티스트/난이도 파싱

### Processors

파싱된 데이터를 게임 객체로 변환합니다.

- `CustomChartInjector`: `ParsedNote`를 `ShortNote`/`HoldNote` 계열 게임 노트로 변환하고 `laneData`에 추가

### Helpers

반복되는 저수준 접근을 모읍니다.

- `BmsFileResolver`: BMS 파일 선택 규칙
- `ThumbnailLoader`: 썸네일 파일 로드
- `CustomTrackHelper`: 현재 커스텀 트랙 상태
- `ReflectionHelper`/`TypeFinderHelper`: 타입/필드/프로퍼티 탐색
- `GameLaneDataHelper`: `SXGTData.laneData` 접근
- `ManagerMusicSelectBridge`: MusicSelect 매니저 접근

## 핵심 흐름

```text
MusicSelect
  -> TrackDataAnalyzer가 커스텀 트랙을 목록에 추가
  -> ManagerMusicSelectHook이 선택 트랙 상태/썸네일/preview 처리

PlayStart
  -> ManagerPlayHook
  -> CustomPlayStartupFlow
  -> BmsFileResolver + BmsParser
  -> CustomChartInjector.SetParsedBmsNotes
  -> BGA/BGM 교체
  -> SXGTDataHook 원본 노트 제거/커스텀 노트 주입
```

## 제거된 과거 구조

현재 아키텍처에는 다음이 없습니다.

- `SXGTReaderHook`
- `NumberInterpolatorHook`
- `InitialMediaFileScanner`
- `NoteDataExtractor`, `NoteGroupProcessor`, `NoteMatcher`
- `Models`, `Manipulators`
- 대량 진단용 `Diagnostics` 파일

## 남은 리스크

- `TextHook`는 넓은 텍스트 setter 후킹입니다.
- 일부 UI/Audio 탐색은 `FindObjectsOfType` 기반입니다.
- 게임 업데이트로 내부 타입/필드명이 바뀌면 Reflection 경로가 깨질 수 있습니다.
