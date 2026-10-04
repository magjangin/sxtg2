# 🎵 노트 시스템

기준일: 2026-09-28 (v1.1.0)

**파싱된 노트를 게임 노트로 바꿔 주입하는 과정, 노트 스킨, 노트 연출(흔들림/속도 카오스)**

---

## 노트 타입

```csharp
// 파서 결과 (Loaders/BmsParser.cs)
public class ParsedNote
{
    public float Time { get; set; }        // 초
    public int Lane { get; set; }          // 0~6, 오픈은 9
    public NoteType NoteType { get; set; } // Normal, Long, Open (끝 노트는 파싱 단계에서 제거됨)
    public float Length { get; set; }      // 홀드 길이(초)
    public string OriginalNoteValue { get; set; }
}

// 게임 노트 (Assembly-CSharp, 직접 참조)
RhythmGame.Note        // timing, nType, nColor, targetLane, referObject, luckyScore, nAction
├── RhythmGame.ShortNote   // 생성자 (float timing, int lane) → nType = SHORT
└── RhythmGame.HoldNote    // 생성자 (float timing, int lane) → nType = HOLD
                           // duration, tickTime[], tickJudge[], tickLength, isFinished, isHeadJudged, elapsedTick
```

`HoldNote`의 생성자는 `(float, int)` 하나뿐입니다(디컴파일 기준). 길이와 틱은 생성 후
`FinishHoldNote(duration, bpm)`을 호출해야 채워집니다.

---

## 노트 생성과 주입 (`Processors/CustomChartInjector.cs`)

`ManagerPlayHook.FetchBMSToModulesPrefix`가 `SetParsedChart(result)` 후 `InjectBmsNotesToLaneData(_bms)`를 호출합니다.

```text
InjectBmsNotesToLaneData(SXGTData data)
  ├─ data 또는 파싱 결과가 비었으면 경고 후 종료
  ├─ ClearLaneData: laneData의 모든 리스트 Clear, unfinished[레인] = null
  ├─ bpm = 파싱 결과 BaseBpm (0 이하면 150)
  ├─ 각 ParsedNote마다
  │    ├─ CreateNote → ShortNote / HoldNote (끝 노트 종류면 null → 건너뜀)
  │    ├─ data.laneData[Lane]에 추가 (그 레인이 없으면 건너뜀)
  │    └─ 레인 9/10이 아니면 totalNotes += 1,
  │         BLUE/RED 홀드면 totalNoteWithTicks += 1 + tickLength (아니면 += 1)
  ├─ 레인별로 timing 기준 정렬
  ├─ data.bpm = [bpm]
  ├─ data.totalNotes, data.totalNoteWithTicks 덮어쓰기
  ├─ data.scorePerNote = maxScore(1000000) / totalNotes   // 게임 판정식에서는 쓰이지 않음
  └─ 로그: [CustomChartInjector] N개 주입, totalNotes=..., totalNoteWithTicks=..., BPM=..., trackStartTiming=...(도너 값)
```

`SXGTData` 인스턴스는 게임이 도너 패턴을 읽어 만든 것을 그대로 쓰고, 내용만 바꿉니다. 게임은 이 직후
`NoteGenerator.FetchBMS`/`RG_PS_Judgement.FetchBMS`로 같은 인스턴스를 받아 갑니다.

### 노트 만들기 (`CreateNote`)

| `ParsedNote.NoteType` | 게임 노트 | 색 (`ResolveColor`) | 추가 처리 |
| --- | --- | --- | --- |
| `Normal` | `new ShortNote(Time, Lane)` | 레인 4/5 → `RED`, 그 외 → `BLUE` | — |
| `Long` | `new HoldNote(Time, Lane)` | 레인 4/5 → `RED`, 그 외 → `BLUE` | `nAction = NONE`, `Length > 0`이면 `FinishHoldNote(Length, bpm)` |
| `Open` | `new HoldNote(Time, 9)` | `OPEN` | 위와 같음 |
| 그 외 | 만들지 않음 | — | — |

