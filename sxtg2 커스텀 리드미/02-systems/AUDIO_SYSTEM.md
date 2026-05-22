# 🔊 오디오 시스템

**BGM 교체 및 오디오 처리 시스템**

---

## BGM 교체 흐름

```csharp
public class BGMPlayerHook
{
    private static bool _bgmReplaced = false;

    public static void ReplacePlaySceneBGM(string customAlbumFolder)
    {
        if (_bgmReplaced) return;

        // 1. AudioSource 찾기
        var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
        
        foreach (var source in audioSources)
        {
            if (IsBgmSource(source))
            {
                // 2. BGM 파일 찾기
                var bgmPath = BgmFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false);
                
                if (bgmPath != null)
                {
                    // 3. 비동기 로드 및 교체
                    MelonCoroutines.Start(LoadAndReplaceBGM(source, bgmPath));
                    _bgmReplaced = true;
                    break;
                }
            }
        }
    }
}
```

현재 구현은 한 파일에 모두 몰려 있지 않고 아래처럼 나뉩니다.

- `BGMPlayerHook.cs`: 교체 상태, `ManagerPlay.bgm` 캡처, 교체 시작
- `BGMPlayerHook.Playback.cs`: `UnityWebRequest` 기반 비동기 로드/재생
- `BgmFileResolver.cs`: 커스텀 앨범 폴더에서 `ogg → mp3 → wav` 순서로 파일 선택
- `BgmAudioSourceFinder.cs`: `ManagerPlay.bgm` 우선, 없으면 씬 AudioSource 탐색
- `InitialMediaFileScanner.cs`: 초기 hwa 폴더 미디어 스캔

---

## AudioSource 찾기

```csharp
private static bool IsBgmSource(AudioSource source)
{
    // 1. Clip 이름 확인
    if (source.clip != null && source.clip.name.Contains("BGM"))
        return true;
    
    // 2. GameObject 이름 확인
    if (source.gameObject.name.Contains("BGM"))
        return true;
    
    // 3. 볼륨 확인 (BGM은 보통 높은 볼륨)
    if (source.volume > BGM_VOLUME_THRESHOLD)
        return true;
    
    return false;
}
```

---

## BGM 파일 찾기

```csharp
private static string ResolveBgmFile()
{
    var hwaPath = Path.Combine(Application.dataPath, "..", "hwa");
    
    // 1. 앨범 폴더에서 찾기
    var albumPath = FindAlbumFolder();
    if (albumPath != null)
    {
        var bgm = FindBgmInFolder(albumPath);
        if (bgm != null) return bgm;
    }
    
    // 2. hwa 루트에서 찾기
    return FindBgmInFolder(hwaPath);
}

private static string FindBgmInFolder(string folder)
{
    // 우선순위: OGG → MP3 → WAV
    var extensions = new[] { "*.ogg", "*.mp3", "*.wav" };
    
    foreach (var ext in extensions)
    {
        var files = Directory.GetFiles(folder, ext, SearchOption.TopDirectoryOnly);
        if (files.Length > 0)
        {
            // music.* 우선
            var musicFile = files.FirstOrDefault(f => 
                Path.GetFileNameWithoutExtension(f).Equals("music", StringComparison.OrdinalIgnoreCase)
            );
            
            return musicFile ?? files[0];
        }
    }
    
    return null;
}
```

플레이 씬 교체 경로에서는 `BgmFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false)`를 사용합니다. 즉 커스텀 트랙으로 확정된 앨범 폴더 안의 BGM만 교체 대상으로 삼아 일반 트랙의 오디오를 잘못 바꾸지 않게 합니다.

---

## 비동기 로드

```csharp
private static IEnumerator LoadAndReplaceBGM(AudioSource source, string path)
{
    var uri = "file://" + path;
    var audioType = GetAudioType(path);
    
    using (var www = UnityWebRequestMultimedia.GetAudioClip(uri, audioType))
    {
        // 스트리밍 설정
        var handler = (DownloadHandlerAudioClip)www.downloadHandler;
        
        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > 5 * 1024 * 1024) // 5MB 이상
        {
            handler.streamAudio = true;
            MelonLogger.Msg("스트리밍 모드 활성화");
        }
        
        yield return www.SendWebRequest();
        
        if (www.result == UnityWebRequest.Result.Success)
        {
            var clip = handler.audioClip;
            
            // AudioSource 설정
            source.clip = clip;
            source.loop = false;
            source.Play();
            
            MelonLogger.Msg($"BGM 교체 완료: {Path.GetFileName(path)}");
        }
        else
        {
            MelonLogger.Error($"BGM 로드 실패: {www.error}");
        }
    }
}
```

---

## AudioType 결정

```csharp
private static AudioType GetAudioType(string path)
{
    var ext = Path.GetExtension(path).ToLower();
    
    switch (ext)
    {
        case ".ogg":
            return AudioType.OGGVORBIS;
        case ".mp3":
            return AudioType.MPEG;
        case ".wav":
            return AudioType.WAV;
        default:
            return AudioType.UNKNOWN;
    }
}
```

---

## 프리뷰 음악 재생

