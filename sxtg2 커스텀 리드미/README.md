# sxtg2 문서

이 폴더는 Sixtar Gate STARTRAIL용 `sxtg2` 모드 문서 모음입니다.

현재 문서는 2026-05-15 코드 정리 이후 구조를 기준으로 다시 정리했습니다. 오래된 분석/리뷰 문서는 `90-archive/`에 보관했습니다.

## 먼저 볼 문서

- [CURRENT_STATUS.md](CURRENT_STATUS.md): 현재 코드 상태와 최근 정리 내역
- [00-overview/PROJECT_OVERVIEW.md](00-overview/PROJECT_OVERVIEW.md): 프로젝트 개요
- [00-overview/DOCUMENTATION.md](00-overview/DOCUMENTATION.md): 최신 종합 흐름
- [03-development/CODE_STRUCTURE.md](03-development/CODE_STRUCTURE.md): 현재 코드 구조
- [01-user-guide/INSTALL_AND_LAYOUT.md](01-user-guide/INSTALL_AND_LAYOUT.md): 설치와 `hwa` 폴더 배치
- [01-user-guide/TROUBLESHOOTING.md](01-user-guide/TROUBLESHOOTING.md): 문제 해결

## 폴더 구조

```text
sxtg2 커스텀 리드미/
├── README.md
├── CURRENT_STATUS.md
├── 00-overview/       # 개요, 전체 흐름, 용어
├── 01-user-guide/     # 설치, 폴더 배치, 트러블슈팅
├── 02-systems/        # BMS, 미디어, 스코어, 훅 등 세부 시스템
├── 03-development/    # 코드 구조, 구현 상세, 디버깅
└── 90-archive/        # 과거 리뷰/레거시 분석 문서
```

## 최신 코드 기준 요약

- C# 소스 파일은 `sxtg2-mod/` 기준 87개입니다. (`bin`, `obj`, `.vs` 제외)
- `BmsFileResolver`가 BMS 확장자와 파일 선택 규칙을 공통 관리합니다.
- `SXGTReaderHook`, `InitialMediaFileScanner`, `NumberInterpolatorHook`, 미사용 모델/조작기/진단 파일은 제거되었습니다.
- 스코어 보정은 `SXGTReader.MaxScore`와 `SXGTData.maxScore` 중심입니다. `ManagerPlay.targetBestScore` 보정은 제거되었습니다.
- `TextHook`는 아직 플레이 로딩 텍스트 감지 보조 경로로 남아 있습니다.

## 문서 신뢰도

`CURRENT_STATUS.md`, `00-overview/DOCUMENTATION.md`, `03-development/CODE_STRUCTURE.md`는 최신 코드 정리 후 다시 작성한 문서입니다.

`02-systems/`의 일부 오래된 상세 문서는 아직 역사적 설명이 섞여 있을 수 있습니다. 최신 판단이 필요하면 위 세 문서를 우선 기준으로 보세요.