> ⚠️ **알려진 문제 (확인 필요)**: 끝(`03`/`05`)이 없는 홀드 시작은 `Length = 0`이라 `FinishHoldNote`가 호출되지 않고
> `tickTime = null`인 `HoldNote`로 들어갑니다. 게임의 `RG_PS_Judgement.CheckHoldTick`은 헤드가 판정된 뒤
> `holdNote.tickTime.Length`를 읽으므로, 그 순간부터 매 프레임 `NullReferenceException`이 나고 `Update`의 나머지 처리가 멈춥니다.
> 짝 없는 시작을 `ShortNote`로 바꾸거나 버리고 경고를 남기도록 고치는 것이 좋습니다.

### 홀드 틱 (게임 원본 `HoldNote.FinishHoldNote`)

틱 간격은 16분음표 길이입니다.

```text
unit = 60 / bpm / 4                       // 16분음표(초)
tickLength = (int)((duration - 3 × unit) / unit)   // 1보다 작으면 1
tickLength == 1 인 짧은 홀드: tickTime = [timing + unit]
그 외: tickTime[i] = timing + 2 × unit + i × unit   (i = 0 .. tickLength-1)
```

예: `bpm = 160`이면 `unit = 0.09375초`라 첫 틱이 `timing + 0.1875초`, 간격 `0.09375초`입니다. 예전 문서의
"첫 틱 0.188초, 간격 0.094초"는 **160 BPM일 때만** 맞는 값이었습니다.

### 노트 수와 곡 종료

게임의 점수(`JudgeScore = Lerp(0, 1000000, JudgeRatio / totalNotes)`)와 곡 종료(`elapsedNote >= totalNoteWithTicks`)가
이 두 값을 쓰기 때문에, 주입 후 반드시 다시 계산합니다. 자세한 배경은 `SCORE_SYSTEM.md`.

---

## 노트가 화면에 나오는 과정 (게임 원본)

1. `NoteGenerator.Update`가 레인마다 `curTime + notePreGenerateTime(3초) >= 다음 노트 timing`이면 `Generate`
2. `Generate`가 `targetLane`/색으로 프리팹(Blue/White/Red/Gate/Shift)을 골라 `Instantiate` → `SetTiming(timing, duration)`
   (`SetTiming` 안에서 게임 설정의 노트 크기 `noteSize`를 적용)
3. 각 `RG_NoteObject.Update`가 매 프레임 `CalculatePosition(curTime)`으로 자식 y를 계산:
   `y = Max((Timing - curTime) × noteSpeed × 2.5, 0)`

모드는 2단계 직후(`NoteSpriteHook`)와 3단계 직후(`NoteSwayHook`, `NoteSpeedChaosHook`)에 개입합니다.
게임 쪽 구조는 `GAME_LOGIC.md` 참고.

---

## 노트 스킨(커스텀 스프라이트)

이미 생성된 노트의 **시각적 스킨**만 바꾸는 기능입니다(2026-07-18 `InventoryPopup` 모드에서 이식).

### 후킹 지점 (`Hooks/GameplayHooks.cs` `NoteSpriteHook`)

`RhythmGame.NoteGenerator.Generate(LaneIndex, Note)` Postfix에서 반환된 `RG_NoteObject`의 private 필드를
`AccessTools.FieldRefAccess`로 읽습니다.

```csharp
// RhythmGame.RG_NoteObject (디컴파일 기준)
[SerializeField] private RectTransform shortNote;    // 노트 본체(헤드)
[SerializeField] private RectTransform holdMask;     // 홀드 몸통 마스크
[SerializeField] private RectTransform holdTexture;  // 홀드 몸통 무늬 (holdMask 안쪽)
[SerializeField] private RectTransform tailNote;     // 홀드 끝
```

### 처리 순서

