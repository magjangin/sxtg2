# 현재 상태

기준일: 2026-07-27

## 현재 결론

프로젝트는 폐기 상태가 아니라 정리 가능한 상태입니다. 최근 정리로 미사용 파일과 진단용 후킹을 제거했고, 빌드/로직 테스트/실게임 동작 확인까지 완료했습니다.

## 검증된 상태

- `dotnet build sxtg2-mod/sxtg2.csproj --configuration Debug` 성공 (경고 0개)
- `sxtg2.LogicTests` 4개 통과
- 실제 게임에서 커스텀 차트 흐름 정상 동작 확인

## 2026-07-27 추가: 노트 흔들림 연출 (NoteSway)

- **추가한 것**: 노트가 눈송이처럼 좌우로 흔들리며 내려오는 시각 효과.
  `Hooks/GameplayHooks.cs`의 `NoteSwayHook`이 `RG_NoteObject.CalculatePosition` Postfix에서
  **루트 RectTransform**의 x를 밀어준다. config의 `NoteSway`로 켜며 기본은 꺼짐.
- 자식(`shortNote`/`holdMask`/`tailNote`)이 아니라 루트를 미는 이유: 홀드 몸통이 단일
  RectTransform이라 S자로 휠 수 없고, `holdMask`만 옮기면 `holdTexture`가 상대적으로 밀려
  무늬만 반대로 미끄러져 보인다. 루트를 밀면 헤드·몸통·꼬리가 한 덩어리로 움직인다
  (최신 게임 버전의 연출도 뻣뻣한 막대가 통째로 움직이는 형태라고 사용자가 확인해줌).
- 흔들림은 곡 진행 시간 기준 사인파이고, 위상은 `Timing`을 시드로 흩뿌려 노트마다 다르다
  (결정론적 — 리트라이해도 궤적 동일). 감쇠를 켜면 판정선 근처에서 진폭이 0으로 수렴한다.
- 판정은 `Note.timing`과 시간만 비교하므로 이 연출은 정확도에 영향이 없다.
- **검증**: `dotnet build` 성공(경고 0개), 로직 테스트 6개 통과, 게임 `Mods/`에 배포 완료.
  실게임 확인 **미완료** — `Lane` 프리팹에 `RectMask2D`가 있으면 진폭이 클 때 노트가 잘릴 수
  있는데 이는 코드로 확인이 불가능하므로 실제 화면을 보며 진폭을 조정해야 한다.
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`("노트 흔들림 연출" 절),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

## 2026-07-27 추가: 설정 파일로 점수 상한 지정

- **추가한 것**: `SaveCustomKey/config.txt`의 `MaxScore` 항목으로 만점 기준값을 지정할 수 있게 함.
  새 훅 `JudgeScoreMaxHook`(`Hooks/GameplayHooks.cs`, Harmony Transpiler)이
  `RG_PS_Judgement.Update()` / `CalculateJudgeScore(float)`에 리터럴로 박힌 `1000000f`를 교체.
- 상수를 새 값으로 굽지 않고 `GetMaxScore()` 호출로 바꿔서, 설정 로드와 패치 적용 순서에
  관계없이 항상 최신 설정값을 읽도록 함. `MaxScore`가 기본값이면 `Prepare()`가 `false`를 반환해
  패치를 아예 붙이지 않음.
- 이전 버전에서 만들어진 `config.txt`에는 `MaxScore` 항목이 없으므로, 없으면 파일 끝에 기본값 줄을
  자동으로 덧붙임.
- **검증**: `dotnet build` 성공(경고 0개), 로직 테스트 6개 통과, 게임 `Mods/`에 배포 완료.
  실게임에서 점수 상한이 실제로 바뀌는지는 **미확인** — 값을 바꿔 플레이 확인 필요.
- 자세한 내용: `02-systems/SCORE_SYSTEM.md`("점수 상한 설정" 절),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

## 2026-07-26 추가: 플레이 씬 키뷰어 (v0.1.4)

- **추가한 것**: 플레이 씬 하단 중앙에 7개 레인(LT LL L GATE R RR RT)의 실시간 입력 상태를 표시하는
  오버레이. 새 파일 `Features/KeyViewerFeature.cs`, `Main.OnUpdate()/OnGUI()`에서 호출.
- 키 라벨은 `UserAccountModule.Instance.userData.keySetting.GetKeyFromLane()`에서 읽으므로 게임에서
  키를 리매핑하면 그대로 반영됩니다. 입력 감지는 `Input.GetKey()` 직접 폴링 — 게임의
  `ManagerPlay.LaneTouchStates`는 일시정지/게이트 미개방 상태에서 갱신되지 않아 키뷰어 용도로는 부정확함.
- `config.txt`의 `EnableKeyViewer` 항목은 이전부터 파싱만 되고 소비하는 코드가 없는 죽은 플래그였는데,
  이번에 실제로 동작하게 됨.
- **검증**: `dotnet build` 성공(경고 0개), 실게임에서 표시 확인 완료.
- 자세한 내용: `02-systems/PLAY_OVERLAY.md`, `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

