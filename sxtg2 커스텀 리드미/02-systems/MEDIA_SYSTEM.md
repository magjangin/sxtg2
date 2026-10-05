# 미디어 시스템

기준일: 2026-09-28 (v1.1.0)

## 현재 역할

커스텀 앨범 폴더의 BGM/BGA/미리듣기/자켓 파일을 찾아 게임의 재생 대상을 교체하고, BGA를 BGM에 맞춰 동기화합니다.

| 기능 | 코드 | 호출 시점 |
| --- | --- | --- |
| BGM 교체 | `Hooks/AudioHooks.cs` `BgmFileResolver`, `BGMPlayerHook` | `ManagerPlayHook.FetchBMSToModulesPrefix` |
| BGA 교체 | `Hooks/AudioHooks.cs` `BgaFileResolver`, `BGAPlayerHook` | 같은 곳 (게임 BGA 설정이 ON일 때만) |
| BGA↔BGM 동기화 | `Hooks/AudioHooks.cs` `BGABGMSyncHook` | `Main.OnUpdate` 매 프레임 |
| 미리듣기 | `Features/MusicSelectFeature.cs` `ManagerMusicSelectHook.PlayPreviewPrefix` | 곡 선택 화면에서 게임이 `PlayPreview` 호출 시 |
| 자켓/썸네일 | `Helpers/ModHelpers.cs` `ThumbnailLoader`, `Hooks/GameplayHooks.cs` `TrackDataMediaHook` | 게임이 `TrackData.GetJacketSprite()`/`GetThumbSprite()` 호출 시 |

모든 탐색은 **앨범 폴더 한 곳의 최상위만** 봅니다(하위 폴더·`hwa` 루트 폴백 없음). 초기화 때 미리 스캔하지 않고
실제로 필요할 때 찾습니다.

---

## BGM

### 파일 선택 (`BgmFileResolver.FindForAlbum`)

1. `music.ogg` → `music.mp3` → `music.wav`
2. 없으면 폴더에서 **가장 큰** `.ogg`/`.mp3`/`.wav` 파일을 쓰고 경고를 남깁니다(곡 음원은 키음보다 훨씬 크다는 가정. 예전에는 첫 번째 `*.ogg`를
   골라서 키음이 많은 폴더에서는 키음 하나가 BGM이 됐음).

그래도 엉뚱한 파일이 골라질 수 있으니 곡 전체 음원은 `music.*`로 두세요.

### 로드와 교체 (`BGMPlayerHook`)

```text
ReplacePlaySceneBGM(ManagerPlay.bgm, albumFolder)
  ├─ 요청 버전(_requestVersion) 증가, _isLoading = true
  └─ 전용 코루틴 러너(DontDestroyOnLoad GameObject "sxtg2 BGM Loader")에서 LoadAudio 실행
       ├─ UnityWebRequestMultimedia.GetAudioClip(file:// URL, AudioType)
       │    - URL은 MediaUrl.FromPath: `\`→`/`, 그리고 `%`, `#`, `?`만 이스케이프(그 밖의 글자는 그대로)
       │    - 확장자별 AudioType: .ogg=OGGVORBIS, .mp3=MPEG, .wav=WAV
       │    - 5MB 초과일 때만 streamAudio = true (.wav도 5MB 이하면 통째로 로드)
       ├─ 요청 버전이 바뀌었으면(다음 플레이가 시작됨) 결과 버림
       └─ 성공 시 bgm.Stop() → bgm.clip 교체, loop = false
  finally: _isLoading = false (같은 버전일 때만)
```

로드 중에는 `ManagerPlayHook.CheckBGMStartPrefix`가 원본 `CheckBGMStart`를 막아 BGM 시작을 보류합니다. 다만 게임의 곡 시계(`CurTime`)는 계속
흐르므로(플레이 시작 지연 `startDelay` 2.5초 뒤 0초), 로드가 그 지연보다 오래 걸리면 BGM이 그만큼 늦게 시작해 차트와 어긋납니다. 실제 로그에서는
교체가 주입 후 수십 ms 안에 끝납니다. 로드에 실패하면 경고를 남기고 **도너 트랙의 BGM**이 그대로 재생됩니다.
BGM/BGA의 시작 시각(`trackStartTiming`)은 모드가 0초로 고정합니다(원래는 도너 패턴의 값).
## BGA

### 파일 선택 (`BgaFileResolver.FindForAlbum`)

- 앨범 폴더에서 이름순으로 첫 번째 `*.mp4` (다른 확장자는 찾지 않음). `Directory.GetFiles`의 순서는 보장되지 않아 이름순으로 정렬해 항상 같은 파일이 고릅니다.

### 적용 조건과 동작 (`BGAPlayerHook`)

- 게임 설정의 BGA 모드(`UserAccountModule.Instance.userData.bgaMode`)가 `ON`일 때만 시도합니다.
- 성공하면 `VideoPlayer.url = file:// URL(MediaUrl.FromPath)`, `source = Url`, `clip = null`, 오디오 출력 없음,
  `skipOnDrop = true`로 설정하고 `Prepare()`를 호출합니다. `ManagerPlayHook`이 BGA 오브젝트를 켜고 기본 BGA 캔버스를 끕니다.
- 대상 `VideoPlayer`는 `ManagerPlay`의 private 필드 `bgaPlayer`를 Harmony 필드 주입(`___bgaPlayer`)으로 직접 받습니다.
- 경로에 `%`, `#`, `?`가 있어도 읽히도록 URL에서 그 세 글자만 이스케이프합니다(예전에는 URL 해석이 깨졌음).
## BGA↔BGM 동기화 (`BGABGMSyncHook`)

