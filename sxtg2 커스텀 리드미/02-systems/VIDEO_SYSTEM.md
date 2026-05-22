# 🎬 비디오 시스템

**BGA 교체 및 비디오 처리 시스템**

---

## BGA 교체 흐름

```csharp
public class BGAPlayerHook
{
    private static bool _bgaReplaced = false;

    public static void ReplacePlaySceneBGA(string customAlbumFolder)
    {
        if (_bgaReplaced) return;

        // 1. VideoPlayer 찾기
        var videoPlayers = UnityEngine.Object.FindObjectsOfType<VideoPlayer>();
        
        foreach (var player in videoPlayers)
        {
            // 2. BGA 파일 찾기
            var bgaPath = BgaFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false);
            
            if (bgaPath != null)
            {
                // 3. URL 설정 및 재생
                player.url = "file://" + bgaPath;
                player.Play();
                
                _bgaReplaced = true;
                MelonLogger.Msg($"BGA 교체 완료: {Path.GetFileName(bgaPath)}");
                break;
            }
        }
    }
}
```

현재 구현은 아래 파일로 책임이 나뉩니다.

- `BGAPlayerHook.cs`: BGA 교체 상태와 `VideoPlayer` 설정
- `BgaFileResolver.cs`: 커스텀 앨범 폴더에서 `*.mp4` 선택
- `BgaVideoPlayerFinder.cs`: 씬에서 교체 대상 `VideoPlayer` 탐색
- `InitialMediaFileScanner.cs`: 초기 hwa 폴더의 첫 BGA 스캔

---

## BGA 파일 찾기

```csharp
private static string ResolveBgaFile()
{
    var hwaPath = Path.Combine(Application.dataPath, "..", "hwa");
    
    // 1. 앨범 폴더에서 찾기
    var albumPath = FindAlbumFolder();
    if (albumPath != null)
    {
        var bga = FindBgaInFolder(albumPath);
        if (bga != null) return bga;
    }
    
    // 2. hwa 루트에서 찾기
    return FindBgaInFolder(hwaPath);
}

private static string FindBgaInFolder(string folder)
{
    // MP4 파일 찾기
    var mp4Files = Directory.GetFiles(folder, "*.mp4", SearchOption.TopDirectoryOnly);
    
    if (mp4Files.Length > 0)
    {
        // video.mp4 우선
        var videoFile = mp4Files.FirstOrDefault(f => 
            Path.GetFileNameWithoutExtension(f).Equals("video", StringComparison.OrdinalIgnoreCase)
        );
        
        return videoFile ?? mp4Files[0];
    }
    
    return null;
}
```

플레이 중 교체는 `BgaFileResolver.FindForAlbum(customAlbumFolder, allowRootFallback: false)`를 사용합니다. 따라서 현재 커스텀 트랙의 앨범 폴더에 있는 mp4만 사용하고, 일반 트랙까지 루트 파일로 덮어쓰는 상황을 피합니다.

---

## VideoPlayer 설정

```csharp
public static void ConfigureVideoPlayer(VideoPlayer player, string videoPath)
{
    // 소스 설정
    player.source = VideoSource.Url;
    player.url = "file://" + videoPath;
    
    // 재생 설정
    player.isLooping = false;
    player.playOnAwake = false;
    player.skipOnDrop = true;
    
    // 오디오 설정 (BGA는 무음)
    player.audioOutputMode = VideoAudioOutputMode.None;
    
    // 렌더 모드
    player.renderMode = VideoRenderMode.MaterialOverride;
    
    // 준비 완료 이벤트
    player.prepareCompleted += OnVideoPrepared;
    
    // 준비 시작
    player.Prepare();
}

private static void OnVideoPrepared(VideoPlayer player)
{
    MelonLogger.Msg("비디오 준비 완료");
    player.Play();
}
```

실제 교체 시에는 기존 재생을 멈춘 뒤 `source = VideoSource.Url`, `url = file://...`, `clip = null`, `audioOutputMode = None`, `skipOnDrop = true`를 설정하고 `Prepare()`를 시도합니다. `Prepare()` 실패는 경고만 남기고 흐름을 계속 진행합니다.

---

## 비디오 동기화

```csharp
public class BGABGMSyncHook
{
    private static float _lastSyncTime = 0f;
    private const float SYNC_INTERVAL = SYNC_INTERVAL_SECONDS;
    private const float SYNC_THRESHOLD = HARD_SYNC_THRESHOLD_SECONDS;

    public static void CheckAndSync()
    {
        if (Time.time - _lastSyncTime < SYNC_INTERVAL)
            return;
        
        _lastSyncTime = Time.time;
        
        var videoPlayer = UnityEngine.Object.FindObjectOfType<VideoPlayer>();
        var audioSource = FindBgmAudioSource();
        
        if (videoPlayer == null || audioSource == null)
            return;
        
        if (!videoPlayer.isPlaying || !audioSource.isPlaying)
            return;
        
        // 시간 차이 계산
        var videoTime = (float)videoPlayer.time;
        var audioTime = audioSource.time;
        var diff = Mathf.Abs(videoTime - audioTime);
        
        if (diff > SYNC_THRESHOLD)
        {
            // BGA 시간을 BGM에 맞춤
            videoPlayer.time = audioTime;
            MelonLogger.Msg($"BGA/BGM 동기화: 차이 {diff:F3}초");
        }
    }
}
```

---

## Soft Sync (부드러운 동기화)

