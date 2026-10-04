# 현재 아키텍처

기준일: 2026-09-28 (v1.1.0)

## 한 줄 요약

`sxtg2`는 게임 어셈블리를 직접 참조한 Harmony 훅으로 게임 흐름에 끼어들어, 외부 `hwa` 폴더의 BMS/미디어/메타데이터를
게임 데이터로 바꿔 넣고, 플레이 씬 위에 IMGUI 오버레이를 그리는 모드입니다.

## 레이어

```text
Game (Assembly-CSharp, Unity)
  ↑ Harmony 훅 ([HarmonyPatch], MelonLoader가 자동 PatchAll)
Hooks / Features          게임 이벤트를 받아 기능 실행
  ↓
Loaders / Processors      외부 파일 읽기, 게임 객체로 변환
  ↓
Models / Helpers          CustomTrackData, 로그, config.txt, 썸네일
```

`Main`은 훅이 아닌 경로(씬 전환 이벤트, 매 프레임 `OnUpdate`/`OnGUI`)를 담당합니다.

## 주요 책임

### Main (`Main/Main.cs`)

- 초기화: MelonPreferences 등록 → `config.txt` 로드 → `hwa` 폴더 생성 → `CustomNotes` 스프라이트 로드 → 씬 이벤트 구독
- `activeSceneChanged`: 플레이 씬 여부 판단(`AutoPlayHook.IsPlayScene`), 플레이 씬이면 설정 재로드, 오버레이/연출 상태 초기화
- `OnUpdate`: BGA↔BGM 동기화, (플레이 씬) 키 입력 폴링과 판정 범위 갱신
- `OnGUI`: (플레이 씬) 판정바, 키뷰어 렌더링

### Features

- `MusicSelectFeature.cs`
  - `TrackDataAnalyzer`: `hwa` 스캔 → `CustomTrackData` 생성 → 곡 목록에 추가
  - `ManagerMusicSelectHook`: 곡 선택 씬 `Awake`/`PlayPreview` 훅 (+ 진단용 로깅 훅)
  - `OperatorCharacterHook`, `ManagerResultHook`: 진단용 로깅만
- `JudgmentBarFeature.cs`: 판정 데이터 수집 훅 + 판정바 렌더링
- `KeyViewerFeature.cs`: 키뷰어

### Hooks

- `GameplayHooks.cs`: 플레이 씬 훅 9개 클래스 — 차트/미디어 교체(`ManagerPlayHook`), 도너 리소스와 자켓
  (`TrackDataMediaHook`), 노트 스킨/연출(`NoteSpriteHook`, `NoteSwayHook`, `NoteSpeedChaosHook`), 치트/보호
  (`AutoPlayHook`, `AllPerfectJudgeHook`, `ResultSaveBlockHook`), 점수 상한(`JudgeScoreMaxHook`)
- `AudioHooks.cs`: BGM/BGA 파일 탐색·로드·동기화 헬퍼 (Harmony 패치 아님)

### Loaders / Processors

- `BmsParser`: BMS → `ParsedNote` (캐시 포함)
- `TrackInfoParser`: 곡 정보 txt → 제목/아티스트/난이도
- `CustomNoteSpriteLoader`, `NoteRendererRecovery`: 노트 스킨
- `CustomChartInjector`: `ParsedNote` → `ShortNote`/`HoldNote`, `SXGTData` 갱신

### Models / Helpers

- `CustomTrackData`: `TrackData` 파생. `AlbumFolder`, `BmsPath`, `ResourceDonor`(도너 트랙), `CustomJacket`
- `ModLog`: MelonPreferences `LogLevel`(0/1/2)에 따라 로그 출력, 예외 포맷
- `SaveCustomKeyConfig`: `config.txt` 생성·파싱·재로드·누락 항목 자동 추가(누락 항목 추가는 게임 시작 때만)
- `ThumbnailLoader`: 앨범 폴더 자켓 PNG → `Sprite`

## 핵심 설계

### 도너 트랙 방식

커스텀 트랙은 게임 리소스(암호화된 패턴 파일, 오디오 클립, 로딩 영상)가 없습니다. 그래서 곡 목록의 첫 번째 원본 곡을
**도너**로 정하고, 게임이 리소스를 요청하면 도너 것을 돌려줍니다(`TrackDataMediaHook`). 게임은 도너 곡을 로드하는
정상 흐름을 그대로 타고, 모드는 `FetchBMSToModules` 직전에 **같은 `SXGTData` 인스턴스의 노트만** 커스텀 차트로
바꿉니다. 이 방식 덕분에 게임의 판정/점수/결과 흐름을 거의 수정하지 않습니다.

### 설정은 플레이 단위로 고정

`config.txt`는 플레이 씬에 들어가는 순간에만 다시 읽습니다. 한 판 도중에는 값이 바뀌지 않으므로 노트가 튀거나
판정이 흔들리는 부작용이 없고, 다음 플레이부터 수정이 반영됩니다.

### 판정에 영향 없는 연출

NoteSway/NoteSpeedChaos는 `CalculatePosition` 이후 화면 위치만 바꿉니다. 게임 판정은 시간(`Note.timing`)만 보므로
정확도에 영향이 없습니다.

## 핵심 흐름

```text
MusicSelect
  -> ManagerMusicSelectHook.AwakePostfix -> TrackDataAnalyzer.InjectCustomTracks
  -> ManagerMusicSelectHook.PlayPreviewPrefix (커스텀 트랙 미리듣기)
  -> TrackDataMediaHook (자켓)

Play
  -> ManagerPlay.Set: 도너 패턴/오디오 로드 (TrackDataMediaHook)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
       -> BmsParser -> CustomChartInjector (노트 교체, 노트 수 갱신)
       -> BGMPlayerHook / BGAPlayerHook (미디어 교체)
  -> 매 프레임: 게임 판정 + AutoPlayHook, 노트 연출 훅, BGABGMSyncHook, 오버레이

Result
  -> ResultSaveBlockHook (조건부 저장 차단)
```

자세한 순서는 [DOCUMENTATION.md](DOCUMENTATION.md), 훅 목록은 [../02-systems/HOOK_SYSTEM.md](../02-systems/HOOK_SYSTEM.md).

## 남은 리스크

- 게임 업데이트로 private 필드/메서드 이름(`bgaPlayer`, `shortNote`, `notePreGenerateTime`, `AutoPlayJudge` 등)이나
  판정 메서드 안의 `1000000f` 리터럴이 바뀌면 해당 기능이 조용히 꺼지거나 경고만 남깁니다.
- 진단용 로깅 훅 4개가 켜져 있어 곡 선택 확인창/결과 화면마다 로그가 많이 쌓이고, 결과 화면에서
  `FindObjectsOfType<GameObject>()` 전체 스캔을 합니다.
- 게임 버전 가드가 없습니다. 업데이트로 훅 대상이 바뀌어도 어떤 훅이 적용됐는지 요약이 로그에 남지 않고, 정적 `FieldRefAccess`
  초기화 실패가 씬 감지 구독을 건너뛰게 할 수도 있습니다(알려진 문제 #15, #16). 시작 시 게임 버전과 적용된 패치 수를 로그로 남기는
  것이 좋습니다.
- `BlockSave`는 이름과 달리 기록/랭킹 전송만 막고 Steam 업적, 플레이 횟수, 서버 플레이 카운트는 막지 않습니다(#13, #14).
- 알려진 버그 목록은 [../CURRENT_STATUS.md](../CURRENT_STATUS.md)의 "알려진 문제" 절에 모았습니다.
