# 트러블슈팅(모음) - sxtg2 기준

`DOCUMENTATION.md`에도 트러블슈팅이 있지만, 실제로는 케이스가 빠르게 늘어나기 때문에 별도 문서로 분리합니다.

## 1) 커스텀 차트가 주입되지 않음

### 체크 1: `hwa` 폴더가 존재하나요?

- 현재 `sxtg2`는 `hwa` 폴더를 자동 생성하지 않습니다.
- `{게임 설치 폴더}\hwa\`가 없으면 BMS/미디어 탐색이 실패합니다.

### 체크 2: 플레이 로딩에서 “커스텀 차트” 텍스트가 감지되나요?

- `TextHook`는 Play/Loading 씬에서 `"커스텀 차트"` 문자열을 감지하면 트랙 기반 BMS 파싱을 시도합니다.
- 로그에 `[TextHook] '커스텀 차트' 텍스트 감지!`가 나오는지 확인하세요.

### 체크 3: 플레이 시작 훅에서 최종 주입이 호출되나요?

- `ManagerPlayHook` 로그에서 `set_bms`/`FetchBMSToModules`/`GetPatternFromDir` 호출을 확인하세요.
- 이후 `[SXGTDataHook] ProcessPendingNoteRemovalAndInjection 호출됨`이 나와야 실제 주입이 진행됩니다.

## 2) 홀드 노트가 이상함(길이 0, tickTime 없음)

- 원인 후보
  - BMS에서 02-03(또는 04-05) 끝노트가 누락됨
  - HoldNote 생성자 선택이 실패하여 duration/tickTime이 제대로 들어가지 않음
- 확인 포인트
  - 초기화 시 출력되는 “끝노트 누락 통계” 확인 (`Main.ScanAndParseBmsFiles`)
  - `CustomChartInjector` 로그에서 HoldNote duration/tickTime 생성자 사용 성공 여부 확인

## 3) BGA/BGM이 교체되지 않음

- 플레이 시작 훅에서 `BGAPlayerHook/BGMPlayerHook`가 1회 교체를 시도합니다.
- 교체가 늦거나 실패하면:
  - BGA: `VideoPlayer`를 아직 못 찾은 상태일 수 있음(나중에 다시 호출되지 않으므로, 플레이 시작 시점에서 찾기 실패하면 교체가 안 될 수 있음)
  - BGM: 대상 `AudioSource` 선택에 실패했거나 로드 실패(`UnityWebRequest` 오류)

## 4) BGA와 BGM 싱크가 계속 어긋남

- `BGABGMSyncHook`는 **짧은 기준 간격**으로 체크하며, 오차 범위에 따라 두 가지 방식으로 보정합니다.
  - **소-중간 오차 구간**: 재생 속도를 조절하는 **Soft Sync** (튀지 않고 부드럽게)
  - **큰 오차 구간**: 시간을 강제로 맞추는 **Hard Sync**
- BGA 또는 BGM 중 하나가 재생 중이 아니면 동기화가 작동하지 않습니다.

## 5) Score/클리어 사운드가 이상함

- 스코어 제한 해제는 `MaxScore/targetBestScore/SXGTData` 보정이 결합되어 동작합니다.
- 클리어 사운드는 `clear*`를 감지해 `KeyBlue_Tam`으로 교체합니다.
- 만약 다른 사운드까지 바뀌면 clipName 탐지 로직이 “clear”를 과하게 잡는지 로그로 확인하세요.





