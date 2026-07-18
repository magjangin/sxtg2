# 스코어 시스템

기준일: 2026-07-18

## 현재 역할

커스텀 차트 플레이 중 게임의 기본 스코어 제한과 일부 사운드 처리를 보정합니다.

## 현재 코드 경로

- `Hooks/Audio/HighscoreMeterHook.cs`
- `Hooks/Audio/HighscoreMeterHook.Score.cs`
- `Hooks/Audio/HighscoreMeterHook.SXGTReader.cs`
- `Hooks/Audio/HighscoreMeterHook.SoundObject.cs`
- `Hooks/Audio/HighscoreMeterHook.AudioClip.cs`
- `Hooks/SXGT/SXGTDataHook.Score.cs`
- `Processors/CustomChartInjector.cs` (`AccumulateNoteCounts` / `ApplyNoteCountsToSxgtData`)

## 적용 시점

```text
CustomPlayStartupFlow.Run
  -> HighscoreMeterHook.ApplyForCustomChart
  -> FixSXGTReaderMaxScore

SXGTDataHook.ProcessPendingNoteRemovalAndInjection
  -> FixMaxScoreField
  -> CustomChartInjector.InjectBmsNotesToLaneData
       -> (레인별 노트 주입 중 AccumulateNoteCounts로 누적)
       -> ApplyNoteCountsToSxgtData (마지막에 1회, totalNotes/totalNoteWithTicks 덮어쓰기)
```

## 현재 보정 대상

- `SXGTReader.MaxScore`
- `SXGTData.maxScore`
- `SXGTData.totalNotes` / `SXGTData.totalNoteWithTicks` (2026-07-18 추가)
- `SoundObject` clear 계열 사운드

## 알려진 근본 원인과 수정 (2026-07-18)

커스텀 트랙은 도너(복제 원본) `TrackData`를 그대로 복제해서 만들어지고, `SXGTReader.ReadBMSFile()`이
도너 차트를 파싱하며 채운 `totalNotes`/`totalNoteWithTicks`(노트 개수 기반 스코어·곡종료 판정 값)는
`CustomChartInjector`가 `laneData`(노트 리스트) 내용만 커스텀 차트로 갈아끼워도 갱신되지 않았습니다.

- `RG_PS_Judgement.JudgeScore = Mathf.Lerp(0f, 1000000f, JudgeRatio / bms.totalNotes)`
- `RG_PS_Judgement`의 곡 종료 판정: `elapsedNote >= bms.totalNoteWithTicks`

이 두 값이 실제 재생 중인 커스텀 차트가 아니라 도너 트랙의 노트 개수를 기준으로 계산되면서,
커스텀 차트의 노트 수가 도너보다 많거나 적을 때 스코어가 실제 실력과 무관하게 튀거나
곡이 중간에 끝난 것처럼 처리되어 `Clear_FullCombo`/`Clear_Normal`(→`KeyBlue_Tam`)이
부적절한 시점에 재생되는 증상으로 나타났습니다.

`maxScore`/`MaxScore` 보정(`FixMaxScoreField`, `FixSXGTReaderMaxScore`)은 애초에 저 스코어 계산식이
참조하지 않는 필드라 효과가 없었습니다 (게다가 `FixSXGTReaderMaxScore`는 static 필드로 찾도록
되어 있는데 실제 `SXGTReader.MaxScore`는 인스턴스 필드라 항상 no-op이었습니다).

**수정**: `CustomChartInjector`가 노트를 레인에 실제로 주입하는 동안 성공한 노트 수를 직접 세어
(원본 게임과 동일하게 레인 9/10 제외, 홀드 노트는 `tickLength`만큼 추가) 주입이 끝난 직후
`SXGTData.totalNotes`/`totalNoteWithTicks`를 이 값으로 덮어씁니다.

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
