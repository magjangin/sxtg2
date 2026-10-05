# 설치/폴더 구조 가이드 (sxtg2 기준)

이 문서는 “어디에 무엇을 놓아야 하는지”를 **최소한의 규칙 + 실제 코드 탐색 범위** 기준으로 설명합니다.
(기준일: 2026-09-28, v1.1.0)

## 0) 다운그레이드 버전 (DepotDownloader)

특정 타겟 버전의 Sixtar Gate STARTRAIL 클라이언트가 필요한 경우 아래 DepotDownloader 명령어를 사용하세요.

```cmd
depotdownloader -app 1802720 -depot 1802721 -manifest 8524424218577615553
```

---

## 1) 설치(Mods)

- 빌드 결과: `sxtg2-mod\bin\Debug\sxtg2.dll` (Release는 `bin\Release\`)
- 설치 위치: `{게임 설치 폴더}\Mods\sxtg2.dll`
- `build.bat`(Debug) / `build-release.bat`(Release)는 빌드 후 위 경로로 복사까지 합니다.
- `build-release.bat`은 `build.bat Release`를 부르는 래퍼이고, 두 스크립트 모두 `x64`로 빌드합니다.
  저장소 위치는 스크립트 위치에서 구하므로 따로 고칠 필요가 없습니다.
- 다른 PC/경로에서 빌드하려면 두 곳만 맞추면 됩니다.
  1. 게임 폴더: 환경 변수 `GAME_PATH`(없으면 스크립트 상단의 기본값 `H:\Sixtar Gate STARTRAIL custom mode`). `NO_PAUSE=1`이면 끝에서 멈추지 않습니다.
  2. `sxtg2-mod\sxtg2.csproj`의 게임 DLL 참조 경로(`H:\Sixtar Gate STARTRAIL custom mode\...`) —
     스크립트의 `GAME_PATH`는 csproj에 전달되지만 csproj가 쓰지 않으므로 직접 고쳐야 합니다.

## 2) hwa 폴더

모드가 처음 초기화될 때 `{게임 설치 폴더}\hwa\`를 **자동으로 만듭니다**. 여기에 앨범 폴더를 넣으세요.

## 3) 앨범 폴더 구조

`hwa` 아래 **1단계 하위 폴더 하나 = 커스텀 트랙 하나**입니다. 곡 선택 화면에 들어갈 때마다 다시 스캔하므로,
폴더를 추가/수정한 뒤 곡 선택 화면으로 돌아가면 반영됩니다.

```text
{게임 설치 폴더}\hwa\
  Album_A\
    chart.bms          ← 폴더의 첫 BMS 하나만 사용
    trackinfo.txt      ← 제목/아티스트/난이도
    music.ogg          ← 플레이 BGM
    demo.ogg           ← 곡 선택 미리듣기 (선택)
    thumb.png          ← 자켓 (선택)
    bg.mp4             ← BGA (선택)
  Album_B\
    ...
  (hwa 루트에 BMS를 직접 두면 루트도 트랙 하나로 등록됨)
```

- 2단계 이상 아래(`hwa\A\B\chart.bms`)는 찾지 않습니다.
- 한 폴더에 BMS가 여러 개 있어도 하나만 씁니다(`*.bms` → `*.bme` → `*.bml` 순으로 처음 찾은 파일).
  난이도별 차트는 폴더를 나누세요.
- 곡 선택 화면의 어떤 난이도를 골라도 같은 BMS가 재생되고, 판정 범위만 난이도에 따라 달라집니다.

## 4) 리소스 파일 규칙(요약)

모든 탐색은 **해당 앨범 폴더의 최상위만** 봅니다(`hwa` 루트나 다른 폴더로 폴백하지 않음).

### BMS

- 확장자: `.bms`, `.bme`, `.bml`
- sxtg2 전용 채널/값 규칙을 씁니다: `02-systems/BMS_FORMAT.md`
- 모든 홀드 시작(`02`/`04`)에는 끝(`03`/`05`)을 두세요. 끝이 빠진 홀드(`02`)는 일반 노트로, 끝이 빠진 오픈 노트(`04`)는 기본 길이 1초로
  바뀌어 판정이 멈추지는 않지만, 의도와 달라지고 로그에 경고(`[CustomChartInjector] 끝(03)이 없는 홀드 …`)가 남습니다.
- BMS 헤더(`#TITLE` 등)는 `#`와 `:` 사이가 데이터 줄 형식(마디 숫자 + 채널)이 아니면 무시되므로, 값에 콜론이 있어도 가짜 노트가 생기지 않습니다.
  제목은 그래도 `trackinfo.txt`에 쓰세요(BMS 헤더의 제목은 읽지 않습니다).
- ⚠️ **GATE 노트(`18` 채널)는 앞에 오픈 노트(`04`~`05`)가 있어야 칠 수 있습니다.** 게이트는 닫힌 채 시작하고, 닫혀 있으면 게임이 GATE
  키 입력을 무시합니다. 오픈 노트 한 쌍은 게이트를 한 번 열거나 닫는(토글) 것이고 길이는 애니메이션 시간입니다
  (`02-systems/BMS_FORMAT.md`의 "게이트와 오픈 노트" 절).
- `#WAV` 헤더는 키가 `WAV` + 3자리(`#WAV001`)일 때만 "값 3글자" 모드를 켭니다(`#WAVCMD` 같은 명령 줄은 무시). `#BPM`은 0 초과 10만 이하의 값만
  인정하고, 그 밖의 값(`Infinity` 등)은 건너뛰고 다음 `#BPM`(없으면 150)을 씁니다.

### TrackInfo (곡 정보 txt)

- 파일: `{BMS 파일명}.txt` → `trackinfo.txt` → `info.txt` → 폴더의 첫 `*.txt` 순서로 하나를 읽음(UTF-8)
- 형식: 한 줄에 `키: 값`, `#`/`//`로 시작하면 주석
- 파일은 **UTF-8**로 저장하세요. 메모장에서 ANSI로 저장하면 `제목:` 키가 깨져 읽히지 않습니다. 이때는 `[TrackInfoParser] …UTF-8이 아닙니다` 또는
  `…하나도 읽지 못했습니다` 경고가 로그에 남습니다. `subtitle:`/`부제:` 키는 제목을 덮어쓰지 않고 무시합니다.

```text
제목: My Custom Song
아티스트: Someone
난이도: 3, 7, 11, 14
```

- 키: 제목 = `제목`/`곡 제목`/`title`, 아티스트 = `아티스트`/`artist`/`작곡가`, 난이도 = `난이도`/`difficulty`/`level`
- 난이도 4칸을 앞에서부터 채우고, 모자라면 마지막 값을 반복합니다. 제목이 없으면 **앨범 폴더 이름**(폴더 이름도 비면 `커스텀 차트`), 작곡가/난이도는 도너 곡 값.
- 자세한 규칙: `02-systems/BMS_SELECTION.md`

### BGM

- `music.ogg` → `music.mp3` → `music.wav`
- 없으면 폴더에서 **가장 큰** `.ogg`/`.mp3`/`.wav` 파일을 쓰고 경고를 남깁니다(곡 음원은 키음보다 훨씬 크다는 가정). 그래도 엉뚱한 파일이 골라질 수
  있으니 **곡 음원은 `music.*`로 두세요**.
- 로드에 실패하면 도너 곡(곡 목록 첫 곡)의 BGM이 재생됩니다. 곡 종료 시점도 BGM 길이로 정해집니다.

### BGA

- 폴더에서 이름순으로 첫 `*.mp4` (다른 형식은 찾지 않음). 영상이 곡보다 짧으면 끝난 뒤에는 그대로 둡니다.
- 게임 설정에서 BGA가 켜져 있을 때만 적용됩니다.

### Preview(미리듣기)

- `demo.*` → `music.*` → 폴더에서 가장 큰 오디오 (확장자는 각각 `.ogg` → `.mp3` → `.wav`)
- 구간 반복이 아니라 파일 전체를 한 번 재생합니다. 짧은 미리듣기를 원하면 `demo.ogg`를 따로 만드세요.

### 자켓

- `thumb.png` → `thumbnail.png` → `jacket.png` → `cover.png` → `image.png`
- 곡 목록, 확인창, 로딩, 일시정지, 결과 화면에 모두 적용됩니다. 없으면 게임 기본 자켓(곡마다 한 번만 경고 로그가 남음).

자세한 내용: `02-systems/MEDIA_SYSTEM.md`

## 5) CustomNotes 폴더(선택, 커스텀 노트 스킨)

- 경로: `{게임 설치 폴더}\CustomNotes\` — 모드 초기화 시 자동 생성, PNG만 읽음, **게임 시작 시 1회** 로드
- 파일명(확장자 제외)이 그대로 키가 됩니다(대소문자 무시).

게임이 노트를 만들면 이름이 `_Blue(Clone)`처럼 됩니다. 모드는 `(Clone)` 접미사를 떼고 `Blue`로 찾으므로 파일 이름은 이렇게 짓습니다
(2026-10-05부터. 그 전에는 `Blue.png`가 매칭되지 않았습니다):

| 대상 | 파일명 (우선순위순, 대소문자 무시) |
| --- | --- |
| 노트 본체 | `Blue.png` (`White`, `Red`, `Gate`) → 전체 이름 `_Blue.png` |
| 홀드 끝 | `Blue_tail.png` → `tailNote.png` → 노트 본체와 같은 이미지 |
| 홀드 몸통 | `Blue_hold.png` → `holdTexture.png` → 노트 본체와 같은 이미지 |

- `White`는 레인 L/R 노트, `Blue`는 LL/RR과 오픈 노트, `Red`는 LT/RT, `Gate`는 게이트 노트입니다. 파일이 없는 종류는 게임 기본 스킨 그대로입니다.
- 예전 안내대로 `Blue(Clone).png`처럼 접미사까지 붙인 파일도 `Blue`로 인식합니다.
- 스킨이 적용된 이미지는 스프라이트 원본 크기로 맞춘 뒤 게임의 노트 크기 옵션(`noteSize`)을 다시 곱합니다. 스킨이 없는 노트는 건드리지 않습니다.
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`

## 6) SaveCustomKey 폴더(모드 설정 파일)

- 경로: `{게임 설치 폴더}\SaveCustomKey\config.txt`
- 모드 초기화 시 폴더와 기본 설정 파일을 자동 생성하고 읽습니다(`SaveCustomKeyConfig`, `Helpers/ModHelpers.cs`).
- **플레이 씬에 들어갈 때마다 파일을 다시 읽습니다(v1.1.0).** 게임을 재시작할 필요 없이 저장 후 다음 판부터 적용되고,
  한 판 도중에는 바뀌지 않습니다. 리트라이도 재진입으로 취급되어 다시 읽습니다.
  - 재로드 때는 먼저 모든 값을 기본값으로 되돌린 뒤 읽으므로, 줄을 지우거나 주석 처리하면 기본값으로 돌아갑니다.
  - 바뀐 항목만 로그에 남습니다: `[SaveCustomKey] 설정 재로드 #n (...) - N개 항목이 이번 플레이부터 적용됩니다`
  - 파일을 읽지 못하면(편집기가 잠근 상태 등) 기존 값을 유지하고 다음 플레이 때 다시 시도합니다.
  - 새로 만들어지는 파일 머리말의 "게임 실행 시 자동 적용됩니다" 문구는 옛 설명입니다. 실제 동작은 위와 같습니다.
- `#` 또는 `//`로 시작하는 줄은 주석입니다. 형식은 `키=값`이고 키는 대소문자를 구분하지 않습니다.
- 값 뒤의 줄 끝 주석도 인식합니다: `AutoPlay=1 # 메모`, `JudgmentBarSide=Left // 메모`(주석 기호 앞에 공백이 있을 때. `#26BFD9D9` 같은 색상 값은
  그대로 읽습니다). 모르는 키, 알 수 없는 값, `키=값` 형식이 아닌 줄은 **경고를 남기고 무시**합니다.

### 켜기/끄기 값

| 켜기 | 끄기 |
| --- | --- |
| `1`, `true`, `t`, `on`, `yes`, `y`, `enable`, `enabled`, `켜짐`, `켜기`, `사용`, `활성화`, `참`, `트루` | `0`, `false`, `f`, `off`, `no`, `n`, `disable`, `disabled`, `꺼짐`, `끄기`, `미사용`, `비활성화`, `거짓`, `폴스` |

목록에 없는 값은 경고(`[SaveCustomKey] … 켜기/끄기로 읽지 못했습니다`)를 남기고 기본값이 유지됩니다.

### 항목 전체

| 키 (별칭) | 기본값 | 설명 |
| --- | --- | --- |
| `AutoPlay` | `0` | 오토 플레이(게임 자체 오토플레이 경로를 켬. 키 입력은 무시되고 홀드도 정상적으로 끝남) |
| `AllPerfect` | `0` | 모든 판정을 BLUESTAR로 강제 |
| `BlockSave` | `1` | 원본 곡의 베스트 스코어/랭킹 저장 차단 (커스텀 곡은 이 값과 무관하게 항상 차단, 아래 참고) |
| `EnableJudgmentBar` (`JudgmentBar`) | `1` | 실시간 판정바 표시 |
| `JudgmentBarVertical` | `1` | 판정바 방향 (1 = 세로, 0 = 가로) |
| `JudgmentBarShape` | `0` | 바 모양: `0` 사각(`rect`, `사각`), `1` 알약(`capsule`, `캡슐`), `2` 삼각/다이아몬드(`triangle`, `삼각`) |
| `JudgmentBarRangeShape` | `-1` | 안쪽 판정 범위 박스 모양: `-1` 바 모양 따라감(`same`, `추종`), `0`/`1`/`2`는 위와 같음 |
| `JudgmentBarCapsule` | (없음) | 옛 키. `1`이면 `JudgmentBarShape=1`, `0`이면 캡슐일 때만 사각으로 |
| `JudgmentBarSide` (`JudgmentBarPosition`) | `Center` | `Left`(`왼쪽`, `-1`) / `Right`(`오른쪽`, `1`) / `Center`(`중앙`, `0`) |
| `EnableKeyViewer` (`KeyViewer`) | `1` | 실시간 키뷰어 표시 |
| `KeyViewerPressedColor` | `#26BFD9D9` | 키 눌림 색 |
| `KeyViewerNormalColor` | `#141414A6` | 키 기본 색 |
| `KeyViewerGatePressedColor` | `#FF4081E6` | GATE 키 눌림 색 |
| `MaxScore` (`ScoreLimit`) | `1000000` | 점수 상한(만점 기준값), 0.001 이상 |
| `NoteSway` (`EnableNoteSway`) | `0` | 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 |
| `NoteSwayAmplitude` | `12` | 흔들림 폭(픽셀), 0 ~ 1000 |
| `NoteSwaySpeed` | `0.8` | 흔들림 속도(초당 왕복 횟수), 0 ~ 50 |
| `NoteSwayDamping` | `1` | 판정선에 가까워지면 흔들림을 잦아들게 함 |
| `NoteSwayDampingTime` | `0.4` | 판정선 도달 몇 초 전부터 잦아들지, 0.01 ~ 30 |
| `NoteSpeedChaos` (`EnableNoteSpeedChaos`) | `0` | [챌린지] 노트마다 낙하 속도를 제각각으로 |
| `NoteSpeedChaosMin` | `0.6` | 속도 배율 최솟값 (1 = 원래 속도), 0.05 ~ 10 |
| `NoteSpeedChaosMax` | `1.8` | 속도 배율 최댓값, 0.05 ~ 10 (Min보다 작으면 두 값을 맞바꿈) |
| `NoteSpeedChaosPerLane` | `0` | 1 = 레인마다, 0 = 노트마다 속도가 다름 |

- 숫자 항목은 소수점 `.`으로 씁니다. 범위를 벗어나거나 숫자가 아니면 경고를 남기고 기본값을 유지합니다.
- 색상은 `#RRGGBB`, `#RRGGBBAA`, `R,G,B[,A]`(0~255 또는 0~1), 영문/한글 색상명을 씁니다. 목록은 `02-systems/PLAY_OVERLAY.md`.
- `MaxScore`의 IL 패치는 항상 붙어 있고, 기본값이면 원본과 똑같이 동작합니다. 자세한 내용: `02-systems/SCORE_SYSTEM.md`
- `NoteSway`와 `NoteSpeedChaos`는 판정에 전혀 영향이 없습니다(판정은 시간만 봄). 자세한 내용: `02-systems/NOTE_SYSTEM.md`
- 예전 버전에서 만든 파일에 다음 묶음이 없으면 **게임을 시작할 때** 파일 끝에 기본값 줄을 자동으로 덧붙입니다:
  판정바 모양(`JudgmentBarShape`/`RangeShape`), 판정바 위치, 키뷰어 색상, `MaxScore`, `NoteSway` 묶음, `NoteSpeedChaos` 묶음.
  (새로 만든 파일에도 키뷰어 색상 묶음이 빠져 있어서, 첫 실행 때 파일 끝에 추가됩니다.)
  플레이 씬 진입 때의 재로드에서는 파일을 **수정하지 않습니다**. 그래서 묶음을 지우거나 주석 처리해도 다음 게임 시작 전까지는
  되살아나지 않고, 그동안은 기본값으로 동작합니다(2026-10-04부터).

### BlockSave와 기록 보호

기록/업적 보호는 두 단계입니다(2026-10-05부터).

- **항상 보호하는 플레이**: `AutoPlay`, `AllPerfect`, `MaxScore`를 1000000이 아닌 값으로 바꾼 플레이, 그리고 **모든 커스텀 곡 플레이**.
  이 플레이는 `BlockSave` 값과 무관하게
  - 로컬 베스트 스코어 저장(`UserAccountModule.SavePlayData`)과 서버 랭킹 전송(`LyrebirdServer.PostUserScore`)을 막고,
  - Steam 업적 해금(`PUREBLUE_FIRST`, `FULLCOMBO_FIRST` 등), 플레이 횟수/실패 횟수 증가, 마지막 플레이 곡 저장을 되돌립니다(`ResultTaintHook`).
- **`BlockSave`가 정하는 것**: 위 조건에 해당하지 않는 **원본 곡**의 기록 저장/전송만 막습니다. `BlockSave=1`(기본)이면 막고, `BlockSave=0`이면
  원본 곡 기록이 평소처럼 저장됩니다. `config.txt` 값만 따르며(MelonPreferences의 `BlockSaveBestRanking`은 더 이상 읽지 않음), 플레이 씬에 들어갈 때
  다시 읽습니다. 업적/플레이 횟수는 `BlockSave`로 막지 않습니다(원본 곡을 평범하게 플레이하면 평소처럼 올라감).
- 커스텀 곡 ID(`CUSTOM_…`)는 공식 서버로 보내지 않습니다(곡 시작 때의 플레이 카운트 차단, 랭킹 조회는 빈 목록으로 응답).
- 차단될 때마다 `[차단] 하이스코어 및 랭킹 저장 차단(사유): …` 로그가 남고, 결과 화면의 베스트 점수 표시는 원본처럼 갱신됩니다(저장만 안 함).
## 7) MelonPreferences 항목

`{게임 설치 폴더}\UserData\MelonPreferences.cfg`의 `[sxtg2]` 카테고리입니다(게임을 한 번 실행하면 생김).

| 항목 | 기본값 | 설명 |
| --- | --- | --- |
| `LogLevel` | `1` | `0` 오류만, `1` 보통, `2` 상세(대량 로그). 문제 조사 시 `2`로 |
| `EnableAutoPlay` | `false` | `config.txt`의 `AutoPlay`와 OR(둘 중 하나가 켜지면 오토플레이) |
| `EnableAllPerfect` | `false` | `config.txt`의 `AllPerfect`와 OR |
| `BlockSaveBestRanking` | `true` | **더 이상 읽지 않음**(항목만 남아 있음). 기본값 `true`가 `config.txt`의 `BlockSave=0`을 덮어써서 끌 수 없었으므로 `config.txt`의 `BlockSave`만 씁니다 |

게임 자체의 커스텀 키 설정은 `UserAccountModule.Instance.userData.customKeySetting`(세이브 데이터 내부,
`GameSetting/KeyPresetSetting.cs`)로 관리되며, 모드 설정과는 별개입니다. 키 프리셋을 파일로 내보내기/가져오기 하는 기능은 없습니다.

---

## 기술 스택 및 DLL 참조

### 프로젝트 설정

- **타겟 프레임워크**: .NET Framework 4.7.2 (C# 9.0, x64)
- **프로젝트 파일**: `sxtg2-mod/sxtg2.csproj`

### 어셈블리 어트리뷰트

```csharp
[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", sxtg2.ModInfo.Version, "화영왕")]   // ModInfo.Version = "1.1.0" (Properties/AssemblyInfo.cs)
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)] // 인자 순서 (alpha, red, green, blue) → 반투명 시안
```

- 개발 환경의 MelonLoader: 0.7.3 (`MelonLoader\net35\MelonLoader.dll` 기준)
- 버전은 `Properties/AssemblyInfo.cs`의 `ModInfo.Version` 한 곳에서 정하고, `MelonInfo`와 `AssemblyVersion`이 같은 상수를 씁니다. 올릴 때는 이 상수와 README의 버전만 고치면 됩니다.

**위치**: `sxtg2-mod/Main/Main.cs`

### DLL 참조 (`sxtg2.csproj`)

- `MelonLoader.dll`, `0Harmony.dll`: `{게임 설치 폴더}\MelonLoader\net35\`
- `Assembly-CSharp.dll`: `{게임 설치 폴더}\Sixtar Gate STARTRAIL_Data\Managed\` — 게임 타입을 직접 참조
- Unity: 같은 `Managed` 폴더의 `UnityEngine.dll`, `UnityEngine.*.dll`, `Unity.*.dll`을 **와일드카드로 전부** 참조

모두 `Private=False`(출력 폴더로 복사하지 않음)입니다.