1. 노트 GameObject 이름에서 타입 문자열을 뽑습니다(`CustomNoteSpriteLoader.ExtractNoteType`):
   **첫 `_` 뒤부터 두 번째 `_` 앞까지**, 두 번째 `_`가 없으면 첫 `_` 뒤 전부.
2. 각 자식의 `Image.sprite`를 교체합니다(스프라이트를 못 찾으면 그 자식은 그대로).
   - `shortNote`: `{타입}` → 노트 이름 전체
   - `tailNote`: `{타입}_tail` → `tailNote` → (없으면 `shortNote`와 같은 규칙)
   - `holdTexture`: `{타입}_hold` → `holdTexture` → (없으면 `shortNote`와 같은 규칙)
3. `NoteRendererRecovery.RecoverNoteRenderer`가 노트 루트와 **직계 자식**의 `Image`마다
   `SetNativeSize()` + `SetAllDirty()`를 호출합니다. 커스텀 스프라이트가 없어도 **모든 노트**에 대해 실행됩니다.

### 스프라이트 소스

`CustomNoteSpriteLoader.Initialize()`(모드 초기화 시 1회)가 `{게임 설치 폴더}\CustomNotes\*.png`를 읽어
`Sprite.Create`로 변환하고 **파일명(확장자 제외) 그대로**를 키로 저장합니다(대소문자 무시). 폴더가 없으면 만듭니다.
게임 실행 중에 PNG를 추가/수정하면 재시작해야 반영됩니다.

### ⚠️ 알려진 문제 — 현재 파일명 규칙이 실제 노트 이름과 맞지 않음 (확인 필요)

게임은 노트 프리팹을 `Resources.Load("Rhythm Game Part/PlayScene/NoteSkin/{스킨}/_Blue")`처럼 불러와 `Instantiate`하므로,
생성된 노트 이름은 `_Blue(Clone)` 형태가 됩니다(Unity는 복제본 이름 뒤에 `(Clone)`을 붙임). 여기서 1번 규칙으로 뽑히는
타입은 `Blue`가 아니라 **`Blue(Clone)`**입니다. 그래서:

- `Blue.png`, `Red.png`, `Gate.png` 같은 이름은 **매칭되지 않습니다**.
- 현재 코드에서 실제로 매칭되는 이름은 `Blue(Clone).png`(또는 전체 이름 `_Blue(Clone).png`), 끝/몸통은
  `Blue(Clone)_tail.png`/`Blue(Clone)_hold.png` 또는 공용 `tailNote.png`/`holdTexture.png`입니다.
  노트 종류: `_Blue`, `_White`(레인 L/R), `_Red`, `_Gate` (스킨에 없으면 게임이 Blue로 대체).
- 끝/몸통 스프라이트가 없으면 **헤드 스프라이트가 끝과 몸통에도 들어갑니다**.

2026-07-18에 처음 이식했을 때의 로더는 이름에 `_blue`/`_red`/`_gate`가 들어 있는지로 판별하고(`Blue.png` 방식),
Gate가 없으면 Blue로 대체하고, 끝/몸통은 `BlueTail`/`TailBlue`/`Tail`/`TailNote` 순서로 찾고 없으면 적용하지 않았습니다.
2026-07-26 파일 통합(`12348f5`) 때 로더가 단순화되면서 이 규칙이 사라졌습니다. 이식 당시에도 실게임 확인은 되지 않았습니다.

### ⚠️ 확인 필요 — 게임 노트 크기 옵션

게임은 `SetTiming`에서 노트 크기 설정(`userData.noteSize`)을 `sizeDelta`에 곱해 둡니다. 그 뒤 모드의
`SetNativeSize()`가 `Image` 크기를 스프라이트 원본 크기로 되돌리므로, 노트 크기를 100%가 아닌 값으로 설정한 경우
설정이 무시될 수 있습니다. 게임에서 노트 크기를 바꿔 모드 유무로 비교해 보세요.

---

