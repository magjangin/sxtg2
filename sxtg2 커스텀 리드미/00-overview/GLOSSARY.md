# 용어집(Glossary) - sxtg2 문서 공통

문서가 늘어나면 용어가 헷갈리기 쉬워서, 공통 용어를 한 곳에 모아둡니다. (기준일: 2026-10-05)

## 게임/데이터 구조

- **TrackData**: 곡 메타데이터(ID, `DisplayName`, `Composer`, `Level` 등)와 리소스 경로를 담는 게임 쪽 타입
- **CustomTrackData**: 모드가 만든 `TrackData` 파생 타입. `AlbumFolder`, `BmsPath`, `ResourceDonor`, `CustomJacket`을 가짐. ID는 항상 `CUSTOM_` 접두(`CustomTrackData.IsCustomId`)
- **도너 트랙(Resource Donor)**: 곡 목록의 첫 번째 원본 곡. 커스텀 트랙에 없는 게임 리소스(패턴 파일, 오디오, 로딩 영상)를 빌려줌
- **DisplayName**: 곡 제목 표시용 값. 모드는 `CustomTrackData`를 만들 때 객체 초기화로 직접 넣음(리플렉션 없음)
- **SXGTData**: 게임의 차트 데이터 객체(레인별 노트 리스트, BPM, 노트 수 등)
- **laneData**: `Dictionary<int, List<Note>>` — 레인 번호 → 노트 리스트
- **Note / ShortNote / HoldNote**: 게임 내부 노트 타입(기반/일반/홀드)
- **nType**: 노트 타입 enum (`SHORT`, `HOLD` 등)
- **nColor**: 노트 색상 enum (`NONE`, `OPEN`, `BLUE`, `RED`, `SHIFT`, `ACTION`)
- **targetLane**: 노트가 속한 레인 번호 (`LaneIndex`: 0 `LL` … 6 `GATE`, 9 `OPEN`, 10 `ACTION`)
- **duration**: 홀드 지속 시간(초)
- **tickTime**: 홀드 중간 판정 시각 배열. `HoldNote.FinishHoldNote(duration, bpm)`이 16분음표 간격으로 채움
- **EJudges**: 판정 등급 `BLUESTAR`(최고) / `WHITESTAR` / `YELLOWSTAR` / `REDSTAR`(미스)
- **PlayWidget**: 플레이 화면에 장착하는 위젯(최대 5개). 게임은 판정/콤보/점수를 모든 위젯에 브로드캐스트함

## BMS 관련

- **Measure(마디)**: BMS의 `#MMMCC:...`에서 `MMM`에 해당
- **Channel(채널)**: BMS의 `CC`에 해당. sxtg2에서는 채널이 레인을 정함(`16`→0, `11`~`15`→1~5, `18`→6)
- **값(노트 값)**: 데이터의 2(또는 3)글자 단위. sxtg2에서는 `01` 일반, `02`/`03` 홀드 시작/끝, `04`/`05` 오픈 시작/끝
- **Tick**: `measure + (slotIndex / totalSlots)`로 계산되는 위치 값(실수). 시간(초) = `tick × 240 / BPM`
- **OPEN/CLOSE(04/05)**: 오픈 노트 쌍(레인 9로 매핑되는 특수 홀드)

## 모드/후킹 관련

- **Hook**: Harmony로 게임 메서드를 가로채는 패치 코드. sxtg2는 전부 `[HarmonyPatch]` 선언형
- **Prefix/Postfix/Transpiler**: Harmony 패치 위치(원본 호출 전/후/IL 자체 수정)
- **FetchBMSToModules**: 게임이 차트를 노트 생성기·판정 모듈에 넘기는 메서드. 모드가 이 직전(Prefix)에 커스텀 노트를 주입함
- **IsPlayScene**: `Main.IsPlayScene`. 씬 이름에 `play`/`rhythm`/`game`이 들어 있으면 참(로딩 씬 `PlayLoading` 포함). 설정 재로드의 기준
- **IsGameplayScene**: `Main.IsGameplayScene`. 씬 이름이 정확히 `Play`일 때만 참. 판정바/키뷰어 오버레이를 그릴지의 기준
- **SafeAccess**: `GameplayHooks.cs`의 private 필드 접근 헬퍼. 필드를 못 찾으면 예외 대신 null과 경고를 돌려줌
- **Taint(오염) 플레이**: 오토플레이/올퍼펙트/점수 상한 변경/커스텀 곡처럼 정상 기록으로 인정하면 안 되는 플레이. `ResultTaintHook`이 업적과 플레이 횟수를 막음

## 폴더/리소스

- **hwa 폴더**: 커스텀 곡(BMS/BGM/BGA/자켓/트랙 정보)을 두는 게임 폴더 하위 디렉터리. 모드가 자동 생성
- **앨범 폴더**: `hwa` 아래 1단계 하위 폴더. 폴더 하나 = 커스텀 트랙 하나(첫 BMS 기준)
- **CustomNotes 폴더**: 커스텀 노트 스프라이트 PNG 폴더
- **SaveCustomKey/config.txt**: 모드 설정 파일(플레이 씬 진입 시마다 다시 읽음)
