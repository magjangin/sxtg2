# 스코어 시스템

기준일: 2026-09-28 (v1.1.0)

## 현재 역할

커스텀 차트 플레이 중 게임의 노트 수 기반 스코어·곡 종료 판정을 보정하고,
설정 파일로 점수 상한(만점 기준값)을 바꿀 수 있게 합니다. 커스텀 곡/오토/올퍼펙트/점수 상한 변경 플레이는 게임 기록·업적에 섞이지 않게 막습니다.

## 현재 코드 경로

- `Hooks/GameplayHooks.cs`의 `ManagerPlayHook.FetchBMSToModulesPrefix` (주입 시작점)
- `Processors/CustomChartInjector.cs`의 `InjectBmsNotesToLaneData` (노트 수 집계와 덮어쓰기를 한 메서드 안에서 처리)
- `Hooks/GameplayHooks.cs`의 `JudgeScoreMaxHook` (점수 상한 Transpiler)
- `Helpers/ModHelpers.cs`의 `SaveCustomKeyConfig.MaxScore`
- `Hooks/ResultGuardHooks.cs` (기록·업적 차단 — 아래 "기록·업적 차단" 절)

## 적용 시점

```text
ManagerPlay.FetchBMSToModules (원본, 플레이 씬 초기화 중)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
     -> CustomChartInjector.InjectBmsNotesToLaneData(_bms)
          ├─ 노트를 레인에 추가하면서 totalNotes / totalNoteWithTicks 누적
          └─ 마지막에 1회 SXGTData.bpm, totalNotes, totalNoteWithTicks, scorePerNote 덮어쓰기
```

## 현재 보정 대상

- `SXGTData.totalNotes` / `SXGTData.totalNoteWithTicks` (2026-07-18 추가)
- `SXGTData.bpm` (`[#BPM 값]` 하나로 교체 — 홀드 틱 간격과 판정 모듈 틱 간격에 쓰임)
- `SXGTData.scorePerNote` (`1000000 / totalNotes`, 게임 판정식에서는 쓰이지 않음)
- 원본 `Clear_FullCombo`/`Clear_Normal` 사운드 재생(모드 교체 없음)

## 알려진 근본 원인과 수정 (2026-07-18)

커스텀 트랙은 도너 트랙의 패턴 파일을 그대로 빌려 쓰기 때문에(당시에는 `TrackData` 복제, 지금은
`CustomTrackData.ResourceDonor` + `TrackDataMediaHook`), `SXGTReader.ReadBMSFile()`이
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

- 패치는 **항상** 붙습니다(`Prepare()`가 항상 `true`). 기본값 `1000000`이면 `GetMaxScore()`가 같은 값을
  돌려주므로 동작은 원본과 같습니다. 예전에는 기본값이면 `Prepare()`가 `false`를 반환해 패치를 건너뛰었는데,
  v1.1.0부터 `config.txt`를 플레이마다 다시 읽게 되면서 "시작할 때 기본값이었으면 훅이 없어서 나중에 값을
  바꿔도 반영되지 않는" 문제가 생겨 항상 패치하도록 바꿨습니다.
- `Prepare()`에서 `SaveCustomKeyConfig.EnsureInitialized()`를 호출하므로, MelonLoader가
  `OnInitializeMelon()`보다 먼저 패치를 적용하더라도 설정값을 정상적으로 읽습니다.
- Transpiler는 두 메서드 안의 `1000000f` 상수를 **전부** 교체합니다. 게임 업데이트로 같은 값이 다른 용도로
  쓰이게 되면 함께 바뀌므로, 업데이트 후에는 로그의 `만점 상수 N곳 교체 완료` 개수를 확인하세요
  (현재 빌드 기준 `Update` 1곳, `CalculateJudgeScore` 1곳).
- 대상 메서드에서 만점 상수를 찾지 못하면(게임 업데이트 등) 경고 로그만 남기고 원본 IL을
  그대로 반환합니다.