## 노트 흔들림 연출 (NoteSway, 2026-07-27 추가)

노트가 눈송이처럼 좌우로 흔들리며 내려오는 순수 시각 효과입니다. `SaveCustomKey/config.txt`의
`NoteSway` 항목으로 켭니다(기본 꺼짐). 구현은 `Hooks/GameplayHooks.cs`의 `NoteSwayHook`.

### 왜 루트를 흔드는가

원본 `RG_NoteObject.CalculatePosition(curTime)`은 **x를 항상 0으로 고정**한 채 자식들의 y만 세팅합니다.

```csharp
shortNote.anchoredPosition = new Vector3(0f, num, 0f);
if (Duration != 0f)
{
    holdMask.sizeDelta        = (x, num2 - num);           // 길이 = 꼬리y - 헤드y
    holdMask.anchoredPosition = (0, num + (num2-num)/2);   // 중심
    holdTexture.anchoredPosition = (0, -holdMask.y + 450); // 마스크 이동을 상쇄
    tailNote.anchoredPosition = (0, num2);
}
```

홀드 몸통은 **단일 RectTransform 직사각형**이라 S자로 휘게 만들 수 없습니다. 그래서 자식이 아니라
`RG_NoteObject`의 **루트 RectTransform**을 통째로 미는 방식을 씁니다.

- 헤드·몸통·꼬리가 한 덩어리(뻣뻣한 막대)로 움직입니다. 최신 게임 버전의 연출도 이 형태입니다.
- `holdMask`만 x로 옮기면 그 안의 `holdTexture`가 상대적으로 밀려서 무늬만 반대로 미끄러져 보입니다
  (원본이 y축에 대해 정확히 그 보정을 하고 있음). 루트를 옮기면 마스크와 텍스처가 함께 움직여
  이 문제가 생기지 않습니다.
- 루트의 x는 게임이 건드리지 않습니다. `ForceSetPosition`이 유일하게 루트를 만지는 메서드인데
  현재 빌드에서 **호출하는 곳이 없습니다**. 그래서 최초 관측 시의 x를 기준점(`BaseX`)으로 캡처해도
  안전합니다.

### 흔들림 식

```text
offset = amplitude * sin(curTime * speed * 2π + phase)
```

- `curTime`은 곡 진행 시간(`Time.time`이 아님)이라 일시정지하면 흔들림도 같이 멈춥니다.
- `phase`는 `Mathf.Repeat(Timing * 12.9898f, 2π)` — 노트마다 위상을 흩뿌려 제각각 흔들리게 합니다.
  `Timing` 기반이라 결정론적이고, 리트라이해도 같은 궤적이 나옵니다.
- `NoteSwayDamping`이 켜져 있으면 판정선 도달 `NoteSwayDampingTime`초 전부터 진폭이 0으로 수렴합니다.
  화면 위쪽에서는 나풀거리다가 칠 때는 제자리에 있으므로 정확도에 영향을 주지 않습니다.

### 판정과의 관계

판정은 `RG_PS_Judgement.TryJudgeShortNote`가 `Note.timing`과 시간만 비교하고 화면 위치는 보지
않습니다. 따라서 이 연출은 진폭을 아무리 키워도 **판정에 전혀 영향이 없습니다**.

### 주의

`Lane` 프리팹에 `RectMask2D`가 붙어 있으면 진폭이 클 때 레인 밖으로 나간 노트가 잘립니다.
프리팹 설정이라 코드로는 확인할 수 없으므로, 기본값(12px)에서 시작해 실제 화면을 보며 조정하세요.

상태(`BaseX`/`Phase`)는 노트 인스턴스 ID로 캐싱하며 씬 전환 시 `NoteSwayHook.Reset()`으로 비웁니다.

---

## 노트 속도 카오스 (NoteSpeedChaos, 2026-07-27 추가)