```csharp
public static void SoftSync(VideoPlayer video, AudioSource audio)
{
    var videoTime = (float)video.time;
    var audioTime = audio.time;
    var diff = audioTime - videoTime;
    
    if (Mathf.Abs(diff) < SOFT_SYNC_IGNORE_THRESHOLD)
    {
        // 무시
        return;
    }
    else if (Mathf.Abs(diff) < HARD_SYNC_THRESHOLD_SECONDS)
    {
        // Soft Sync: 재생 속도 조절
        var speedAdjust = NORMAL_PLAYBACK_SPEED + (diff * SOFT_SYNC_ADJUSTMENT_FACTOR);
        video.playbackSpeed = Mathf.Clamp(speedAdjust, MIN_SOFT_SYNC_SPEED, MAX_SOFT_SYNC_SPEED);
        
        MelonLogger.Msg($"Soft Sync: 속도 {speedAdjust:F3}");
    }
    else
    {
        // Hard Sync: 시간 점프
        video.time = audioTime;
        video.playbackSpeed = NORMAL_PLAYBACK_SPEED;
        
        MelonLogger.Msg($"Hard Sync: {diff:F3}초 점프");
    }
}
```

---

## 비디오 이벤트 처리

```csharp
public static void SetupVideoEvents(VideoPlayer player)
{
    // 준비 완료
    player.prepareCompleted += (vp) =>
    {
        MelonLogger.Msg("비디오 준비 완료");
    };
    
    // 재생 시작
    player.started += (vp) =>
    {
        MelonLogger.Msg("비디오 재생 시작");
    };
    
    // 재생 종료
    player.loopPointReached += (vp) =>
    {
        MelonLogger.Msg("비디오 재생 종료");
    };
    
    // 에러
    player.errorReceived += (vp, message) =>
    {
        MelonLogger.Error($"비디오 에러: {message}");
    };
}
```

---

## 비디오 품질 설정

```csharp
public static void SetVideoQuality(VideoPlayer player, VideoQuality quality)
{
    switch (quality)
    {
        case VideoQuality.Low:
            player.targetTexture = CreateRenderTexture(640, 360);
            break;
        
        case VideoQuality.Medium:
            player.targetTexture = CreateRenderTexture(1280, 720);
            break;
        
        case VideoQuality.High:
            player.targetTexture = CreateRenderTexture(1920, 1080);
            break;
    }
}

private static RenderTexture CreateRenderTexture(int width, int height)
{
    return new RenderTexture(width, height, 0)
    {
        antiAliasing = 1,
        filterMode = FilterMode.Bilinear
    };
}
```

---

## 비디오 캐싱

```csharp
public class VideoCache
{
    private static Dictionary<string, VideoClip> _cache = new Dictionary<string, VideoClip>();
    
    public static VideoClip GetClip(string path)
    {
        if (_cache.ContainsKey(path))
            return _cache[path];
        
        // 비디오는 일반적으로 캐싱하지 않음 (용량 큼)
        return null;
    }
    
    public static void Clear()
    {
        foreach (var clip in _cache.Values)
        {
            if (clip != null)
                UnityEngine.Object.Destroy(clip);
        }
        _cache.Clear();
    }
}
```

---

## 비디오 프레임 제어

```csharp
public static void SeekToFrame(VideoPlayer player, long frameNumber)
{
    if (player.canSetTime)
    {
        player.frame = frameNumber;
        MelonLogger.Msg($"프레임 이동: {frameNumber}");
    }
}

public static void SeekToTime(VideoPlayer player, double seconds)
{
    if (player.canSetTime)
    {
        player.time = seconds;
        MelonLogger.Msg($"시간 이동: {seconds:F3}초");
    }
}
```

---

## 비디오 정보 로깅

```csharp
public static void LogVideoInfo(VideoPlayer player)
{
    MelonLogger.Msg("=== VideoPlayer 정보 ===");
    MelonLogger.Msg($"URL: {player.url}");
    MelonLogger.Msg($"길이: {player.length:F2}초");
    MelonLogger.Msg($"프레임 수: {player.frameCount}");
    MelonLogger.Msg($"FPS: {player.frameRate}");
    MelonLogger.Msg($"해상도: {player.width}x{player.height}");
    MelonLogger.Msg($"재생 중: {player.isPlaying}");
    MelonLogger.Msg($"준비됨: {player.isPrepared}");
}
```

---

## 메모리 최적화

```csharp
public static void OptimizeVideoMemory(VideoPlayer player)
{
    // 텍스처 크기 제한
    if (player.targetTexture != null)
    {
        var maxSize = 1920;
        if (player.targetTexture.width > maxSize || player.targetTexture.height > maxSize)
        {
            var aspect = (float)player.targetTexture.width / player.targetTexture.height;
            var newWidth = maxSize;
            var newHeight = (int)(maxSize / aspect);
            
            player.targetTexture = CreateRenderTexture(newWidth, newHeight);
            MelonLogger.Msg($"비디오 텍스처 크기 조정: {newWidth}x{newHeight}");
        }
    }
}
```

---

## 비디오 재생 제어

```csharp
public class VideoController
{
    private VideoPlayer _player;
    
    public void Play()
    {
        if (_player != null && _player.isPrepared)
        {
            _player.Play();
        }
    }
    
    public void Pause()
    {
        if (_player != null && _player.isPlaying)
        {
            _player.Pause();
        }
    }
    
    public void Stop()
    {
        if (_player != null)
        {
            _player.Stop();
        }
    }
    
    public void SetSpeed(float speed)
    {
        if (_player != null)
        {
            _player.playbackSpeed = Mathf.Clamp(speed, MIN_ALLOWED_SPEED, MAX_ALLOWED_SPEED);
        }
    }
}