현재 교체된 `VideoPlayer`/`AudioSource`가 둘 다 있을 때만 동작합니다.

| 상황 | 동작 |
| --- | --- |
| BGM 재생 중, BGA 멈춤 | BGA 시간을 BGM에 맞추고 재생 |
| BGM 멈춤(일시정지 등), BGA 재생 중 | BGA 일시정지 |
| 차이 > 0.5초 | **하드 싱크**: `videoPlayer.time = bgmTime`, 속도 1.0 |
| 차이 > 0.05초 (보정 중이면 0.01초) | **소프트 싱크**: 속도를 ±2% (차이 > 0.1초면 ±5%)로 조정 |
| 그 이하 | 속도 1.0으로 복귀 |

비교는 0.1초 간격으로 하며, 두 시간 중 하나라도 0 이하이면 건너뜁니다. 로그는 `LogLevel=2`(상세)일 때만 남습니다.

**BGA 영상이 곡보다 짧을 때**: `BGM 위치 >= 영상 길이`(영상이 준비됐고 길이를 알 때)이면 동기화 훅이 아무것도 하지 않습니다. 영상은 끝난 상태(마지막
프레임)로 남고, 매 프레임 재생을 다시 시도하지도 않습니다(예전에는 "BGM은 재생 중인데 영상은 멈춤"으로 보고 매 프레임 `time = BGM 시간; Play()`를 불렀음).
## 미리듣기 (Preview)

### 파일 선택 (`BgmFileResolver.FindPreviewForAlbum`)

1. `demo.ogg` → `demo.mp3` → `demo.wav`
2. `music.ogg` → `music.mp3` → `music.wav`
3. 폴더에서 가장 큰 `.ogg`/`.mp3`/`.wav` (BGM과 같은 방식)

### 동작 (`ManagerMusicSelectHook.PlayPreviewPrefix`)

- 커스텀 트랙이면 원본 미리듣기/정지 코루틴을 멈추고 `previewSource`, `bgmSource`를 정지한 뒤,
  파일을 `UnityWebRequest`로 읽어 `previewSource`에서 재생합니다(볼륨 `Util.GetGameplayVolume()`).
- 클립 길이만큼 재생한 뒤 메뉴 BGM(`bgmSource`)을 다시 켭니다. 파일이 없거나 로드에 실패해도 메뉴 BGM을 복구합니다.
- 요청 버전 번호로, 곡을 빨리 넘겨도 늦게 도착한 이전 곡 미리듣기가 재생되지 않게 막습니다.
- 원본과 달리 **구간 반복이 아니라 파일 전체**를 재생합니다. `demo.*`가 없으면 곡 전체가 미리듣기로 나옵니다.
- 새 미리듣기 클립이 만들어지면 **이전 커스텀 클립을 해제**합니다(`Object.Destroy`). 곡을 넘길 때마다 한 곡 분량의 오디오가 쌓이지 않습니다.
## 자켓 / 썸네일

`TrackDataMediaHook`이 `GetJacketSprite()`와 `GetThumbSprite()`를 가로채 `ThumbnailLoader.LoadThumbnail`의 결과를
돌려줍니다. 결과는 `CustomTrackData.CustomJacket`에 캐시됩니다. 자켓 파일이 **없는 곡도 한 번 찾아봤다는 사실**(`JacketSearched`)을 기억해서, 게임이 자켓을 요청할 때마다
파일을 다시 확인하거나 경고를 반복하지 않습니다(경고는 곡마다 한 번).

탐색 순서 (앨범 폴더 안, 대소문자 무시는 파일 시스템에 따름):

1. `thumb.png`, `thumbnail.png`, `jacket.png`, `cover.png`, `image.png`
2. `{트랙ID}_thumb.png`, `{트랙ID}_thumbnail.png`, `{트랙ID}_jacket.png`, `{트랙ID}.png`

트랙 ID는 `CUSTOM_` + 해시값이라 2번 이름은 사실상 쓰기 어렵습니다. 1번 이름을 쓰세요.
일시정지 창·로딩 화면·결과 화면도 게임이 같은 메서드로 자켓을 요청하므로 별도 처리 없이 커스텀 자켓이 나옵니다.

> **메모리 (보류, 낮음)**: `ThumbnailLoader.LoadSprite`는 PNG를 **원본 해상도 그대로** `Texture2D`(RGBA32, 밉맵 켬)로 읽고,
> 곡 목록 썸네일(`GetThumbSprite`)에도 같은 스프라이트를 씁니다. 압축이나 축소를 하지 않으므로 앨범이 많고 자켓이 클수록 메모리를
> 많이 씁니다(예: 2048×2048이면 한 장에 약 16MB, 밉맵 포함 약 21MB). 필요하면 자켓을 512~1024px 정도로 줄여서 넣으세요.
> DXT 압축은 크기가 4의 배수여야 해서(1952×1098 같은 자켓은 불가) 코드로 고치지 않았습니다(`CURRENT_STATUS.md`의 남은 문제).
## 도너 트랙 리소스

커스텀 트랙에는 게임 리소스가 없으므로 다음은 도너 트랙(곡 목록의 첫 번째 원본 곡) 것을 씁니다.

- `GetAudioClip()`: 게임이 처음 세팅하는 BGM (커스텀 BGM 로드 전/실패 시 들리는 소리)
- `GetLoadingAnimation()`: 로딩 화면 영상
- `GetSixtarPatternDirectory(...)`: 게임이 먼저 읽는 패턴 파일 (`FetchBMSToModules` 시점에 커스텀 노트로 교체됨)