노트마다 낙하 속도 배율을 다르게 주는 **챌린지용** 기능입니다. `SaveCustomKey/config.txt`의
`NoteSpeedChaos`로 켭니다(기본 꺼짐). 구현은 `Hooks/GameplayHooks.cs`의 `NoteSpeedChaosHook`.

### 왜 "챌린지용"인가

원본 위치 식은 모든 노트가 같은 `noteSpeed`를 씁니다.

```csharp
y = Max((Timing - curTime) * noteSpeed * 2.5f, 0f)
```

여기서 배율을 노트별로 다르게 주면 **노트끼리 서로 추월합니다**. 예를 들어 10초 노트에 배율
1.8, 12초 노트에 배율 0.6을 주면 나중에 쳐야 할 노트가 화면상 더 아래에 그려집니다. 읽기
난이도를 올리는 것이 목적인 기능이라 의도된 동작이지만, **정상적인 SV(스크롤 속도 변화)와는
다른 것**이라는 점을 알아둬야 합니다.

제대로 된 SV는 노트별 배율이 아니라 곡 전체에 하나뿐인 누적 거리 함수 `F(t)`를 만들고
`y = (F(Timing) - F(curTime)) * 2.5f`로 계산합니다. `F`가 단조증가라 순서가 절대 뒤집히지
않고, 기울기 0이면 STOP, 음수면 역스크롤이 됩니다. 커스텀 곡에서 BPM 변화를 쓰지 않으므로
현재는 구현하지 않았습니다.

### 홀드 처리

원본이 `holdMask.sizeDelta.y`에 `꼬리y - 헤드y`를 넣어두므로, 헤드와 길이에 같은 배율을 걸고
꼬리를 `헤드 + 길이`로 다시 잡습니다. 빠른 홀드는 길쭉하게, 느린 홀드는 뭉툭하게 보입니다.
마스크를 옮겼으니 `holdTexture` 상쇄(`-holdMask.y + holdTextureYOffset`)도 원본과 같은 식으로
다시 걸어줘야 무늬가 반대로 밀리지 않습니다.

### 노트 선행 생성 시간 조정

`NoteGenerator.CheckNoteGenerate`는 **속도와 무관하게** 고정 시간 기준으로 노트를 만듭니다.

```csharp
private readonly float notePreGenerateTime = 3f;
if (_curTime + notePreGenerateTime >= note.timing) { Generate(...); }
```

배율이 1보다 작은(느린) 노트는 생성 시점에 이미 화면 안쪽에 있어야 해서, 그대로 두면 허공에서
튀어나옵니다. 그래서 `NoteGenerator.Start` Postfix에서 `notePreGenerateTime`을 `3 / 최저배율`로
늘립니다. `private readonly`지만 인스턴스 필드라 리플렉션 `SetValue`가 동작합니다.

### 배율 결정

- `NoteSpeedChaosPerLane=0`: `Timing`을 시드로 노트마다 다른 배율(완전 카오스)
- `NoteSpeedChaosPerLane=1`: 부모 `NoteGroup`(레인)의 인스턴스 ID(`% 1000`)를 시드로 레인마다 다른 배율.
  같은 레인 안에서는 순서가 유지되므로 읽을 수는 있습니다.

노트별 모드는 `Timing`이 시드라 결정론적이고, 리트라이해도 같은 패턴이 나옵니다. 레인별 모드는 Unity 인스턴스 ID가
씬을 다시 불러올 때마다 달라지므로 **리트라이할 때마다 레인 배율이 바뀔 수 있습니다**.
배율은 노트 인스턴스 ID로 캐시하고 씬 전환 때 `NoteSpeedChaosHook.Reset()`으로 비웁니다.
`Min > Max`로 적어두면 설정 로드 시 두 값을 맞바꿉니다.

### 판정과의 관계

`NoteSway`와 마찬가지로 판정은 `Note.timing`과 시간만 비교하므로 **정확도 자체에는 영향이
없습니다**. 어렵게 느껴지는 것은 순전히 읽기 난이도 때문입니다.