## 2026-07-26 수정: 판정바가 실제 게임 판정과 어긋나던 문제 (v0.1.4)

- **증상**: 표시되는 ms 값 자체는 맞았지만, 색과 라벨이 실제 판정 등급과 일치하지 않았음.
- **원인**: 판정바가 `±30ms = Perfect`, `±70ms = Great`로 **하드코딩**되어 있었음. 실제 판정 범위는
  `ManagerPlay.Set()`이 난이도별 `JudgeBalancer.BalanceList[(int)lv]`를 주입하는 구조라
  Comet 72ms / Nova 54ms / SuperNova·Quasar 36ms(BLUESTAR 기준)로 전부 다름. 즉 어떤 난이도에서도
  하드코딩 값과 맞지 않았고, Comet에서 50ms로 친 BLUESTAR가 판정바에서는 FAST/SLOW로 표시됨.
- **수정**:
  - 틱 색을 오차 크기로 추정하지 않고, `OnGetJudge`가 넘겨주는 `EJudges` 값을 그대로 사용
    (BLUESTAR 파랑 / WHITESTAR 흰색 / YELLOWSTAR 노랑 / REDSTAR 빨강).
  - `JudgmentBar.RefreshJudgeRange()`가 `RG_PS_Judgement.JudgeRange`를 런타임에 읽어 배경 박스와
    바 전체 스케일을 난이도에 자동으로 맞춤.
  - 오차 텍스트를 소수점 1자리 + 판정 등급명으로 변경(`+32.4 ms · BLUESTAR (FAST)`). 기존 `F0` 반올림
    표시가 게임 화면 숫자(`Mathf.Floor`)와 1ms 어긋나 보이던 문제도 해소.
- **미해결로 남긴 것**: `OnGetJudge`는 `WidgeInvoke`로 모든 `PlayWidget`에 브로드캐스트되고
  `JudgeTextViewer`가 2-인자 버전을 오버라이드하지 않아, 히트 1회가 중복 등록될 수 있음. 판정바 표시에는
  영향이 없지만(같은 값이 겹쳐 그려짐) 결과 화면 통계로 집계할 계획이라면
  `RG_PS_Judgement.TryJudgeShortNote`(노트당 1회) 후킹으로 옮겨야 함.
- **검증**: `dotnet build` 성공(경고 0개), 실게임에서 판정 등급별 색이 정상 표시되는 것 확인 완료.
- 자세한 내용: `02-systems/PLAY_OVERLAY.md`

## 2026-07-18 추가: InventoryPopup 모드의 노트 스킨 기능 이식

- **배경**: 별도 저장소 `H:\source\repos\InventoryPopup`에 있던 커스텀 노트 스프라이트(스킨) 교체
  기능을 sxtg2-mod로 통합해달라는 요청.
