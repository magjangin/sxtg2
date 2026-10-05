# 프로젝트 개요

기준일: 2026-10-05 (v1.1.0)

`sxtg2`는 Sixtar Gate STARTRAIL을 위한 MelonLoader 기반 모드입니다.

주요 목적은 `hwa` 폴더에 배치한 BMS 차트와 미디어 파일을 게임 플레이에 주입하는 것이고, 여기에 플레이 보조
오버레이와 연출/챌린지 옵션이 더해져 있습니다.

## 주요 기능

### 커스텀 곡

- `hwa` 폴더의 앨범 폴더마다 BMS 하나를 커스텀 트랙으로 곡 목록에 추가 (`trackinfo.txt`로 제목/아티스트/난이도 지정)
- BMS 파싱 후 게임 노트로 주입, 노트 수 기반 점수·클리어 판정 보정
- 커스텀 BGM(`music.*`), BGA(`*.mp4`), 미리듣기(`demo.*`), 자켓(`thumb.png` 등) 적용
- BGA를 BGM 재생 위치에 맞춰 자동 동기화

### 플레이 보조

- 실시간 판정바: 게임이 매긴 판정 등급과 오차(ms) 표시, 난이도별 실제 판정 범위 반영
- 키뷰어: 7레인 입력 상태 표시, 색상 커스터마이징

### 연출/챌린지/치트 (기본 꺼짐)

- 노트 흔들림(NoteSway), 노트별 속도 카오스(NoteSpeedChaos)
- 오토플레이, 올퍼펙트(모든 판정을 BLUESTAR로)
- 점수 상한 변경(MaxScore)
- 기록/랭킹 저장 차단: 커스텀 곡·오토플레이·올퍼펙트·점수 상한 변경 플레이는 항상 기록/업적/플레이 횟수에서 제외, 원곡 플레이는 `BlockSave`(기본 켜짐)를 따름

### 스킨

- `CustomNotes` 폴더 PNG로 노트 스프라이트 교체 (노트 이름의 `(Clone)`을 떼고 파일명과 매칭 — `02-systems/NOTE_SYSTEM.md`)

### 설정

- `SaveCustomKey/config.txt` 한 파일로 모든 옵션을 켜고 끔. 플레이 씬에 들어갈 때마다 다시 읽으므로 게임을 재시작할
  필요가 없습니다(v1.1.0).

## 현재 주요 구성

```text
sxtg2-mod/
├── Main/          # MelonMod 진입점, 씬 전환 감지, 매 프레임 호출 분배
├── Features/      # 곡 선택(커스텀 트랙/미리듣기), 판정바, 키뷰어, 진단용 로깅 훅
├── Hooks/         # 플레이 씬 Harmony 훅, 기록·업적 차단, BGM/BGA 처리
├── Loaders/       # BMS/트랙 정보 파서, 커스텀 노트 스프라이트 로더
├── Processors/    # 파싱된 노트 → 게임 노트 주입
├── Models/        # CustomTrackData
└── Helpers/       # 로그, config.txt 설정과 값 파서, 썸네일 로더
```

C# 파일 16개입니다. 파일별 설명은 [../03-development/CODE_STRUCTURE.md](../03-development/CODE_STRUCTURE.md).

## 최근 큰 변화

- 2026-07-21 ~ 07-26: 87개 파일 → 13개로 통합, Harmony 패치 선언형 전환, 커스텀 차트 파이프라인을
  `FetchBMSToModules` Prefix 하나로 단순화
- 2026-07-26 ~ 08-03: 판정바/키뷰어, MaxScore, NoteSway, NoteSpeedChaos, 판정바 모양/위치, 키뷰어 색상 추가
- 2026-08-09 (v1.1.0): `config.txt`를 플레이 씬 진입 때마다 다시 읽음
- 2026-10-05: 알려진 문제 #1~#34 일괄 수정 — 커스텀 곡·치트 플레이의 기록/업적 차단, 노트 스킨 적용, 오토플레이를 게임 플래그로 교체, 진단 로그를 상세 모드로 제한 등
  (`CURRENT_STATUS.md`의 "일괄 수정 내역")

자세한 이력은 [../CURRENT_STATUS.md](../CURRENT_STATUS.md).

## 참고

- 전체 흐름(시간순): [DOCUMENTATION.md](DOCUMENTATION.md)
- 구조와 책임: [ARCHITECTURE.md](ARCHITECTURE.md)
- 설치와 폴더 배치: [../01-user-guide/INSTALL_AND_LAYOUT.md](../01-user-guide/INSTALL_AND_LAYOUT.md)
