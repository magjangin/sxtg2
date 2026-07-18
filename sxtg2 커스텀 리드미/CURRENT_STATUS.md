# 현재 상태

기준일: 2026-07-18

## 현재 결론

프로젝트는 폐기 상태가 아니라 정리 가능한 상태입니다. 최근 정리로 미사용 파일과 진단용 후킹을 제거했고, 빌드/로직 테스트/실게임 동작 확인까지 완료했습니다.

## 검증된 상태

- `dotnet build sxtg2-mod/sxtg2.csproj --configuration Debug` 성공 (경고 0개)
- `sxtg2.LogicTests` 4개 통과
- 실제 게임에서 커스텀 차트 흐름 정상 동작 확인

## 2026-07-18 수정: 커스텀 차트 스코어/클리어 사운드 타이밍 버그

- **증상**: 커스텀 차트 플레이 중 곡이 다 끝나기도 전에(또는 이상한 시점에) `Clear_FullCombo`/
  `Clear_Normal`(→ `KeyBlue_Tam`) 사운드가 재생됨.
- **근본 원인**: `SXGTData.totalNotes`/`totalNoteWithTicks`가 도너(복제 원본) 트랙의 노트 개수로 남아있었고,
  `CustomChartInjector`는 `laneData`(노트 리스트)만 갈아끼울 뿐 이 개수 필드는 갱신하지 않았음. 게임의
  스코어 계산(`JudgeScore`)과 곡 종료 판정(`elapsedNote >= totalNoteWithTicks`)이 전부 이 값을 기준으로
  동작하기 때문에, 실제 커스텀 차트와 도너 트랙의 노트 수가 다르면 판정이 어긋남.
- **기존에 있던(효과 없던) 시도**: `maxScore`/`MaxScore` 필드를 -1로 바꾸는 보정이 있었지만, 애초에
  스코어 계산식이 그 필드를 참조하지 않아 무의미했고, 클리어 사운드는 증상만 `KeyBlue_Tam`으로 가려왔음.
- **수정**: `CustomChartInjector`가 노트를 레인에 실제로 주입하면서 성공한 노트 수를 직접 세어(레인 9/10
  제외, 홀드 노트는 `tickLength`만큼 가산) 주입 완료 직후 `SXGTData.totalNotes`/`totalNoteWithTicks`를
  덮어쓰도록 함. 실게임 테스트로 확인 완료.
- **후속**: 근본 원인이 고쳐지면서 `Clear_FullCombo`/`Clear_Normal`을 `KeyBlue_Tam`으로 가리던 임시방편이
  불필요해짐을 실게임에서 확인. `Resources.LoadAll<AudioClip>("")`로 Resources 폴더 전체를 스캔하던
  `Hooks/Audio/AudioSourceHook.cs`를 삭제함 (부트 초반 불필요한 트랙 음원 강제 로드 비용도 함께 제거됨).
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
