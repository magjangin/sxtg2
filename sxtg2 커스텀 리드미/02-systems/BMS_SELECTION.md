# BMS 선택 규칙

기준일: 2026-05-15

## 현재 담당 파일

`Helpers/Track/BmsFileResolver.cs`

이 파일이 BMS 확장자와 파일 선택 규칙을 공통 관리합니다.

## 지원 확장자

- `*.bms`
- `*.bme`
- `*.bml`

## 초기 스캔

초기화 시 `Main.BmsBootstrap`이 다음 흐름으로 BMS 파일을 스캔합니다.

```text
Main.ScanAndParseBmsFiles
  -> BmsFileResolver.FindRootAndAlbumBmsFiles
  -> BmsParser.ParseBmsFileWithStatistics
  -> 첫 번째 성공 결과를 CustomChartInjector.SetParsedBmsNotes에 설정
```

초기 스캔은 통계 출력과 기본 차트 설정 용도입니다. 실제 플레이 직전에는 선택 트랙 기준으로 다시 로드될 수 있습니다.

## 플레이 직전 선택

```text
TextHook.LoadAndInjectBmsForTrack
  -> BmsFileResolver.FindForTrack(trackId, hwaFolder, albumFolder)
  -> BmsParser.ParseBmsFile
  -> CustomChartInjector.SetParsedBmsNotes
```

## 선택 우선순위

`albumFolder`가 있으면:

1. 앨범 폴더 안의 `{trackId}.bms/.bme/.bml`
2. 앨범 폴더 안의 첫 번째 BMS/BME/BML

그 다음 fallback:

3. `hwa` 전체 하위 폴더에서 `{trackId}.bms/.bme/.bml`
4. `hwa`의 앨범 폴더 중 첫 번째 BMS/BME/BML
5. `hwa` 루트의 첫 번째 BMS/BME/BML

## TrackData 주입에서의 사용

`TrackDataAnalyzer`는 앨범 폴더 후보를 `BmsFileResolver.GetAlbumFoldersOrRootWithBms`로 얻고, 각 폴더의 BMS 목록은 `BmsFileResolver.FindInFolder`로 가져옵니다.

## 주의

동일 폴더에 BMS 파일이 여러 개 있으면 현재는 첫 번째 파일을 사용합니다. 여러 난이도/패턴 선택 UI는 아직 별도 구현이 아닙니다.
