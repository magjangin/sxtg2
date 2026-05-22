# 현재 상태

기준일: 2026-05-15

## 현재 결론

프로젝트는 폐기 상태가 아니라 정리 가능한 상태입니다. 최근 정리로 미사용 파일과 진단용 후킹을 제거했고, 빌드/로직 테스트/실게임 동작 확인까지 완료했습니다.

## 검증된 상태

- `dotnet build sxtg2.sln --configuration Debug --no-restore` 성공
- `sxtg2.LogicTests` 3개 통과
- 실제 게임에서 커스텀 차트 흐름 정상 동작 확인

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