- **가져온 것**: `RhythmGame.NoteGenerator.Generate` 후킹 → 생성된 `RG_NoteObject`의
  `shortNote`/`tailNote`/`holdTexture` 필드에 `CustomNotes` 폴더의 PNG 스프라이트를 적용 → UI 렌더러
  강제 갱신. 새 파일: `Loaders/CustomNoteSpriteLoader.cs`, `Hooks/Note/NoteSpriteHook.cs`,
  `Helpers/UI/NoteRendererRecovery.cs`.
- **가져오지 않은 것**: `InventoryPopup`에 있던 진단용 코드(모든 public 메서드를 무차별 후킹해서 로깅,
  `NoteAnalyzer`/`SceneAnalyzer`/`UIRendererAnalyzer`, `Setter_NoteSpeed` 후킹 등)는 실제 기능이 아니라
  분석 도구였으므로 이식하지 않음. 하드코딩된 게임 경로(`H:\Sixtar Gate STARTRAIL custom mode`)도
  sxtg2 관례대로 `Application.dataPath` 기반 동적 경로로 교체.
- **검증**: `dotnet build` 성공(경고 0개). 실게임 동작 확인은 아직 안 됨 — `CustomNotes` 폴더에 PNG를
  넣고 플레이해서 확인 필요.
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`("노트 스킨" 절), `02-systems/HOOK_SYSTEM.md`(NoteSpriteHook),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(5절, CustomNotes 폴더 규칙)

## 2026-07-18 추가: SaveCustomKey 폴더 생성 로직

- `{게임 설치 폴더}\SaveCustomKey\` 폴더를 모드 초기화 시 자동 생성하는 로직만 추가함
  (`Helpers/SaveCustomKeyFolderHelper.cs`). 아직 이 폴더를 실제로 읽고 쓰는 기능은 없음 — 향후
  커스텀 키 프리셋 파일 저장/불러오기 기능을 위한 준비 단계.
- 자세한 내용: `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

## 2026-07-18 수정: 게임 종료 시 "씬 감지 모드 정리 중 오류" 에러

- **증상**: 게임을 종료할 때마다 `MelonLoader\Latest.log`에 빨간 ERROR 줄로
  `씬 감지 모드 정리 중 오류 발생: Operation is not valid due to the current state of the object.`가 찍힘.
- **근본 원인**: 스택 트레이스를 추가해 확인한 결과, `System.MulticastDelegate.RemoveImpl` →
  `SceneManager.remove_sceneLoaded`에서 발생하는 `InvalidOperationException`. Unity가 사용하는 구형
  Mono 런타임에서 `OnApplicationQuit` 시점에 정적 이벤트 구독을 해제하려 할 때 나타나는 런타임 레벨
  특성으로, 모드 로직 버그가 아님.
- **판단**: `SceneDetector.Cleanup()`은 `Main.OnApplicationQuit()` 단 한 곳에서만 호출되고 있었고,
  프로세스가 종료되는 시점에 이벤트 구독을 해제하는 것 자체가 아무 효과가 없음(그 이후 `sceneLoaded`가
  다시 발화할 일이 없음). 즉 이 호출은 아무 이득 없이 Mono의 취약한 내부 경로만 건드리고 있었음.
- **수정**: 로그 레벨을 낮추거나 예외를 조용히 삼키는 방식 대신, 애초에 의미 없던
  `Main.OnApplicationQuit()`과 `SceneDetector.Cleanup()`을 완전히 제거함.

## 2026-07-18 수정: 커스텀 차트 스코어/클리어 사운드 타이밍 버그

- **증상**: 커스텀 차트 플레이 중 곡이 다 끝나기도 전에(또는 이상한 시점에) `Clear_FullCombo`/
  `Clear_Normal` 사운드가 재생됨.
- **근본 원인**: `SXGTData.totalNotes`/`totalNoteWithTicks`가 도너(복제 원본) 트랙의 노트 개수로 남아있었고,
  `CustomChartInjector`는 `laneData`(노트 리스트)만 갈아끼울 뿐 이 개수 필드는 갱신하지 않았음. 게임의
  스코어 계산(`JudgeScore`)과 곡 종료 판정(`elapsedNote >= totalNoteWithTicks`)이 전부 이 값을 기준으로
  동작하기 때문에, 실제 커스텀 차트와 도너 트랙의 노트 수가 다르면 판정이 어긋남.
