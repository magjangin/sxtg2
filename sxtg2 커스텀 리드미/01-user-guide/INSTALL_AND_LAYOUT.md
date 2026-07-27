# 설치/폴더 구조 가이드 (sxtg2 기준)

이 문서는 “어디에 무엇을 놓아야 하는지”를 **최소한의 규칙 + 실제 코드 탐색 범위** 기준으로 설명합니다.

## 1) 설치(Mods)

- 빌드 결과: `sxtg2-mod\bin\Debug\sxtg2.dll`
- 설치 위치: `{게임 설치 폴더}\Mods\sxtg2.dll`
- `build.bat`는 기본적으로 위 경로로 복사까지 수행합니다(환경에 따라 상단 경로 변수 수정 필요).

## 2) hwa 폴더(필수)

현재 `sxtg2`는 `hwa` 폴더를 자동 생성하지 않습니다.  
아래 경로에 직접 폴더를 만들어 주세요.

- `{게임 설치 폴더}\hwa\`

## 3) 추천 폴더 구조(앨범 폴더)

`sxtg2`는 `hwa` 아래의 **1단계 하위 폴더들을 “앨범 폴더”**로 취급하여,
트랙의 `DisplayName`/`trackId` 기반 매칭에 사용합니다.

예시:

```text
{게임 설치 폴더}\hwa\
  \Album_A\
    trackinfo.txt
    <trackId>.bms
    demo.ogg
    music.ogg
    thumb.png
    bg.mp4
  \Album_B\
    ...
  \(root files - optional)\
    any.bms
    any.mp4
    any.ogg
```

## 4) 리소스 파일 규칙(요약)

### BMS

- 확장자: `.bms`, `.bme`, `.bml`
- 초기 스캔(통계/기본값 세팅)은 `hwa` 루트 + 앨범 폴더(1단계)까지만 검사합니다.
- 플레이 시점에는 트랙 기반 탐색으로 앨범 폴더 우선 매칭을 시도합니다.

### BGA

- 확장자: `.mp4`
- 앨범 폴더 우선, 없으면 `hwa` 루트

### BGM

- 확장자: `.ogg`, `.mp3`, `.wav`
- 우선순위: OGG → MP3 → WAV (폴더 내 첫 파일)

### Preview(미리듣기)

- `demo.ogg` 우선, 없으면 `music.ogg` (확장자별로도 폴백 존재)
- 관련 구현은 `ManagerMusicSelectHook.Preview.cs`를 기준으로 합니다.

### TrackInfo(txt)

- 트랙 제목/아티스트/난이도 같은 메타데이터를 앨범 폴더 단위로 제공할 수 있습니다.
- 관련 구현은 `sxtg2-mod/Loaders/TrackInfoParser.cs`, `sxtg2-mod/Features/TrackDataAnalyzer.cs`를 참고하세요.

## 5) CustomNotes 폴더(선택, 커스텀 노트 스킨)

- 경로: `{게임 설치 폴더}\CustomNotes\`
- 폴더가 없으면 모드 초기화 시 자동 생성됩니다(`Loaders/CustomNoteSpriteLoader.cs`).
- 파일명 규칙(대소문자 무시, PNG만):
  - `Blue.png`, `Red.png` — 메인 노트. 없으면 게임 기본 스프라이트 유지(폴백 없음)
  - `Gate.png` — 게이트 노트. 없으면 `Blue.png`로 폴백
  - 끝노트(홀드 끝부분): `RedTail`/`TailRed`, `BlueTail`/`TailBlue`, `GateTail`/`TailGate` 우선,
    없으면 공용 `Tail`/`TailNote` 순서로 탐색. 전부 없으면 적용하지 않음(폴백 없음)
  - 홀드 몸통: `RedHold`/`HoldRed`, `BlueHold`/`HoldBlue`, `GateHold`/`HoldGate` 우선,
    없으면 공용 `Hold`/`HoldTexture` 순서로 탐색. 전부 없으면 적용하지 않음(폴백 없음)
- 관련 구현: `Loaders/CustomNoteSpriteLoader.cs`, `Hooks/Note/NoteSpriteHook.cs`, `Helpers/UI/NoteRendererRecovery.cs`
- 자세한 흐름은 `02-systems/NOTE_SYSTEM.md`의 "노트 스킨(커스텀 스프라이트)" 절 참고.

## 6) SaveCustomKey 폴더(모드 설정 파일)

- 경로: `{게임 설치 폴더}\SaveCustomKey\config.txt`
- 모드 초기화 시 `SaveCustomKeyConfig.Initialize()`(`Helpers/ModHelpers.cs`)가 폴더와 설정 파일을
  자동 생성하고 읽어옵니다. 설정은 **게임 시작 시 1회** 로드되므로, 값을 바꿨으면 게임을 다시 켜야 합니다.
- 값 형식은 유연합니다: `1`/`0`, `true`/`false`, `on`/`off`, `켜짐`/`꺼짐` 모두 인식합니다.
  `#` 또는 `//`로 시작하는 줄은 주석입니다.

