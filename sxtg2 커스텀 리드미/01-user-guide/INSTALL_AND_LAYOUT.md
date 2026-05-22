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





