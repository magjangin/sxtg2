# sxtg2

`sxtg2`는 Sixtar Gate STARTRAIL에서 로컬 `hwa` 폴더의 BMS 차트와 미디어 파일을 불러오는 MelonLoader 모드입니다.

## 주요 기능

- BMS 차트를 파싱하고 커스텀 노트를 플레이에 주입
- 음악 선택 흐름에 커스텀 TrackData 추가
- 커스텀 트랙의 BGA, BGM, 미리듣기, 썸네일 교체
- BGA와 BGM 재생 동기화
- 커스텀 차트용 스코어 제한 보정
- ESC 일시정지 메뉴와 Eyecatch 썸네일 흐름 지원

## 저장소 구조

```text
sxtg2-mod/              # 메인 MelonLoader 모드 프로젝트
sxtg2.LogicTests/       # 로직 테스트 실행 프로젝트
sxtg2 커스텀 리드미/    # 프로젝트, 시스템, 사용자 문서
sxtg2.sln               # Visual Studio 솔루션
build.bat               # Debug 빌드 및 로컬 Mods 복사 스크립트
build-release.bat       # Release 빌드 및 로컬 Mods 복사 스크립트
```

## 필요 환경

- Windows
- Sixtar Gate STARTRAIL
- 게임에 설치된 MelonLoader
- 메인 모드 프로젝트 빌드용 Visual Studio/MSBuild
- `sxtg2.LogicTests` 실행용 .NET 도구

메인 프로젝트는 .NET Framework 4.7.2를 대상으로 하며, 로컬 게임 설치 경로의 MelonLoader, Harmony, Unity 어셈블리를 참조합니다.

## 빌드

빌드 스크립트는 현재 각 파일 상단의 로컬 경로 설정을 사용합니다.

- `GAME_PATH`
- `SOURCE_ROOT`

자기 환경에 맞게 값을 수정한 뒤 실행하세요.

```bat
build.bat
```

Release 빌드는 아래 스크립트를 사용합니다.

```bat
build-release.bat
```

경로 설정이 올바르면 스크립트가 솔루션을 빌드하고 `sxtg2.dll`을 게임의 `Mods` 폴더로 복사합니다.

## 커스텀 콘텐츠 배치

게임 설치 폴더 아래에 `hwa` 폴더를 만들고, 그 아래 1단계 폴더를 앨범 폴더로 사용할 수 있습니다.

```text
{게임 폴더}\hwa\
  Album_A\
    trackinfo.txt
    chart.bms
    demo.ogg
    music.ogg
    thumb.png
    bg.mp4
```

차트 확장자는 `.bms`, `.bme`, `.bml`을 지원합니다. BGM 탐색은 `.ogg`, `.mp3`, `.wav`를 지원하고, BGA 탐색은 `.mp4`를 사용합니다.

## 테스트

저장소 루트에서 로직 테스트 스크립트를 실행합니다.

```bat
run-logic-tests.bat
```

## 문서

자세한 문서는 아래에서 시작하세요.

- [문서 목차](sxtg2%20커스텀%20리드미/README.md)
- [프로젝트 개요](sxtg2%20커스텀%20리드미/00-overview/PROJECT_OVERVIEW.md)
- [설치와 폴더 구조](sxtg2%20커스텀%20리드미/01-user-guide/INSTALL_AND_LAYOUT.md)
- [현재 상태](sxtg2%20커스텀%20리드미/CURRENT_STATUS.md)
