# 플레이 오버레이 (판정바 / 키뷰어)

플레이 씬 위에 IMGUI(`OnGUI`)로 그려지는 두 개의 오버레이를 설명합니다. 게임의 UI 캔버스를
건드리지 않고 MelonLoader의 `OnGUI` 콜백에서 직접 그리므로, 게임 원본 UI 계층에는 영향이 없습니다.

- 구현: `sxtg2-mod/Features/JudgmentBarFeature.cs`, `sxtg2-mod/Features/KeyViewerFeature.cs`
- 진입점: `sxtg2-mod/Main/Main.cs`의 `OnUpdate()` / `OnGUI()`
- 표시 여부: `SaveCustomKey/config.txt`의 `EnableJudgmentBar`, `EnableKeyViewer`
  (설정 파일 전체 설명은 `01-user-guide/INSTALL_AND_LAYOUT.md` 6절 참고)

두 오버레이 모두 `AutoPlayHook.IsPlayScene`이 참일 때만 갱신/렌더링됩니다. 이 플래그는
`Main.UpdatePlaySceneState()`가 씬 이름(`play` / `rhythm` / `game` 포함 여부)으로 판별합니다.

---

## 1) 판정바 (JudgmentBar)

노트를 칠 때마다 실제 판정 오차(ms)를 눈금 위에 틱으로 남기고, 마지막 히트의 수치를 텍스트로 표시합니다.

### 데이터 출처

게임은 노트 판정이 확정되면 모든 `PlayWidget`에 판정 결과를 브로드캐스트합니다.

```text
RG_PS_Judgement.TryJudgeShortNote(judgeTime, note)
  judgeTime -= userData.adjustSync / 1000      // 유저 싱크 보정 적용
  -> ManagerPlay.WidgeInvoke(pw => pw.OnGetJudge((EJudges)i, note.timing - judgeTime))
```

`FastSlowMeter_OnGetJudge_Patch`가 이 `OnGetJudge(EJudges, float)`를 Postfix로 후킹해서
**판정 등급(`EJudges`)** 과 **오차(초)** 를 받습니다. 즉 화면에 뜨는 ms는 게임이 실제 판정에 사용한
값 그대로이며, 싱크 보정도 이미 반영된 값입니다.

부호 규칙은 게임 원본 `FastSlowMeter`와 동일합니다 — **양수 = FAST(빠르게 침)**, 음수 = SLOW.

### 판정 범위는 난이도마다 다름

색과 눈금은 하드코딩된 상수가 아니라 게임이 주입한 실제 판정 범위를 따릅니다.
`ManagerPlay.Set()`이 난이도별 `JudgeBalancer.BalanceList[(int)lv]`를 `RG_PS_Judgement.SetJudgeRange()`로
넣어주고, 모드는 `JudgmentBar.RefreshJudgeRange()`가 매 프레임
`ManagerPlay.Instance.judgeModule.JudgeRange`를 읽어 ms 단위로 캐시합니다.

| 난이도 | BLUESTAR | WHITESTAR | YELLOWSTAR | REDSTAR |
| --- | --- | --- | --- | --- |
| Comet | 72ms | 119ms | 170ms | 204ms |
| Nova | 54ms | 102ms | 153ms | 204ms |
| SuperNova / Quasar | 36ms | 85ms | 136ms | 187ms |

판정 모듈이 아직 준비되지 않은 프레임에는 SuperNova/Quasar 기준 폴백값을 사용합니다.

### 표시 규칙

