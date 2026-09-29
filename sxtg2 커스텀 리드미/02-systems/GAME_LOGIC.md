# 게임 로직 분석

기준일: 2026-09-28 (디컴파일 원본 `sxtg2/` 폴더 기준, 모드 v1.1.0)

이 문서는 **Sixtar Gate STARTRAIL** 원본 게임이 차트를 읽고, 노트를 만들고, 판정하고, 결과를 저장하는 과정을
정리합니다. 모드가 어디에 끼어드는지는 각 절 끝의 "모드 개입"과 마지막 표에 모았습니다.

> 디컴파일 원본은 저장소 루트의 `sxtg2/` 폴더(ilspycmd 산출물, `.gitignore`로 제외)에 있습니다.
> 게임이 업데이트되면 아래 내용도 바뀔 수 있습니다.

## 목차

1. [데이터 타입](#데이터-타입)
2. [씬 흐름](#씬-흐름)
3. [차트 로드](#차트-로드)
4. [노트 생성과 이동](#노트-생성과-이동)
5. [판정](#판정)
6. [점수와 곡 종료](#점수와-곡-종료)
7. [결과 저장](#결과-저장)
8. [모드 개입 지점 요약](#모드-개입-지점-요약)

---

## 데이터 타입

### 노트

```text
RhythmGame.Note            timing, nType, nColor(기본 BLUE), targetLane(기본 -1), referObject, luckyScore, nAction
├── RhythmGame.ShortNote   생성자 (float, int) → nType = SHORT
└── RhythmGame.HoldNote    생성자 (float, int) → nType = HOLD
                           duration, isFinished, isHeadJudged, elapsedTick, tickTime[], tickJudge[], tickLength
                           FinishHoldNote(float dur, float bpm) 로 duration과 틱을 채움
```

`HoldNote`의 생성자는 `(float, int)` 하나뿐입니다. 예전 문서의 5인자 생성자(`duration`/`tickTime`을 받는 버전)는
현재 빌드에 없습니다.

`FinishHoldNote`의 틱 규칙 (`unit = 60 / bpm / 4`, 16분음표):

- `tickLength = (int)((dur - 3·unit) / unit)`, 1보다 작으면 1
- `tickLength == 1`: `tickTime = [timing + unit]`
- 그 외: `tickTime[i] = timing + 2·unit + i·unit`

### 레인 (`LaneIndex`)

| 값 | 이름 | 비고 |
| --- | --- | --- |
| 0 | `LL` | |
| 1 | `L` | |
| 2 | `R` | |
| 3 | `RR` | |
| 4 | `LT` | 빨간 노트 레인 |
| 5 | `RT` | 빨간 노트 레인 |
| 6 | `GATE` | |
| 7 | `LSHIFT` | |
| 8 | `RSHIFT` | |
| 9 | `OPEN` | 오픈 노트 |
| 10 | `ACTION` | 액션 노트 |

### 기타 열거형

- `NoteColor`: `NONE`, `OPEN`, `BLUE`, `RED`, `SHIFT`, `ACTION`
- `EJudges`: `BLUESTAR`(0), `WHITESTAR`(1), `YELLOWSTAR`(2), `REDSTAR`(3, 미스)

### 차트 (`SXGTData`)

```csharp
public Dictionary<int, List<Note>> laneData;   // 생성자에서 레인 수만큼 빈 리스트 생성
public Dictionary<int, HoldNote> unfinished;
public float trackStartTiming, cruiseBeginTime = -1f, cruiseFinishTime;
public List<float> bpm;
public float scorePerNote;
public readonly float maxScore = 1000000f;    // 점수 계산식은 이 필드가 아니라 리터럴 1000000f를 씀
public int totalNotes, totalNoteWithTicks, totalTicks;
```

`List<Note>`라서 `ShortNote`/`HoldNote`를 그대로 넣을 수 있습니다.

---

## 씬 흐름

게임은 `RG_SceneManager.MoveScene(이름, 콜백)`으로 씬을 바꿉니다. 사용되는 씬 이름:
`Warning`, `MainTitle`, `ModeSelect_…`, `MusicSelect`, `PlayLoading`, `Play`, `Result`, `Setting`,
`MissionSelect`/`MissionLobby`/`MissionResult`, `ExplorerLobby`/`ExplorerResult`, `LessonSelect`.

일반 플레이는 `MusicSelect` → `PlayLoading` → `Play` → `Result` 순서입니다. 리트라이는
`ManagerPlay.RestartGame()`이 `Play` 씬을 **다시 로드**하고 콜백에서 `Set(playTrack, lv, ps)`를 다시 호출합니다.

**모드 개입**: `Main`이 `activeSceneChanged`로 씬 이름을 보고 플레이 씬 여부를 판단합니다(이름에 `play`/`rhythm`/`game`
포함). 이 규칙은 `PlayLoading`도 플레이 씬으로 봅니다(`HOOK_SYSTEM.md` 참고).

---

## 차트 로드

```text
PlayLoadingManager  → MoveScene("Play", 콜백)
  콜백: ManagerPlay.Set(track, lv, ps)
          ├─ dir = track.GetSixtarPatternDirectory(lv, ps, willUseSXGT: true)
          ├─ bms = GetPatternFromDir(dir, isEncrypted: true)
          │        = new SXGTReader(...).ReadBMSFile(dir, isEncrypted)   // 암호화된 패턴 파일 → SXGTData
          ├─ SetBGM(track)   // track.GetAudioClip()
          ├─ SetBGA(track)
          └─ judgeModule.SetJudgeRange(JudgeBalancer.BalanceList[(int)lv])

ManagerPlay 초기화 (씬 모드별 처리 후)
  ├─ InitializeWidgets(ud.widgets)       // 유저가 장착한 PlayWidget 최대 5개
  ├─ FetchUserDataToPlayScene(ud)
  └─ FetchBMSToModules(bms)
        ├─ noteGenerator.FetchBMS(bms)
        └─ judgeModule.FetchBMS(bms)
```

**모드 개입**
- 커스텀 트랙이면 `TrackDataMediaHook`이 `GetSixtarPatternDirectory`/`GetAudioClip`을 도너 트랙 값으로 돌려주므로,
  `Set`까지는 **도너 곡**이 로드됩니다.
- `ManagerPlayHook.FetchBMSToModulesPrefix`가 모듈에 넘어가기 직전에 같은 `SXGTData`의 레인을 비우고 커스텀 노트를 채우며,
  BGM/BGA도 이때 교체를 시작합니다.

---

## 노트 생성과 이동

### 생성 (`NoteGenerator`)

- `Start()` → `DelayedInitialize()` 코루틴: 기어 준비를 기다린 뒤 레인 수만큼 커서를 만들고, 노트 스킨 프리팹을
  `Resources.Load("Rhythm Game Part/PlayScene/NoteSkin/{스킨}/_Blue")`(`_Red`, `_White`, `_Gate`)로 불러옵니다.
  `_White`/`_Gate`가 없으면 Blue로 대체.
- `Update()`: 레인마다 `curTime + notePreGenerateTime(3초, private readonly) >= 다음 노트 timing`이면 `Generate`.
- `Generate(lane, note)`: 색이 기본(BLUE 등)이면 `targetLane` 0/3 → Blue, 1/2 → White, 6 → Gate 프리팹,
  `RED` → Red, `SHIFT` → Shift(오른쪽이면 x 스케일 −1), `OPEN`/`ACTION` → Blue. `Instantiate` 후
  `SetTiming(timing, 홀드면 duration 아니면 0)`, `note.referObject`에 GameObject 저장.
  - 색이 기본인데 `targetLane`이 4/5 등이면 프리팹이 선택되지 않아 `null`이 됩니다(모드는 레인 4/5를 `RED`로 만들어 피함).

### 이동 (`RG_NoteObject`)

- `SetTiming`: `Timing` 저장, 게임 설정의 노트 크기(`userData.noteSize / 100`)를 `shortNote`/`holdTexture`/`tailNote`
  크기에 곱함, 홀드면 `holdMask`/`tailNote` 활성화.
- 매 프레임 `CalculatePosition(ManagerPlay.Instance.CurTime)`:

```csharp
float num  = Math.Max((Timing - curTime) * noteSpeed * 2.5f, 0f);
shortNote.anchoredPosition = (0, num);
if (Duration != 0f) {
    float num2 = Math.Max((Timing - curTime + Duration) * noteSpeed * 2.5f, 0f);
    holdMask.sizeDelta        = (x, num2 - num);
    holdMask.anchoredPosition = (0, num + (num2 - num) / 2);
    holdTexture.anchoredPosition = (0, -holdMask.y + holdTextureYOffset(450));
    tailNote.anchoredPosition = (0, num2);
}
```

루트 `RectTransform`은 건드리지 않습니다(`ForceSetPosition`만 루트를 움직이는데 호출하는 곳이 없음).

**모드 개입**: `NoteSpriteHook`(Generate 후 스프라이트 교체), `NoteSwayHook`(루트 x), `NoteSpeedChaosHook`
(자식 y 배율, `notePreGenerateTime` 연장). 자세한 내용은 `NOTE_SYSTEM.md`.

---

## 판정

### 매 프레임 (`RG_PS_Judgement.Update`)

```text
if (!judgeInputEnabled || !ManagerPlay.Instance.initialized) return
if (tickInterval < 0) SetTickInterval(bms.bpm[0])
for 레인 i in 0..numLanes-1:
    오토플레이가 아니면 CheckMissBreak(curTime, i), 오토플레이면 AutoPlayJudge(curTime, i)
    CheckShiftWarning, CheckHoldTick, (크루즈 미종료 시) CheckCruiseMode
CheckOpenState, CheckActionLane
JudgeRatio 합산 → JudgeScore = Lerp(0, 1000000, JudgeRatio / totalNotes) → 점수 위젯 갱신
```

이 메서드 안에서 예외가 나면 그 프레임의 뒤쪽 처리가 전부 건너뛰어집니다. 예를 들어 `CheckHoldTick`은 헤드가
판정된 홀드의 `tickTime.Length`를 읽으므로, `tickTime`이 null인 홀드가 있으면 매 프레임 여기서 멈춥니다
(모드의 "홀드 끝 누락" 알려진 문제의 원인).

### 입력 판정 (`TryJudgeShortNote(judgeTime, note)`)

```text
judgeTime -= userData.adjustSync / 1000
오차 = |note.timing - judgeTime|
EJudges 0..3 중 오차 <= JudgeRange[i] 인 첫 등급으로
    JudgeDivergence(등급, note)
    WidgeInvoke(pw => pw.OnGetJudge(등급, note.timing - judgeTime))   // 양수 = FAST
```

`JudgeRange`는 난이도별 `JudgeBalancer.BalanceList`에서 옵니다(BLUESTAR 기준 Comet 72ms, Nova 54ms,
SuperNova/Quasar 36ms — 전체 표는 `PLAY_OVERLAY.md`).

### 홀드 틱 (`CheckHoldTick`)

헤드가 판정된 홀드는 `tickTime[elapsedTick]` 시각마다 레인을 누르고 있는지(또는 오토플레이인지) 보고
`BLUESTAR`/`REDSTAR`(모드에 따라 `WHITESTAR`)로 `JudgeDivergence`를 호출합니다. 틱을 다 쓰고 `timing + duration`이
지나면 노트 오브젝트를 지우고 커서를 넘깁니다.

### 판정 기록 (`JudgeDivergence`)

미스면 `JudgeAction_Miss`, 아니면 `JudgeAction`을 호출하고 `elapsedNote++`, `JudgeCount.AddJudge(등급)`.

**모드 개입**: `AutoPlayHook`(Update Postfix에서 `AutoPlayJudge` 호출), `AllPerfectJudgeHook`(등급을 BLUESTAR로),
`FastSlowMeter_OnGetJudge_Patch`(판정바 데이터).

---

## 점수와 곡 종료

| 항목 | 원본 식 | 쓰는 값 |
| --- | --- | --- |
| 점수 | `JudgeScore = Lerp(0, 1000000, JudgeRatio / totalNotes)` (`Update`), `Min(JudgeScore, 1000000)` (`CalculateJudgeScore`) | `bms.totalNotes` |
| 클리어 효과음 | `elapsedNote >= totalNoteWithTicks`가 되는 순간: REDSTAR 0개면 `Clear_FullCombo`, 아니면 점수 ≥ 700000일 때 `Clear_Normal` | `bms.totalNoteWithTicks` |
| 곡 종료 | `ManagerPlay.CheckGameFinished(curTime)`: `curTime >= bgmLength - 0.05`면 종료 → 1.5초 뒤 결과 화면 | `bgm.clip.length` |

곡 종료는 노트 수가 아니라 **BGM 길이** 기준입니다. 커스텀 BGM 로드에 실패하면 도너 곡 길이로 끝납니다.

**모드 개입**: `CustomChartInjector`가 `totalNotes`/`totalNoteWithTicks`를 커스텀 차트 기준으로 다시 계산,
`JudgeScoreMaxHook`이 두 `1000000f` 리터럴을 설정값으로 교체. 자세한 내용은 `SCORE_SYSTEM.md`.

---

## 결과 저장

`ManagerResult.Start` 흐름 끝부분:

```text
ComparePlayResultHighScore(playResult)   // 로컬 최고 기록 갱신
CheckResultSceneAchievements()
StartCoroutine(SetBackable(2.5f))
PostRequestPlayResult()                  // 서버 전송
```

**모드 개입**: `ResultSaveBlockHook`이 조건에 따라 `ComparePlayResultHighScore`와 `PostRequestPlayResult`를 건너뜁니다.
`ManagerResultHook`(진단 로깅)은 `Start` Postfix입니다.

**모드가 막지 않는 것**: `ManagerResult.Start`는 위 흐름 앞뒤로 `userData.playCount++`(113행), 점수 1,000,000 이상/풀콤보일 때
`RequestAchievementUnlock("PUREBLUE_FIRST"/"FULLCOMBO_FIRST")`(150~161행), 실패 시 `failCount++`(182행)을 실행하고,
`CheckResultSceneAchievements()`는 플레이 횟수/난이도/실패 횟수 업적과 `lastPlayedTrackID`/`sameTrackPlayCount` 갱신을
합니다(236~253행). 모두 `ResultSaveBlockHook` 밖입니다. 곡을 시작할 때는 `ManagerMusicSelect.MoveToPlayLoadingScene`이
`lastSelectedSongIndex = 트랙 ID`를 저장하고 `LyrebirdServer.IncreaseTrackPlayCount`로 서버에 곡 ID를 보냅니다(540~544행).
저장은 전부 `UserAccountModule.SaveRequest` → `FSForSteam.SaveData` → `SteamRemoteStorage.FileWrite`(Steam 클라우드)입니다.
자세한 영향은 `CURRENT_STATUS.md` 알려진 문제 #13, #14.

---

## 모드 개입 지점 요약

| 게임 쪽 지점 | 모드 코드 | 목적 |
| --- | --- | --- |
| `ManagerMusicSelect.Awake` | `ManagerMusicSelectHook.AwakePostfix` | 커스텀 트랙 등록 |
| `ManagerMusicSelect.PlayPreview` | `ManagerMusicSelectHook.PlayPreviewPrefix` | 커스텀 미리듣기 |
| `TrackData.Get*` | `TrackDataMediaHook` | 도너 리소스/커스텀 자켓 |
| `ManagerPlay.FetchBMSToModules` | `ManagerPlayHook.FetchBMSToModulesPrefix` | 노트 주입, BGM/BGA 교체 |
| `ManagerPlay.CheckBGMStart` | `ManagerPlayHook.CheckBGMStartPrefix` | 커스텀 BGM 로드 대기 |
| `NoteGenerator.Generate` / `Start` | `NoteSpriteHook`, `NoteSpeedChaosHook` | 스킨, 선행 생성 시간 |
| `RG_NoteObject.CalculatePosition` | `NoteSwayHook`, `NoteSpeedChaosHook` | 노트 연출 |
| `RG_PS_Judgement.Update` | `AutoPlayHook`(Postfix), `JudgeScoreMaxHook`(Transpiler) | 오토플레이, 점수 상한 |
| `ManagerPlay.CheckGameFinished` | `AutoPlayHook`(Prefix) | 현재 시간 기록 |
| 판정 관련 메서드들 | `AllPerfectJudgeHook` | 등급 조작 |
| `PlayWidget.OnGetJudge(EJudges, float)` | `FastSlowMeter_OnGetJudge_Patch` | 판정바 |
| `ManagerResult.ComparePlayResultHighScore` / `PostRequestPlayResult` | `ResultSaveBlockHook` | 저장 차단 |

## 관련 문서

- [HOOK_SYSTEM.md](HOOK_SYSTEM.md): 훅 상세
- [NOTE_SYSTEM.md](NOTE_SYSTEM.md): 노트 주입과 연출
- [SCORE_SYSTEM.md](SCORE_SYSTEM.md): 점수 보정
- [BMS_PARSING.md](BMS_PARSING.md): BMS 파싱
- [../00-overview/DOCUMENTATION.md](../00-overview/DOCUMENTATION.md): 전체 흐름