- **기존에 있던(효과 없던) 시도**: `maxScore`/`MaxScore` 필드를 -1로 바꾸는 보정이 있었지만, 애초에
  스코어 계산식이 그 필드를 참조하지 않아 무의미했고, 클리어 사운드를 `KeyBlue_Tam`으로 바꾸는 임시 후킹도
  증상만 가리고 있었음.
- **수정**: `CustomChartInjector`가 노트를 레인에 실제로 주입하면서 성공한 노트 수를 직접 세어(레인 9/10
  제외, 홀드 노트는 `tickLength`만큼 가산) 주입 완료 직후 `SXGTData.totalNotes`/`totalNoteWithTicks`를
  덮어쓰도록 함. 실게임 테스트로 확인 완료.
- **후속**: 근본 원인이 고쳐지면서 `Clear_FullCombo`/`Clear_Normal`을 `KeyBlue_Tam`으로 가리던 임시방편이
  불필요해짐을 실게임에서 확인. `SoundObject.Play`/`PlayAndDestroy`를 후킹하던 클리어 사운드 교체 코드를
  삭제하고, 부트 초반 Resources 전체 스캔도 제거함.
- 자세한 내용: `02-systems/SCORE_SYSTEM.md`, `01-user-guide/TROUBLESHOOTING.md` (5번 항목)

## 최근 정리 내역

- 미사용/비활성 C# 파일 제거
  - `NumberInterpolatorHook`
  - `CustomAlbumManager`
  - `DictionaryHelper`, `InitReport`, `ResourceManagerHelper`
  - `NoteGroupProcessor`, `NoteMatcher`, `NoteDataExtractor`
  - `Models/*`, `Manipulators/*`
- 진단성 후킹 제거
  - `SXGTReaderHook.*`
  - `MusicSelectAnalyzer.Diagnostics`
  - `SXGTDataHook.Diagnostics`
- 초기 진단 스캔 제거
  - `InitialMediaFileScanner`
- 중복 책임 정리
  - `BmsFileResolver` 추가
  - BMS 파일 확장자와 파일 선택 규칙 통합
- 불필요한 경고 제거
  - `ManagerPlay.targetBestScore` 보정 제거
- 2026-07-18 불필요 코드 정리
  - `HighscoreMeterHook` 및 `SoundObjectClipAccessor` 삭제
  - `SXGTDataHook.Score`의 무효한 `maxScore` 보정 삭제
  - ESC 강제 일시정지 호출/탐색 코드 삭제
  - 원본 `PauseGame` 이후 커스텀 자켓 적용만 유지

## 현재 핵심 흐름

```text
MusicSelect
  -> TrackDataAnalyzer가 커스텀 TrackData 주입
  -> ManagerMusicSelectHook이 선택 트랙/썸네일/프리뷰 처리

PlayLoading / PlayStart
  -> TextHook 또는 CustomPlayStartupFlow가 BMS 로드
  -> BmsFileResolver가 BMS 파일 선택
  -> BmsParser가 파싱
  -> CustomChartInjector가 노트 생성 준비

SXGTData / ManagerPlay
  -> SXGTDataHook가 원본 노트 제거
  -> CustomChartInjector가 laneData에 커스텀 노트 주입
  -> BGAPlayerHook/BGMPlayerHook이 미디어 교체
  -> BGABGMSyncHook이 BGA/BGM 동기화
```

## 남은 주의점

- `TextHook` 기반 `"커스텀 차트"` 텍스트 감지는 아직 넓은 후킹입니다.
- `MusicSelectAnalyzer`는 여전히 씬 전체 탐색을 일부 수행합니다.
- `FindObjectsOfType` 기반 탐색은 모딩 특성상 남아 있지만, 자주 호출되는 경로인지 계속 확인해야 합니다.
- `02-systems/`의 일부 상세 문서는 과거 코드 설명이 남아 있을 수 있습니다.
