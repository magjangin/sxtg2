# sxtg2

`sxtg2`는 Sixtar Gate STARTRAIL에서 로컬 `hwa` 폴더의 BMS 차트와 미디어 파일을 불러오는 MelonLoader 모드입니다.
현재 버전: **v1.1.0**

## 주요 기능

- `hwa` 폴더의 BMS 차트를 곡 목록에 커스텀 트랙으로 추가하고 플레이에 노트 주입
- 커스텀 트랙의 BGM, BGA, 미리듣기, 자켓 교체 및 BGA↔BGM 재생 동기화
- 커스텀 차트 노트 수 기반 스코어/클리어 판정 보정
- 플레이 오버레이: 실시간 판정바(난이도별 실제 판정 범위), 키뷰어(색상 커스터마이징)
- 연출/챌린지: 노트 흔들림(NoteSway), 노트별 속도 카오스(NoteSpeedChaos)
- 오토플레이, 올퍼펙트, 점수 상한 변경(MaxScore), 기록/랭킹 저장 차단(BlockSave)
- 커스텀 곡과 오토플레이/올퍼펙트/점수 상한 변경 플레이는 게임 기록·Steam 업적·플레이 횟수에서 항상 제외
- `CustomNotes` 폴더 PNG로 노트 스킨 교체 (`blue.png`, `red.png` 등 노트 이름과 같은 파일명)
- 모든 옵션은 `SaveCustomKey/config.txt` 하나로 설정하며, 플레이를 시작할 때마다 다시 읽어 게임 재시작 없이 반영

## 저장소 구조

```text
sxtg2-mod/              # 메인 MelonLoader 모드 프로젝트 (C# 16개 파일)
sxtg2.LogicTests/       # BMS 파서/설정 파서 로직 테스트 (.NET 8 콘솔)
sxtg2 커스텀 리드미/    # 프로젝트, 시스템, 사용자 문서
tools/                  # 메서드 길이 측정 스크립트 (method_length_scan.py)
sxtg2.sln               # Visual Studio 솔루션 (메인 모드 프로젝트만 포함)
build.bat               # 빌드(Debug 기본, build.bat Release도 가능) 후 Mods 폴더로 복사
build-release.bat       # build.bat Release를 부르는 래퍼
run-logic-tests.bat     # 로직 테스트 실행
```

저장소 루트의 `sxtg2/` 폴더(게임 어셈블리 디컴파일 결과)는 `.gitignore`로 제외되어 있으며, 로컬에서 게임 코드를 확인할 때 씁니다.

## 필요 환경

- Windows
- Sixtar Gate STARTRAIL
- 게임에 설치된 MelonLoader (개발 환경: 0.7.3)
- 메인 모드 프로젝트 빌드용 Visual Studio/MSBuild
- `sxtg2.LogicTests` 실행용 .NET SDK (대상 프레임워크 .NET 8)

메인 프로젝트는 .NET Framework 4.7.2를 대상으로 하며, 로컬 게임 설치 경로의 MelonLoader, Harmony, Unity 어셈블리와
게임 어셈블리(`Assembly-CSharp.dll`)를 참조합니다.

### 다운그레이드 버전 (DepotDownloader)

특정 버전의 게임 클라이언트가 필요한 경우 아래 DepotDownloader 명령어를 사용할 수 있습니다.

```cmd
depotdownloader -app 1802720 -depot 1802721 -manifest 8524424218577615553
```

## 빌드

빌드 전에 게임 설치 경로를 확인하세요.

- 게임 설치 폴더는 환경 변수 `GAME_PATH`로 바꿀 수 있습니다(기본 `H:\Sixtar Gate STARTRAIL custom mode`). 이 값은 DLL 참조 경로(csproj의 `GamePath`)와 `Mods` 복사 위치에 모두 쓰입니다.
- Visual Studio에서 csproj를 직접 빌드할 때는 csproj의 `GamePath` 기본값(같은 `H:\...` 경로)이 쓰입니다. 다른 경로에 설치했다면 `GAME_PATH`로 빌드하거나 `GamePath` 기본값을 고치세요.
- 저장소 위치는 스크립트 위치에서 자동으로 구합니다.

```bat
build.bat
```

Release 빌드는 아래 중 하나를 사용합니다.

```bat
build.bat Release
build-release.bat
```

스크립트는 솔루션을 x64로 빌드하고 `sxtg2.dll`을 `{GAME_PATH}\Mods`에 **복사(배포)** 합니다. 배포 없이 빌드만 확인하려면 `NO_DEPLOY=1`을
설정하세요(`set NO_DEPLOY=1` 후 `set NO_PAUSE=1`). 참조하는 DLL은 항상 `GAME_PATH`의 게임 설치본입니다. 자세한 내용은
`sxtg2 커스텀 리드미/03-development/CODE_STRUCTURE.md`.

## 커스텀 콘텐츠 배치

모드가 게임 설치 폴더 아래에 `hwa` 폴더를 자동으로 만듭니다. 그 아래 1단계 폴더 하나가 커스텀 트랙 하나입니다.

```text
{게임 폴더}\hwa\
  Album_A\
    chart.bms        # 폴더의 첫 BMS 하나만 사용
    trackinfo.txt    # 제목: / 아티스트: / 난이도:
    music.ogg        # 플레이 BGM (이름을 music으로 두는 것을 권장)
    demo.ogg         # 곡 선택 미리듣기 (없으면 music 전체 재생)
    thumb.png        # 자켓
    bg.mp4           # BGA (게임 설정에서 BGA가 켜져 있을 때)
```

차트 확장자는 `.bms`, `.bme`, `.bml`을 지원하며, 채널/값은 sxtg2 전용 규칙을 따릅니다
([BMS 포맷](sxtg2%20커스텀%20리드미/02-systems/BMS_FORMAT.md)). BGM/미리듣기는 `.ogg`, `.mp3`, `.wav`,
BGA는 `.mp4`만 찾습니다. 자세한 규칙과 `config.txt` 전체 항목은 [설치와 폴더 구조](sxtg2%20커스텀%20리드미/01-user-guide/INSTALL_AND_LAYOUT.md)를 보세요.

## 테스트

저장소 루트에서 로직 테스트 스크립트를 실행합니다(현재 13개).

```bat
run-logic-tests.bat
```

## 문서

자세한 문서는 아래에서 시작하세요.

- [문서 목차](sxtg2%20커스텀%20리드미/README.md)
- [현재 상태 / 알려진 문제](sxtg2%20커스텀%20리드미/CURRENT_STATUS.md)
- [프로젝트 개요](sxtg2%20커스텀%20리드미/00-overview/PROJECT_OVERVIEW.md)
- [설치와 폴더 구조](sxtg2%20커스텀%20리드미/01-user-guide/INSTALL_AND_LAYOUT.md)
- [트러블슈팅](sxtg2%20커스텀%20리드미/01-user-guide/TROUBLESHOOTING.md)