주의: 이 값은 **플레이 중 판정 점수 계산**만 바꿉니다. 결과 화면·뮤직셀렉트의 만점 표시
(`Util.cs`, `ManagerResult.cs`, `ManagerMusicSelect.cs`의 `score >= 1000000` 비교)는 별도의
하드코딩 상수라 그대로입니다. 상한을 1000000보다 크게 잡으면(예: 2000000이면 노트 절반만 맞혀도 1000000 이상) 그 화면들이
만점으로 취급하고 결과 화면이 `PUREBLUE_FIRST`/`FULLCOMBO_FIRST` 업적을 요청합니다. 이 때문에 `MaxScore`를 기본값(1000000)이 아니게
바꾼 플레이는 아래 "기록·업적 차단"에서 **모두 기록/업적에서 제외**됩니다(예전에는 `BlockSave=1`이어도 업적은 막지 못했음, 알려진 문제 #13).
클리어 효과음 기준(`RG_PS_Judgement.JudgeDivergence`의 `JudgeScore >= 700000f`)도 고정값이라,
상한을 700000 이하로 낮추면 풀콤보가 아닐 때 `Clear_Normal`이 나오지 않습니다.

## 기록·업적 차단 (`Hooks/ResultGuardHooks.cs`)

모드로 하는 플레이가 게임 기록이나 Steam 업적과 섞이지 않도록 세 개의 훅이 결과 처리를 막습니다.

| 훅 | 막는 것 | 방식 |
| --- | --- | --- |
| `ResultSaveBlockHook` | 하이스코어 저장, 랭킹 점수 전송 | `UserAccountModule.SavePlayData`, `LyrebirdServer.PostUserScore` Prefix가 `false` 반환 (결과 화면 래퍼가 아니라 **가장 안쪽 메서드**를 막아 어떤 경로로 불려도 저장되지 않음) |
| `ResultTaintHook` | 플레이 횟수·실패 횟수·마지막 플레이 곡, 업적 | `ManagerResult.Start`를 Prefix로 감싸 시작 전 값을 저장하고 Finalizer에서 되돌림. `UserAccountModule.RequestAchievementUnlock`은 그동안 `false` 반환 |
| `ServerGuardHook` | 커스텀 곡의 서버 요청 | `IncreaseTrackPlayCount`는 커스텀 ID면 막고, `GetHighScoreList`는 빈 목록으로 응답, `Util.FindTrackByID` Postfix가 커스텀 트랙을 찾아 줘서 랭킹 화면의 NullReferenceException을 막음 |

차단하는 조건(위에서부터 먼저 맞는 사유가 로그에 남음):

1. `EnableAutoPlay=1` (오토플레이)
2. `EnableAllPerfect=1` (올퍼펙트)
3. `MaxScore`가 기본값이 아님 (`SaveCustomKeyConfig.IsMaxScoreCustom`)
4. 커스텀 곡 (`CustomTrackData`이거나 `CUSTOM_` 접두 ID의 랭킹 데이터)
5. `BlockSave=1` (저장·랭킹 전송만 막음 — 위 1~4와 달리 플레이 횟수/업적은 건드리지 않음)

**커스텀 곡은 `BlockSave` 값과 상관없이 항상 제외됩니다.** 예전에는 `BlockSave=0`이면 커스텀 곡 점수가 저장되거나 서버로 올라가려 했고, 업적과
플레이 횟수는 `BlockSave=1`이어도 올라갔습니다. 도너 트랙 ID로 저장이 새는 것도 막기 위해 결과 화면 래퍼가 아니라 저장 메서드 자체를 막습니다.
`BlockSave`(`SaveCustomKeyConfig.BlockSave`)는 이제 원곡을 오토/올퍼펙트 없이 플레이하는 경우에만 의미가 있습니다. 설정 항목 `BlockSaveBestRanking`은
호환용으로 남아 있고 효과가 없습니다(설명에 "사용 안 함").

로그: 차단될 때 `[차단] 하이스코어 및 랭킹 저장 차단(사유): ...`, 결과 화면 진입 시 `[ResultTaint] 이번 결과는 업적/플레이 횟수에 반영하지 않습니다 (...)`가
남습니다(업적 요청을 막은 로그는 `LogLevel=2`에서만). 패치 대상을 찾지 못하면 `[ResultSaveBlock]` 경고가 나옵니다.
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
