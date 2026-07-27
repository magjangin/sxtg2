# 스코어 시스템

기준일: 2026-07-27

## 현재 역할

커스텀 차트 플레이 중 게임의 노트 수 기반 스코어·곡 종료 판정을 보정하고,
설정 파일로 점수 상한(만점 기준값)을 바꿀 수 있게 합니다.

## 현재 코드 경로

- `Hooks/SXGT/SXGTDataHook.Pending.cs`
- `Processors/CustomChartInjector.cs` (`AccumulateNoteCounts` / `ApplyNoteCountsToSxgtData`)
- `Hooks/GameplayHooks.cs`의 `JudgeScoreMaxHook` (점수 상한 Transpiler)
- `Helpers/ModHelpers.cs`의 `SaveCustomKeyConfig.MaxScore`

## 적용 시점

```text
SXGTDataHook.ProcessPendingNoteRemovalAndInjection
  -> CustomChartInjector.InjectBmsNotesToLaneData
       -> (레인별 노트 주입 중 AccumulateNoteCounts로 누적)
       -> ApplyNoteCountsToSxgtData (마지막에 1회, totalNotes/totalNoteWithTicks 덮어쓰기)
```

## 현재 보정 대상

- `SXGTData.totalNotes` / `SXGTData.totalNoteWithTicks` (2026-07-18 추가)
- 원본 `Clear_FullCombo`/`Clear_Normal` 사운드 재생(모드 교체 없음)

## 알려진 근본 원인과 수정 (2026-07-18)

커스텀 트랙은 도너(복제 원본) `TrackData`를 그대로 복제해서 만들어지고, `SXGTReader.ReadBMSFile()`이
도너 차트를 파싱하며 채운 `totalNotes`/`totalNoteWithTicks`(노트 개수 기반 스코어·곡종료 판정 값)는
`CustomChartInjector`가 `laneData`(노트 리스트) 내용만 커스텀 차트로 갈아끼워도 갱신되지 않았습니다.

- `RG_PS_Judgement.JudgeScore = Mathf.Lerp(0f, 1000000f, JudgeRatio / bms.totalNotes)`
- `RG_PS_Judgement`의 곡 종료 판정: `elapsedNote >= bms.totalNoteWithTicks`

이 두 값이 실제 재생 중인 커스텀 차트가 아니라 도너 트랙의 노트 개수를 기준으로 계산되면서,
커스텀 차트의 노트 수가 도너보다 많거나 적을 때 스코어가 실제 실력과 무관하게 튀거나
곡이 중간에 끝난 것처럼 처리되어 `Clear_FullCombo`/`Clear_Normal`이
부적절한 시점에 재생되는 증상으로 나타났습니다.

이전의 `SXGTData.maxScore` **필드** 보정은 디컴파일 원본의 실제 판정식이 참조하지 않는 필드라
효과가 없었습니다. 해당 보정 파일과 호출부는 현재 제거된 상태입니다. 점수 상한을 실제로 바꾸려면
아래 "점수 상한 설정" 절의 Transpiler 방식을 써야 합니다.

**수정**: `CustomChartInjector`가 노트를 레인에 실제로 주입하는 동안 성공한 노트 수를 직접 세어
(원본 게임과 동일하게 레인 9/10 제외, 홀드 노트는 `tickLength`만큼 추가) 주입이 끝난 직후
`SXGTData.totalNotes`/`totalNoteWithTicks`를 이 값으로 덮어씁니다.

**후속 확인 (2026-07-18)**: 위 수정 이후 `Clear_FullCombo`/`Clear_Normal`이 실제 곡 종료 시점에
정상적으로 재생되는 것을 실게임에서 확인했습니다. 따라서 `KeyBlue_Tam`으로 바꿔치기하던
임시방편과 `SoundObject.Play`/`PlayAndDestroy` 후킹 코드를 삭제했습니다. 원본 클리어 사운드는
이제 교체 없이 그대로 재생됩니다.

## 점수 상한 설정 (2026-07-27 추가)

`SaveCustomKey/config.txt`의 `MaxScore` 값으로 만점 기준값을 바꿀 수 있습니다.

- 구현: `Hooks/GameplayHooks.cs`의 `JudgeScoreMaxHook` (Harmony **Transpiler**)
- 대상: `RhythmGame.Play.RG_PS_Judgement.Update()`, `RG_PS_Judgement.CalculateJudgeScore(float)`

만점 상수 `1000000f`는 `SXGTData.maxScore` 필드가 아니라 위 두 메서드 안에 리터럴로 직접
박혀 있습니다. 따라서 리플렉션으로 필드를 바꾸는 방식(과거 `maxScore` 보정)은 효과가 없고,
IL을 고치는 Transpiler만 동작합니다.

```text
Update()               : JudgeScore = Mathf.Lerp(0f, 1000000f, JudgeRatio / bms.totalNotes)
CalculateJudgeScore()  : JudgeScore = Mathf.Min(JudgeScore, 1000000f)
```

Transpiler는 `Ldc_R4 1000000f` 명령어를 찾아 상수를 새 값으로 굽는 대신
`JudgeScoreMaxHook.GetMaxScore()` **호출**(`Call`)로 바꿉니다. 스택 효과가 동일해 안전하고,
설정 로드와 Harmony 패치 적용 순서에 관계없이 항상 최신 설정값을 읽습니다.
`CodeInstruction`의 `opcode`/`operand`만 교체하므로 라벨과 예외 블록도 보존됩니다.

동작 조건과 안전장치:

- `MaxScore`가 기본값 `1000000`이면 `Prepare()`가 `false`를 반환해 **패치 자체를 붙이지 않습니다**.
  설정을 건드리지 않은 사용자는 게임 IL이 그대로 유지됩니다.
- `Prepare()`에서 `SaveCustomKeyConfig.EnsureInitialized()`를 호출하므로, MelonLoader가
  `OnInitializeMelon()`보다 먼저 패치를 적용하더라도 설정값을 정상적으로 읽습니다.
- 대상 메서드에서 만점 상수를 찾지 못하면(게임 업데이트 등) 경고 로그만 남기고 원본 IL을
  그대로 반환합니다.

주의: 이 값은 **플레이 중 판정 점수 계산**만 바꿉니다. 결과 화면·뮤직셀렉트의 만점 표시
(`Util.cs`, `ManagerResult.cs`, `ManagerMusicSelect.cs`의 `score >= 1000000` 비교)는 별도의
하드코딩 상수라 그대로입니다. 상한을 1000000보다 크게 잡으면 그 화면들에서 만점 취급이
어긋날 수 있으니, `BlockSave=1`을 유지한 채 사용하는 것을 권장합니다.

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
- `Hooks/Audio/AudioSourceHook.cs` (2026-07-18, `KeyBlue_Tam` 전수 스캔용, 근본 원인 수정 후 불필요해짐)

## 주의

스코어 제한 해제는 오프라인 커스텀 플레이 목적입니다. 온라인 랭킹이나 공식 기록과 섞이지 않도록 주의해야 합니다.