- 틱 색: 게임이 매긴 판정 등급 그대로 — BLUESTAR 파랑 / WHITESTAR 흰색 / YELLOWSTAR 노랑 / REDSTAR 빨강
- 배경 박스: BLUESTAR / WHITESTAR / YELLOWSTAR 경계를 각각 겹쳐 표시
- 바 전체 폭: REDSTAR 경계(= 판정이 잡히는 최대 오차)에 맞춰 자동 스케일
- 텍스트: `+32.4 ms · BLUESTAR (FAST)` 형식 (소수점 1자리)
- 틱과 텍스트는 1.5초에 걸쳐 서서히 사라집니다.
- 배치: `JudgmentBarVertical=1`이면 화면 좌측 세로 바(x=60), `0`이면 화면 정중앙 가로 바
- 모양: `JudgmentBarShape`로 `0`(사각 바), `1`(알약/캡슐), `2`(삼각/다이아몬드) 중 선택할 수 있습니다.
  - 기존 `JudgmentBarCapsule=1` 설정과 100% 하위 호환됩니다.
  - 삼각(Triangle) 모양은 중앙(0ms)에서 너비가 가장 넓고 양끝(±MaxMs) 오차 한계선으로 갈수록 뾰족해지는 다이아몬드 마스크 텍스처(`GetTriangleTexture`)를 생성하여 안티에일리어싱 처리됩니다.
  - `JudgmentBarRangeShape` (`-1` = 추종, `0` = 사각, `1` = 알약, `2` = 삼각) 설정으로 안쪽 등급 범위 박스(BLUESTAR/WHITESTAR/YELLOWSTAR)의 모양도 통일하거나 다르게 바꿀 수 있습니다.
- 좌우 위치: `JudgmentBarSide`로 `Left`(화면 왼쪽 가장자리, 여백 60px) / `Right`(화면 오른쪽
  가장자리, 여백 60px) / `Center`(기본값 — 세로 바는 기존처럼 왼쪽 고정, 가로 바는 화면 정중앙)
  중 선택. 세로 바를 `Right`로 두면 히트 텍스트 라벨도 자동으로 바 왼쪽으로 붙어서 화면 밖으로
  나가지 않음.

### 향후 계획: 등급별 누적 판정 카운터

**계획 중**: BLUESTAR/WHITESTAR/YELLOWSTAR/REDSTAR 4개 등급의 누적 개수를 플레이 중
실시간으로 보여주는 위젯을 추가할 예정 (판정바처럼 `EnableJudgmentBar`류 토글로 켜고 끌 수
있게 만들 계획).

- 데이터는 게임 원본에 이미 다 쌓이고 있음: `RG_PS_Judgement.JudgeCount`
  (`sxtg2/RhythmGame.Play/RG_PS_Judgement.cs`)가 `JudgeCounter` 타입(`sxtg2/JudgeCounter.cs`,
  `int[4]` 배열)이고, 노트 판정마다 `JudgeDivergence()`에서 `JudgeCount.AddJudge(j)`로 누적됨.
  `JudgeCount[(int)EJudges.BLUESTAR]`처럼 등급별로 바로 읽을 수 있음.
- 원본 게임에는 REDSTAR(미스)만 실시간으로 보여주는 `RedStarCounter`
  (`sxtg2/RhythmGame.Play/RedStarCounter.cs`, `PlayWidget` 상속, `"Red : N"` 텍스트)가 있고
  BLUESTAR/WHITESTAR/YELLOWSTAR용 라이브 위젯은 없음 — 새로 만들 위젯은 이 4개 등급을 전부
  다루는 확장판 개념.
- 구현 방향은 `JudgmentBar`처럼 `OnGUI`에서 직접 그리는 방식이 유력. 데이터 소스는
  `FastSlowMeter_OnGetJudge_Patch`(같은 파일, `RG_PS_Judgement.JudgeDivergence` 후킹)를
  그대로 재사용하거나, `RG_PS_Judgement.Instance.JudgeCount`를 매 프레임 직접 읽어도 됨.

### 정밀도 한계 (게임 원본 특성)

`ManagerPlay.CurTime`은 오디오 클럭이 아니라 `Time.time` 기반으로 계산되고
(`CurTime = Time.time - SumPausedTime - playSceneStartTime`), 키 입력도 `Update` 폴링으로 처리됩니다.
따라서 판정 시각의 분해능은 프레임 단위(60fps면 약 16.7ms)로 양자화되어 있습니다.
표시되는 ms는 "게임이 매긴 판정과 동일한 값"이라는 의미에서 정확한 것이지, 물리적 절대 오차는 아닙니다.

