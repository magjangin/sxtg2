# sxtg2 문서

이 폴더는 Sixtar Gate STARTRAIL용 `sxtg2` 모드 문서 모음입니다.

현재 문서는 **2026-10-05, 코드 v1.1.0(알려진 문제 일괄 수정 브랜치 `fix/known-issues-batch`)** 기준으로 다시 정리했습니다. 2026-07 리팩터링 이전 구조를
설명하던 문서는 `90-archive/`에 보관했습니다.

## 먼저 볼 문서

- [CURRENT_STATUS.md](CURRENT_STATUS.md): 현재 상태, **알려진 문제**, 변경 이력
- [01-user-guide/INSTALL_AND_LAYOUT.md](01-user-guide/INSTALL_AND_LAYOUT.md): 설치, `hwa` 폴더 배치, `config.txt` 전체 항목
- [01-user-guide/TROUBLESHOOTING.md](01-user-guide/TROUBLESHOOTING.md): 증상별 확인 방법과 로그 메시지
- [00-overview/DOCUMENTATION.md](00-overview/DOCUMENTATION.md): 게임 실행부터 결과 화면까지 전체 흐름
- [03-development/CODE_STRUCTURE.md](03-development/CODE_STRUCTURE.md): 파일별 코드 구조, 빌드/테스트

## 폴더 구조

```text
sxtg2 커스텀 리드미/
├── README.md
├── CURRENT_STATUS.md
├── 00-overview/       # 개요, 아키텍처, 전체 흐름, 데이터 흐름, 용어
├── 01-user-guide/     # 설치/폴더 배치/설정, 트러블슈팅
├── 02-systems/        # BMS, 미디어, 노트, 스코어, 훅, 오버레이, 게임 원본 로직
├── 03-development/    # 코드 구조, 디버깅
└── 90-archive/        # 과거 리뷰/레거시 분석 문서 (현재 코드와 다름)
```

## 문서 목록

| 문서 | 내용 |
| --- | --- |
| `00-overview/PROJECT_OVERVIEW.md` | 기능 목록과 구성 요약 |
| `00-overview/ARCHITECTURE.md` | 레이어, 클래스별 책임, 핵심 설계(도너 트랙 방식 등) |
| `00-overview/DOCUMENTATION.md` | 시간순 전체 흐름 |
| `00-overview/DATA_FLOW.md` | 파일 → 게임 데이터 변환 경로 |
| `00-overview/GLOSSARY.md` | 용어집 |
| `02-systems/BMS_FORMAT.md` | sxtg2 전용 BMS 채널/값 규칙, 시간 계산 |
| `02-systems/BMS_PARSING.md` | 파서 동작, 캐시, 제한사항 |
| `02-systems/BMS_SELECTION.md` | 앨범 폴더 스캔, BMS/곡 정보 선택, 트랙 생성 |
| `02-systems/MEDIA_SYSTEM.md` | BGM/BGA/미리듣기/자켓, BGA↔BGM 동기화 |
| `02-systems/NOTE_SYSTEM.md` | 노트 주입, 홀드 틱, 노트 스킨, 흔들림/속도 카오스 |
| `02-systems/SCORE_SYSTEM.md` | 노트 수 보정, 점수 상한(MaxScore), 기록·업적 차단 |
| `02-systems/HOOK_SYSTEM.md` | Harmony 훅 전체 목록과 상세 |
| `02-systems/PLAY_OVERLAY.md` | 판정바, 키뷰어 |
| `02-systems/GAME_LOGIC.md` | 게임 원본의 차트 로드/노트/판정/결과 흐름 (디컴파일 기준) |
| `03-development/CODE_STRUCTURE.md` | 파일별 코드 구조, 빌드 스크립트, 테스트 |
| `03-development/DEBUGGING_GUIDE.md` | 로그 레벨, 확인할 로그, 디컴파일 소스 활용 |

## 최신 코드 기준 요약

- C# 소스 파일은 `sxtg2-mod/` 기준 16개입니다. (`bin`, `obj`, `.vs` 제외)
- 게임 어셈블리를 직접 참조하고, Harmony 훅은 전부 `[HarmonyPatch]` 선언형(MelonLoader가 자동 적용)입니다.
- 커스텀 곡은 곡 선택 화면 `Awake` 때 `hwa` 폴더마다 하나씩 등록되고, 플레이 시작 시
  `ManagerPlay.FetchBMSToModules` Prefix에서 BMS를 파싱해 노트를 주입합니다.
- 스코어/클리어 판정은 `CustomChartInjector`가 `SXGTData.totalNotes`와 `totalNoteWithTicks`를 갱신해 맞춥니다.
- 커스텀 곡/오토플레이/올퍼펙트/점수 상한 변경 플레이는 `ResultGuardHooks`가 기록·업적·플레이 횟수에서 항상 제외합니다.
- 설정은 `SaveCustomKey/config.txt` 하나이며 플레이 씬 진입마다 다시 읽습니다.

## 문서 신뢰도

`90-archive/`를 제외한 문서는 모두 2026-10-05에 현재 코드와 대조했습니다(일괄 수정 반영). 각 문서 첫머리의 "기준일"을 확인하세요.
코드와 문서가 다르게 보이면 코드가 기준이며, 이미 알고 있는 코드 쪽 문제는 `CURRENT_STATUS.md`의 "알려진 문제" 표에 있습니다.
2026-09-29에는 코드 전체를 디컴파일 원본과 다시 대조해 알려진 문제 #13~#21을 정리했고, 이후 #22~#34까지 쌓인 문제를
2026-10-05에 일괄 수정했습니다(수정하지 않고 남긴 항목은 `CURRENT_STATUS.md`의 "남은 문제" 표). **수정된 모드 DLL을 `build.bat`로 게임에
배포해 실제 플레이로 확인하기 전까지는 빌드와 로직 테스트만 거친 상태**입니다. `90-archive/`의 깨진 링크도 2026-09-29에 바로잡았습니다.
