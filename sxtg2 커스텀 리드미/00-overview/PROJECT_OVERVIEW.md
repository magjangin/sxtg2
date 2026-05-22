# 프로젝트 개요

`sxtg2`는 Sixtar Gate STARTRAIL을 위한 MelonLoader 기반 커스텀 모드입니다.

주요 목적은 `hwa` 폴더에 배치한 BMS 차트와 미디어 파일을 게임 플레이에 주입하는 것입니다.

## 주요 기능

- BMS 파일 파싱 및 커스텀 노트 주입
- 커스텀 TrackData 주입
- 커스텀 BGA/BGM 교체
- BGA/BGM 시간 동기화
- 커스텀 썸네일/프리뷰 음악 적용
- 커스텀 차트용 스코어 제한 보정
- ESC 일시정지 메뉴 호출 및 Eyecatch 썸네일 적용

## 현재 주요 구성

```text
sxtg2-mod/
├── Main/          # MelonMod 진입점과 초기화/업데이트/BMS 부트스트랩
├── Features/      # 커스텀 플레이 흐름, 씬 감지, TrackData 주입
├── Hooks/         # Harmony 후킹과 게임 객체 교체
├── Helpers/       # Reflection, Track, Screen, Pause 등 보조 기능
├── Loaders/       # BMS/track info 파서
└── Processors/    # BMS 노트를 게임 노트로 변환/주입
```

## 현재 정리된 점

- 오래된 실험/진단 후킹을 제거했습니다.
- BMS 파일 선택 규칙을 `BmsFileResolver`로 모았습니다.
- 스코어 보정에서 타입 불일치가 나던 `targetBestScore` 수정 경로를 제거했습니다.
- 루트 문서 폴더를 주제별 하위 폴더로 정리했습니다.

## 참고

가장 최신의 실제 코드 흐름은 [DOCUMENTATION.md](DOCUMENTATION.md)와 [../CURRENT_STATUS.md](../CURRENT_STATUS.md)를 우선 확인하세요.