### 알려진 주의점

`OnGetJudge`는 `WidgeInvoke`를 통해 **모든 `PlayWidget`에 브로드캐스트**됩니다. 또한
`JudgeTextViewer`는 2-인자 버전을 오버라이드하지 않아, 후킹 대상 탐색 시 상속된
`PlayWidget.OnGetJudge(EJudges, float)` 베이스 메서드가 잡힐 수 있습니다. 이 경우 히트 1회가
여러 번 등록될 수 있습니다.

판정바는 같은 값이 같은 위치에 겹쳐 그려지므로 표시상 문제가 없지만, 앞으로 이 데이터를
**통계(예: 결과 화면 평균 오차/표준편차)** 로 집계한다면 노트 수가 배수로 부풀 수 있습니다.
그때는 후킹 지점을 `RG_PS_Judgement.TryJudgeShortNote`(노트당 1회 실행) 쪽으로 옮겨야 합니다.

---

## 2) 키뷰어 (KeyViewer)

플레이 씬 하단 중앙에 7개 레인의 실시간 입력 상태를 표시합니다.

### 레인 구성

화면 좌 → 우 순서로 `LT LL L GATE R RR RT` (`SixtarInput.RhythmGame_*`)입니다.
게임 내부 레인 인덱스(`LaneIndex`)와는 순서가 다르며, 키뷰어는 화면 배치 순서를 따릅니다.

### 키 라벨

`UserAccountModule.Instance.userData.keySetting.GetKeyFromLane()`에서 실제 `KeyCode`를 읽어오므로,
게임 설정에서 키를 리매핑하면 라벨도 따라 바뀝니다. 씬이 바뀔 때
`KeyViewer.Reset()`으로 캐시를 비우고, 다음 폴링에서 다시 읽습니다.

라벨은 `Alpha1` → `1`, `Space` → `SPC`, `LeftShift` → `LSFT`처럼 축약해서 표시하고,
바인딩이 `KeyCode.None`인 레인(예: 사용하지 않는 GATESUB)은 `-`로 흐리게 표시합니다.

### 입력 감지

`Main.OnUpdate()`에서 `KeyViewer.Poll()`이 `Input.GetKey()`로 직접 폴링합니다.
게임의 `ManagerPlay.LaneTouchStates`를 쓰지 않는 이유는, 그 값이 일시정지(`isOnPause`)나
게이트 미개방(`gear.IsGateOpened == false`) 상태에서 갱신되지 않아
키를 눌러도 false로 남기 때문입니다. 키뷰어는 물리 입력을 그대로 보여주는 쪽이 맞습니다.

### 표시 규칙 및 색상 커스터마이징

- 박스 크기 44×44, 간격 6, 화면 하단에서 56px 위
- `config.txt`에서 커스텀 색상 지정 가능:
  - `KeyViewerPressedColor`: 일반 노트 키 입력 시 배경색 (기본값 `#26BFD9D9` / 시안)
  - `KeyViewerNormalColor`: 미입력 상태 배경색 (기본값 `#141414A6` / 반투명 다크)
  - `KeyViewerGatePressedColor`: 중앙 GATE 키 입력 시 전용 배경색 (기본값 `#FF4081E6` / 마젠타)
- 색상 표기 지원 포맷:
  - HEX 헥스코드: `#00FFCC`, `#26BFD9D9`
  - RGBA 수치: `255,128,0`, `0.15,0.75,0.85,0.85`
  - **한글 색상명 지원**: `시안`, `마젠타`, `노랑`, `빨강`, `파랑`, `초록`, `흰색`, `검정`, `주황`, `보라`, `분홍`, `하늘색`, `민트` 등
- 렌더링 순서상 판정바보다 나중에 그려지므로, 겹칠 경우 키뷰어가 위에 옵니다.
  (기본 좌표에서는 겹치지 않습니다 — 세로 판정바와는 창이 868×500 미만,
  가로 판정바와는 창 높이 224px 미만일 때만 겹칩니다.)