| 키 | 기본값 | 설명 |
| --- | --- | --- |
| `AutoPlay` | `0` | 오토 플레이 |
| `AllPerfect` | `0` | 모든 판정을 BLUESTAR로 강제 |
| `BlockSave` | `1` | 베스트 스코어/랭킹 저장 차단 |
| `EnableJudgmentBar` | `1` | 실시간 판정바 표시 |
| `JudgmentBarVertical` | `1` | 판정바 형태 (1 = 세로, 0 = 가로) |
| `EnableKeyViewer` | `1` | 실시간 키뷰어 표시 |
| `MaxScore` | `1000000` | 점수 상한(만점 기준값). `ScoreLimit`도 같은 키로 인식 |
| `NoteSway` | `0` | 노트가 눈송이처럼 좌우로 흔들리며 내려오는 연출 |
| `NoteSwayAmplitude` | `12` | 흔들림 폭(픽셀) |
| `NoteSwaySpeed` | `0.8` | 흔들림 속도(초당 왕복 횟수) |
| `NoteSwayDamping` | `1` | 판정선에 가까워지면 흔들림을 잦아들게 함 |
| `NoteSwayDampingTime` | `0.4` | 판정선 도달 몇 초 전부터 잦아들지(초) |

- 불리언이 아니라 **숫자 값**인 항목: `MaxScore`, `NoteSwayAmplitude`, `NoteSwaySpeed`,
  `NoteSwayDampingTime`. 허용 범위를 벗어나거나 숫자로 읽을 수 없는 값은 경고 로그를 남기고
  기본값이 유지됩니다.
- `MaxScore`는 기본값 `1000000`이면 원본과 동일하게 동작하고, 다른 값을 넣었을 때만 게임 코드에
  IL 패치가 적용됩니다. 자세한 내용은 `02-systems/SCORE_SYSTEM.md`의 "점수 상한 설정" 절 참고.
- `NoteSway`는 판정에 전혀 영향이 없는 순수 시각 효과입니다. 판정은 노트의 화면 위치가 아니라
  시간만 보기 때문입니다. 자세한 내용은 `02-systems/NOTE_SYSTEM.md`의 "노트 흔들림 연출" 절 참고.
- 설정 파일에 항목이 아예 없으면 위 기본값이 그대로 적용됩니다(이전 버전에서 만들어진 파일이라
  새 항목이 빠져 있어도 동작함). 단 `MaxScore`와 `NoteSway` 계열은 예외로, 항목이 없으면 모드가
  파일 끝에 기본값 줄을 자동으로 덧붙여줍니다.
- 판정바/키뷰어의 표시 규칙과 동작은 `02-systems/PLAY_OVERLAY.md`를 참고하세요.
- 게임 자체의 커스텀 키 설정은 `UserAccountModule.Instance.userData.customKeySetting`(세이브 데이터 내부,
  `GameSetting/KeyPresetSetting.cs`)로 관리되며, 이 폴더와는 별개입니다. 키 프리셋을 파일로
  내보내기/가져오기 하는 기능은 아직 없습니다.

---

## 기술 스택 및 DLL 참조

### 프로젝트 설정

- **타겟 프레임워크**: .NET Framework 4.7.2
- **프로젝트 파일**: `sxtg2-mod/sxtg2.csproj`

### 어셈블리 어트리뷰트

```csharp
[assembly: MelonInfo(typeof(sxtg2.Main), "sxtg2", "1.0.0", "Meowzter")]
[assembly: MelonGame("Lyrebird Studio", "Sixtar Gate STARTRAIL")]
[assembly: MelonColor(128, 0, 255, 255)] // Purple (R, G, B, A)
```

**위치**: `sxtg2-mod/Main/` (엔트리: `Main.cs`)

### DLL 참조

#### MelonLoader 관련
- **MelonLoader.dll**: `{게임 설치 폴더}\MelonLoader\net35\MelonLoader.dll`
- **0Harmony.dll**: `{게임 설치 폴더}\MelonLoader\net35\0Harmony.dll`

#### Unity Engine 관련
모든 Unity DLL은 `{게임 설치 폴더}\Sixtar Gate STARTRAIL_Data\Managed\` 경로에 있습니다.

- **UnityEngine.dll**: Unity 엔진 핵심
- **UnityEngine.CoreModule.dll**: Unity 코어 모듈
- **UnityEngine.AudioModule.dll**: 오디오 모듈 (BGM 교체)
- **UnityEngine.VideoModule.dll**: 비디오 모듈 (BGA 교체)
- **UnityEngine.UnityWebRequestModule.dll**: 웹 요청 모듈
- **UnityEngine.UnityWebRequestAudioModule.dll**: 오디오 웹 요청 모듈 (BGM 스트리밍)
- **UnityEngine.UnityWebRequestTextureModule.dll**: 텍스처 웹 요청 모듈 (썸네일 로드)
- **UnityEngine.InputLegacyModule.dll**: 입력 모듈 (ESC 키 감지)
- **UnityEngine.ImageConversionModule.dll**: 이미지 변환 모듈 (썸네일 로드)
- **UnityEngine.UI.dll**: Unity UI 모듈 (텍스트 후킹)

**프로젝트 파일 참조**: `sxtg2-mod/sxtg2.csproj`





