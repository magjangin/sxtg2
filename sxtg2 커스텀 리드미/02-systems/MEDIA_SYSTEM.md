# 미디어 시스템

기준일: 2026-05-15

## 현재 역할

커스텀 앨범 폴더의 BGA/BGM/preview 파일을 찾아 게임의 재생 대상을 교체합니다.

## BGA

관련 파일:

- `Hooks/Audio/BGAPlayerHook.cs`
- `Hooks/Audio/BgaFileResolver.cs`
- `Hooks/Audio/BgaVideoPlayerFinder.cs`

흐름:

```text
CustomPlayStartupFlow.ReplaceMedia
  -> BGAPlayerHook.ReplacePlaySceneBGA(albumFolder)
  -> BgaFileResolver.FindForAlbum
  -> BgaVideoPlayerFinder.Find
  -> VideoPlayer.url = file://...
```

지원 파일:

- `*.mp4`

## BGM

관련 파일:

- `Hooks/Audio/BGMPlayerHook.cs`
- `Hooks/Audio/BGMPlayerHook.Playback.cs`
- `Hooks/Audio/BgmFileResolver.cs`
- `Hooks/Audio/BgmAudioSourceFinder.cs`

흐름:

```text
CustomPlayStartupFlow.ReplaceMedia
  -> BGMPlayerHook.ReplacePlaySceneBGM(albumFolder)
  -> BgmFileResolver.FindForAlbum
  -> BgmAudioSourceFinder.Find
  -> UnityWebRequestMultimedia.GetAudioClip
  -> AudioSource.clip 교체
```

지원 파일 우선순위:

1. `*.ogg`
2. `*.mp3`
3. `*.wav`

## Preview/Demo

관련 파일:

- `Hooks/Manager/ManagerMusicSelectHook.Preview.cs`
- `Hooks/Manager/ManagerMusicSelectHook.Preview.FileResolver.cs`
- `Hooks/Manager/ManagerMusicSelectHook.Preview.Audio.cs`
- `Hooks/Manager/ManagerMusicSelectHook.Preview.AudioSources.cs`
- `Hooks/Manager/ManagerMusicSelectHook.Demo.*`

커스텀 트랙의 preview 호출을 가로채고 원래 preview를 차단한 뒤, 앨범 폴더의 preview/music 파일을 재생합니다.

## 동기화

관련 파일:

- `Hooks/Audio/BGABGMSyncHook.cs`

`Main.OnUpdate`에서 주기적으로 BGA/BGM 시간 차이를 확인하고 soft/hard 보정을 수행합니다.

## 제거된 구조

`InitialMediaFileScanner`는 제거되었습니다. 현재는 초기화 때 전체 미디어 파일을 스캔하지 않고, 실제 교체 시점에 필요한 앨범 폴더에서 파일을 찾습니다.