```csharp
public static void PlayCustomMusic(object managerInstance)
{
    // 1. BGM 소스 찾기 및 뮤트
    var bgmSource = FindBgmSourceForPreview();
    if (bgmSource != null)
    {
        bgmSource.mute = true;
        bgmSource.Stop();
    }
    
    // 2. 음악 파일 찾기
    var musicPath = FindMusicFile();
    if (musicPath == null)
    {
        MelonLogger.Warning("프리뷰 음악을 찾을 수 없음");
        return;
    }
    
    // 3. 재생
    MelonCoroutines.Start(LoadAndPlayMusicCoroutine(musicPath));
}

private static string FindMusicFile()
{
    var albumPath = FindAlbumFolder();
    if (albumPath == null) return null;
    
    // 1순위: demo.*
    var demoFiles = new[] { "demo.ogg", "demo.mp3", "demo.wav" };
    foreach (var file in demoFiles)
    {
        var path = Path.Combine(albumPath, file);
        if (File.Exists(path)) return path;
    }
    
    // 2순위: music.*
    var musicFiles = new[] { "music.ogg", "music.mp3", "music.wav" };
    foreach (var file in musicFiles)
    {
        var path = Path.Combine(albumPath, file);
        if (File.Exists(path)) return path;
    }
    
    return null;
}
```

Preview 관련 책임도 분리되어 있습니다.

- `ManagerMusicSelectHook.Preview.cs`: PlayPreview 후킹과 커스텀 트랙 판별
- `ManagerMusicSelectHook.Preview.FileResolver.cs`: `demo.*` 우선, 없으면 `music.*` 선택
- `ManagerMusicSelectHook.Preview.AudioSources.cs`: preview 대상 AudioSource 찾기 및 기존 소스 뮤트
- `ManagerMusicSelectHook.Preview.Audio.cs`: 비동기 로드 및 재생

---

## AudioSource 뮤트 처리

```csharp
private static void StopAndMuteAllAudioSources()
{
    var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
    
    foreach (var source in audioSources)
    {
        if (source.isPlaying)
        {
            source.mute = true;
            source.Stop();
            MelonLogger.Msg($"AudioSource 뮤트: {source.gameObject.name}");
        }
    }
}
```

---

## 사운드 교체 (클리어 사운드)

```csharp
public class HighscoreMeterHook
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(SoundObject), "Play")]
    private static bool PlayPrefix(SoundObject __instance)
    {
        var clipName = __instance.clip?.name;
        
        if (clipName != null && clipName.StartsWith("clear", StringComparison.OrdinalIgnoreCase))
        {
            // clear* 사운드를 KeyBlue_Tam으로 교체
            var newClip = FindAudioClip("KeyBlue_Tam");
            if (newClip != null)
            {
                __instance.clip = newClip;
                MelonLogger.Msg($"사운드 교체: {clipName} → KeyBlue_Tam");
            }
        }
        
        return true;
    }
    
    private static AudioClip FindAudioClip(string clipName)
    {
        var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        return allClips.FirstOrDefault(c => c.name == clipName);
    }
}
```

`SoundObject`의 clip 접근은 `SoundObjectClipAccessor.cs`로 분리되어 있고, 커스텀 차트 스코어 제한 해제 적용은 `HighscoreMeterHook.Score.cs`가 담당합니다.

---

## 오디오 클립 캐싱

```csharp
public class AudioClipCache
{
    private static Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
    
    public static AudioClip GetClip(string name)
    {
        if (_cache.ContainsKey(name))
            return _cache[name];
        
        var clip = Resources.FindObjectsOfTypeAll<AudioClip>()
            .FirstOrDefault(c => c.name == name);
        
        if (clip != null)
            _cache[name] = clip;
        
        return clip;
    }
    
    public static void Clear()
    {
        _cache.Clear();
    }
}
```

---

## 볼륨 조절

```csharp
public static void SetBgmVolume(float volume)
{
    var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
    
    foreach (var source in audioSources)
    {
        if (IsBgmSource(source))
        {
            source.volume = Mathf.Clamp01(volume);
            MelonLogger.Msg($"BGM 볼륨 설정: {volume}");
        }
    }
}
```

---

## 페이드 인/아웃

```csharp
private static IEnumerator FadeOut(AudioSource source, float duration)
{
    var startVolume = source.volume;
    var elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        source.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
        yield return null;
    }
    
    source.volume = 0f;
    source.Stop();
}

private static IEnumerator FadeIn(AudioSource source, float targetVolume, float duration)
{
    source.volume = 0f;
    source.Play();
    
    var elapsed = 0f;
    
    while (elapsed < duration)
    {
        elapsed += Time.deltaTime;
        source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
        yield return null;
    }
    
    source.volume = targetVolume;
}
```

---

## 메모리 관리

```csharp
public static void UnloadUnusedAudioClips()
{
    var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
    
    foreach (var clip in allClips)
    {
        if (!IsClipInUse(clip))
        {
            Resources.UnloadAsset(clip);
            MelonLogger.Msg($"AudioClip 언로드: {clip.name}");
        }
    }
}

private static bool IsClipInUse(AudioClip clip)
{
    var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
    return audioSources.Any(s => s.clip == clip);
}
