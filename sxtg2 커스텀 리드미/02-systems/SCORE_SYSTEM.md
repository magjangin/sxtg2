# 스코어 시스템

기준일: 2026-05-15

## 현재 역할

커스텀 차트 플레이 중 게임의 기본 스코어 제한과 일부 사운드 처리를 보정합니다.

## 현재 코드 경로

- `Hooks/Audio/HighscoreMeterHook.cs`
- `Hooks/Audio/HighscoreMeterHook.Score.cs`
- `Hooks/Audio/HighscoreMeterHook.SXGTReader.cs`
- `Hooks/Audio/HighscoreMeterHook.SoundObject.cs`
- `Hooks/Audio/HighscoreMeterHook.AudioClip.cs`
- `Hooks/SXGT/SXGTDataHook.Score.cs`

## 적용 시점

```text
CustomPlayStartupFlow.Run
  -> HighscoreMeterHook.ApplyForCustomChart
  -> FixSXGTReaderMaxScore

SXGTDataHook.ProcessPendingNoteRemovalAndInjection
  -> FixMaxScoreField
```

## 현재 보정 대상

- `SXGTReader.MaxScore`
- `SXGTData.maxScore`
- `SoundObject` clear 계열 사운드

## 제거된 보정

`ManagerPlay.targetBestScore` 보정은 제거되었습니다.

이 필드는 현재 게임 빌드에서 `Int32`로 확인되었고, 기존 코드가 `-1f`를 넣으면서 다음 경고를 만들었습니다.

```text
Object of type 'System.Single' cannot be converted to type 'System.Int32'.
```

현재 코드는 해당 필드를 수정하지 않습니다.

## 제거된 관련 코드

- `NumberInterpolatorHook`
- `SXGTReaderHook`의 진단성 후킹

## 주의

스코어 제한 해제는 오프라인 커스텀 플레이 목적입니다. 온라인 랭킹이나 공식 기록과 섞이지 않도록 주의해야 합니다.
