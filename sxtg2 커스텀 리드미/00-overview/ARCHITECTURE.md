# 현재 아키텍처

기준일: 2026-10-05 (v1.1.0)

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

- 초기화: MelonPreferences 등록 → `config.txt` 로드 → `hwa` 폴더 생성 → 씬 이벤트 구독 → `CustomNotes` 스프라이트 로드(노트 스킨 초기화는 별도 `try`라 실패해도 씬 감지/오버레이는 계속 동작)
- `activeSceneChanged`: 씬 판단 — `Main.IsPlayScene`(이름에 play/rhythm/game 포함, 로딩 씬도 포함 → 설정 재로드용)과 `Main.IsGameplayScene`(정확히 `Play` → 오버레이용). 플레이 계열 씬이면 설정 재로드, 오버레이/연출 상태 초기화
- `OnUpdate`: BGA↔BGM 동기화, (`Play` 씬) 키 입력 폴링과 판정 범위 갱신
- `OnGUI`: (`Play` 씬) 판정바, 키뷰어 렌더링 (`Repaint` 이벤트에서만 그림)

### Features

- `MusicSelectFeature.cs`
  - `TrackDataAnalyzer`: `hwa` 스캔 → `CustomTrackData` 생성 → 곡 목록에 추가, 커스텀 트랙 등록부(`FindCustomTrack`)
  - `ManagerMusicSelectHook`: 곡 선택 씬 `Awake`/`PlayPreview` 훅 (핵심 기능만)
- `DiagnosticHooks.cs`: 진단용 로깅 훅 클래스 3개(`MusicSelectDiagnosticsHook`, `OperatorCharacterHook`, `ManagerResultHook`). **`LogLevel=2`일 때만 동작**
- `JudgmentBarFeature.cs`: 판정 데이터 수집 훅(같은 프레임 중복 호출 제거) + 판정바 렌더링
- `KeyViewerFeature.cs`: 키뷰어
### Hooks

- `GameplayHooks.cs`: 플레이 씬 훅 — 차트/미디어 교체(`ManagerPlayHook`), 도너 리소스와 자켓
  (`TrackDataMediaHook`), 노트 스킨/연출(`NoteSpriteHook`, `NoteSwayHook`, `NoteSpeedChaosHook`), 치트
  (`AutoPlayHook`, `AllPerfectJudgeHook`), 점수 상한(`JudgeScoreMaxHook`). 맨 위의 `SafeAccess`는 private 필드 접근이 실패해도
  모드가 멈추지 않게 하는 헬퍼(실패하면 null + 경고)
- `ResultGuardHooks.cs`: 기록·업적 차단 — `ResultSaveBlockHook`(저장/랭킹 전송), `ResultTaintHook`(플레이 횟수·업적),
  `ServerGuardHook`(커스텀 곡의 서버 요청)
- `AudioHooks.cs`: BGM/BGA 파일 탐색·로드·동기화 훅과 `MediaUrl`(file:// URL 변환)
### Loaders / Processors

- `BmsParser`: BMS → `ParsedNote` (캐시 포함)
- `TrackInfoParser`: 곡 정보 txt → 제목/아티스트/난이도
- `CustomNoteSpriteLoader`: 노트 스킨 이미지 로드(`NoteSpriteHook`이 적용)
- `CustomChartInjector`: `ParsedNote` → `ShortNote`/`HoldNote`, `SXGTData` 갱신

### Models / Helpers

- `CustomTrackData`: `TrackData` 파생. `AlbumFolder`, `BmsPath`, `ResourceDonor`(도너 트랙), `CustomJacket`
- `ModLog`: MelonPreferences `LogLevel`(0/1/2)에 따라 로그 출력, 예외 포맷
- `SaveCustomKeyConfig`: `config.txt` 생성·파싱(표 기반)·재로드·누락 항목 자동 추가(누락 항목 추가는 게임 시작 때만). 모르는 키/잘못된 값은 경고
- `ConfigParsing`: 설정 값 파서(`#`/`//` 주석 제거, 불리언·실수·색 해석). Unity에 의존하지 않아 `sxtg2.LogicTests`가 그대로 링크해 테스트함
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
  -> ServerGuardHook (랭킹 화면의 커스텀 곡 조회 보호)

Play
  -> ManagerPlay.Set: 도너 패턴/오디오 로드 (TrackDataMediaHook)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
       -> BmsParser -> CustomChartInjector (노트 교체, 노트 수 갱신)
       -> BGMPlayerHook / BGAPlayerHook (미디어 교체)
  -> InitializePlayScene Postfix: AutoPlayHook이 게임의 autoPlay 플래그 설정
  -> 매 프레임: 게임 판정, 노트 연출 훅, BGABGMSyncHook, 오버레이

Result
  -> ResultTaintHook: ManagerResult.Start 전후로 플레이 횟수 등을 되돌리고 업적 요청 차단
  -> ResultSaveBlockHook: SavePlayData / PostUserScore 차단 (오토/올퍼펙트/점수 상한 변경/커스텀 곡은 항상, 그 밖엔 BlockSave 설정)
```

자세한 순서는 [DOCUMENTATION.md](DOCUMENTATION.md), 훅 목록은 [../02-systems/HOOK_SYSTEM.md](../02-systems/HOOK_SYSTEM.md).

## 남은 리스크

- 게임 업데이트로 private 필드/메서드 이름(`bgaPlayer`, `shortNote`, `notePreGenerateTime` 등)이나 판정 메서드 안의
  `1000000f` 리터럴이 바뀌면 해당 기능이 꺼지거나 경고만 남깁니다. 필드 접근은 `SafeAccess`를 거쳐 실패해도 모드 전체가 멈추지는
  않고(해당 기능만 꺼지고 경고), 패치 대상을 못 찾으면 `[ResultSaveBlock]` 같은 훅별 경고가 남습니다.
- **게임 버전 가드는 아직 없습니다.** 시작 시 게임 버전과 적용된 패치 수를 한 줄로 요약하는 로그를 남기고, 기대와 다른 버전이면
  경고하는 것을 제안합니다(`CURRENT_STATUS.md`의 남은 문제 표). 디컴파일 소스(`sxtg2/`)는 2025-11-15 게임 DLL 기준입니다.
- 마디 길이(`#xxx02:`)와 BPM 변경 채널은 지원하지 않습니다(`CURRENT_STATUS.md`의 남은 문제 12번).
- 자켓 PNG는 원본 해상도 그대로 읽어 메모리를 씁니다(보류, 남은 문제 30번).
- 알려진 버그 목록은 [../CURRENT_STATUS.md](../CURRENT_STATUS.md)의 "남은 문제" 절에 모았습니다.
