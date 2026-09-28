# BMS 선택 규칙 (커스텀 트랙 등록)

기준일: 2026-09-28 (v1.1.0)

## 현재 담당 코드

`Features/MusicSelectFeature.cs`의 `TrackDataAnalyzer` (호출: `ManagerMusicSelectHook.AwakePostfix`)

예전의 `BmsFileResolver`/`TextHook` 기반 트랙ID 매칭은 없어졌습니다. 지금은 **곡 선택 화면이 열릴 때 폴더마다
BMS 하나를 골라 트랙으로 등록**하고, 플레이할 때는 그 트랙에 저장된 경로(`CustomTrackData.BmsPath`)를 그대로 읽습니다.

## 등록 흐름

```text
ManagerMusicSelect.Awake (원본)
  -> ManagerMusicSelectHook.AwakePostfix
     -> TrackDataAnalyzer.InjectCustomTracks(__instance.trackDatas)
        ├─ trackDatas가 비었으면 경고 후 종료
        ├─ 이미 CustomTrackData가 들어 있으면 종료 (중복 주입 방지)
        ├─ {게임 폴더}\hwa 가 없으면 종료
        ├─ donor = trackDatas[0]   // 첫 번째 원본 곡
        └─ EnumerateAlbumFolders(hwa) 의 각 폴더마다
             ├─ bmsPath = FindFirstBms(폴더)       // 없으면 건너뜀
             ├─ TrackInfoParser.ParseTrackInfo(폴더, BMS 파일명)
             └─ trackDatas.Add(CreateCustomTrack(donor, info, 폴더, bmsPath))
```

곡 선택 씬에 들어갈 때마다 `Awake`가 다시 불리므로, 폴더를 추가/수정한 뒤 곡 선택 화면으로 돌아오면 반영됩니다.

## 앨범 폴더 후보 (`EnumerateAlbumFolders`)

1. `hwa` 루트 자체 — 루트에 BMS가 있을 때만
2. `hwa`의 1단계 하위 폴더 전부 — 이름순(대소문자 무시) 정렬

재귀 탐색은 하지 않습니다. `hwa\A\B\chart.bms`처럼 2단계 아래 BMS는 등록되지 않습니다.

## 폴더 안에서 BMS 고르기 (`FindFirstBms`)

`*.bms` → `*.bme` → `*.bml` 순서로 패턴을 바꿔 가며 찾고, **처음 발견된 파일 하나**만 씁니다.
같은 패턴 안에서의 순서는 `Directory.EnumerateFiles` 반환 순서(보통 NTFS 이름순)입니다.

> 한 폴더에 난이도별 BMS가 여러 개 있어도 하나만 등록됩니다. 난이도마다 다른 차트를 쓰려면 폴더를 나눠야 합니다.

## 트랙 정보 (`TrackInfoParser`)

### 파일 찾기

1. `{BMS 파일명(확장자 제외)}.txt`
2. `trackinfo.txt`
3. `info.txt`
4. 폴더의 첫 번째 `*.txt` (아무 txt나 읽으므로 `readme.txt` 같은 파일이 있으면 의도치 않게 읽힐 수 있음)

### 형식

`키: 값` 형태로 한 줄에 하나씩 씁니다. `#` 또는 `//`로 시작하는 줄은 주석입니다. 파일은 UTF-8로 읽습니다.

```text
제목: My Custom Song
아티스트: Someone
난이도: 3, 7, 11, 14
```

| 항목 | 인식하는 키 (대소문자 무시) |
| --- | --- |
| 제목 | `제목`(정확히 일치), 또는 키에 `곡 제목`/`title` 포함 |
| 아티스트 | `작곡가`(정확히 일치), 또는 키에 `아티스트`/`artist` 포함 |
| 난이도 | 키에 `난이도`/`difficulty`/`level` 포함. 값은 쉼표/공백/탭으로 구분한 정수 목록 |

키를 "포함" 여부로 비교하므로 `subtitle:`처럼 `title`이 들어간 다른 키가 뒤에 있으면 제목을 덮어씁니다.

## 만들어지는 트랙 (`CreateCustomTrack`)

| 필드 | 값 |
| --- | --- |
| `ID` | `CUSTOM_` + (앨범 폴더명/BMS 파일명을 대문자로 바꾼 문자열의 FNV-1a 64비트 해시) — 폴더/파일 이름이 같으면 항상 같은 ID |
| `DisplayName`, `AlphabetName` | 트랙 정보의 제목, 없으면 `커스텀 차트` |
| `Composer`, `ComposerEN` | 트랙 정보의 아티스트, 없으면 도너 곡 작곡가 |
| `Level`, `Level_LITE` | 난이도 4칸. 난이도 목록이 있으면 순서대로 채우고 모자라면 마지막 값을 반복(`D2` 두 자리). 없으면 도너 값, 그것도 없으면 `00` |
| `visibilityOnList` | `true` |
| `BGAOffset` | `0` |
| `artistInfoDic` | 도너 값 복사 |
| `AlbumFolder`, `BmsPath`, `ResourceDonor` | `CustomTrackData` 전용 속성 |

난이도(`Level`)는 곡 선택 화면 표시용일 뿐이고, 어떤 난이도를 골라도 같은 BMS가 재생됩니다.
판정 범위는 고른 난이도에 따라 게임이 정합니다(`PLAY_OVERLAY.md`의 판정 범위 표 참고).

## 플레이 시점

`ManagerPlayHook.FetchBMSToModulesPrefix`가 `CustomTrackData.BmsPath`를 `BmsParser.ParseBmsFileWithStatistics`로
읽습니다. 파서는 파일 경로+수정 시각으로 결과를 캐시하므로, BMS를 수정하고 저장하면 다음 플레이에서 새로 파싱됩니다.
파싱 규칙은 `BMS_FORMAT.md`/`BMS_PARSING.md` 참고.
