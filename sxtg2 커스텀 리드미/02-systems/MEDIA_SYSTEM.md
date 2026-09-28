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
2. 없으면 폴더의 첫 번째 `*.ogg` → 첫 번째 `*.mp3` → 첫 번째 `*.wav`

> **주의 (알려진 문제)**: 2번 폴백은 폴더에 있는 아무 오디오나 고릅니다. 키음 방식 BMS 폴더처럼 짧은 `.ogg`/`.wav`가
> 많으면 키음 하나가 BGM으로 재생될 수 있습니다. 곡 전체 음원은 반드시 `music.*`로 두세요.

### 로드와 교체 (`BGMPlayerHook`)

```text
ReplacePlaySceneBGM(ManagerPlay.bgm, albumFolder)
  ├─ 요청 버전(_requestVersion) 증가, _isLoading = true
  └─ 전용 코루틴 러너(DontDestroyOnLoad GameObject "sxtg2 BGM Loader")에서 LoadAudio 실행
       ├─ UnityWebRequestMultimedia.GetAudioClip("file://" + 경로, AudioType)
       │    - 확장자별 AudioType: .ogg=OGGVORBIS, .mp3=MPEG, .wav=WAV
       │    - .wav 이거나 5MB 초과면 streamAudio = true
       ├─ 요청 버전이 바뀌었으면(다음 플레이가 시작됨) 결과 버림
       └─ 성공 시 bgm.Stop() → bgm.clip 교체, loop = false
  finally: _isLoading = false (같은 버전일 때만)
```

로드 중에는 `ManagerPlayHook.CheckBGMStartPrefix`가 원본 `CheckBGMStart`를 막아 BGM이 늦게 시작되더라도
노트와 어긋나지 않게 합니다. 로드에 실패하면 경고를 남기고 **도너 트랙의 BGM**이 그대로 재생됩니다.

## BGA

### 파일 선택 (`BgaFileResolver.FindForAlbum`)

- 앨범 폴더의 첫 번째 `*.mp4` (다른 확장자는 찾지 않음)

### 적용 조건과 동작 (`BGAPlayerHook`)

- 게임 설정의 BGA 모드(`UserAccountModule.Instance.userData.bgaMode`)가 `ON`일 때만 시도합니다.
- 성공하면 `VideoPlayer.url = "file://" + 경로`, `source = Url`, `clip = null`, 오디오 출력 없음,
  `skipOnDrop = true`로 설정하고 `Prepare()`를 호출합니다. `ManagerPlayHook`이 BGA 오브젝트를 켜고 기본 BGA 캔버스를 끕니다.
- 대상 `VideoPlayer`는 `ManagerPlay`의 private 필드 `bgaPlayer`를 Harmony 필드 주입(`___bgaPlayer`)으로 직접 받습니다.

> 경로에 `#`, `%` 같은 URL 예약 문자가 있으면 `file://` URL 해석이 깨질 수 있습니다(URL 인코딩을 하지 않음).

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

## 미리듣기 (Preview)

### 파일 선택 (`BgmFileResolver.FindPreviewForAlbum`)

1. `demo.ogg` → `demo.mp3` → `demo.wav`
2. `music.ogg` → `music.mp3` → `music.wav`
3. 폴더의 첫 번째 `*.ogg` → `*.mp3` → `*.wav` (BGM과 같은 키음 주의)

### 동작 (`ManagerMusicSelectHook.PlayPreviewPrefix`)

- 커스텀 트랙이면 원본 미리듣기/정지 코루틴을 멈추고 `previewSource`, `bgmSource`를 정지한 뒤,
  파일을 `UnityWebRequest`로 읽어 `previewSource`에서 재생합니다(볼륨 `Util.GetGameplayVolume()`).
- 클립 길이만큼 재생한 뒤 메뉴 BGM(`bgmSource`)을 다시 켭니다. 파일이 없거나 로드에 실패해도 메뉴 BGM을 복구합니다.
- 요청 버전 번호로, 곡을 빨리 넘겨도 늦게 도착한 이전 곡 미리듣기가 재생되지 않게 막습니다.
- 원본과 달리 **구간 반복이 아니라 파일 전체**를 재생합니다. `demo.*`가 없으면 곡 전체가 미리듣기로 나옵니다.
- 로드한 클립은 명시적으로 해제하지 않습니다(씬 전환 시 Unity가 정리).

## 자켓 / 썸네일

`TrackDataMediaHook`이 `GetJacketSprite()`와 `GetThumbSprite()`를 가로채 `ThumbnailLoader.LoadThumbnail`의 결과를
돌려줍니다. 결과는 `CustomTrackData.CustomJacket`에 캐시됩니다.

탐색 순서 (앨범 폴더 안, 대소문자 무시는 파일 시스템에 따름):

1. `thumb.png`, `thumbnail.png`, `jacket.png`, `cover.png`, `image.png`
2. `{트랙ID}_thumb.png`, `{트랙ID}_thumbnail.png`, `{트랙ID}_jacket.png`, `{트랙ID}.png`

트랙 ID는 `CUSTOM_` + 해시값이라 2번 이름은 사실상 쓰기 어렵습니다. 1번 이름을 쓰세요.
일시정지 창·로딩 화면·결과 화면도 게임이 같은 메서드로 자켓을 요청하므로 별도 처리 없이 커스텀 자켓이 나옵니다.

> **알려진 문제**: 자켓 파일이 없으면 캐시할 값이 없어서, 게임이 자켓을 요청할 때마다 파일 9개를 다시 확인하고
> `[TrackDataMediaHook] 커스텀 자켓을 찾지 못해 기본 자켓을 사용합니다` 경고를 남깁니다. 자켓 PNG를 넣으면 사라집니다.

## 도너 트랙 리소스

커스텀 트랙에는 게임 리소스가 없으므로 다음은 도너 트랙(곡 목록의 첫 번째 원본 곡) 것을 씁니다.

- `GetAudioClip()`: 게임이 처음 세팅하는 BGM (커스텀 BGM 로드 전/실패 시 들리는 소리)
- `GetLoadingAnimation()`: 로딩 화면 영상
- `GetSixtarPatternDirectory(...)`: 게임이 먼저 읽는 패턴 파일 (`FetchBMSToModules` 시점에 커스텀 노트로 교체됨)
