# 현재 상태

기준일: 2026-09-29 (코드 v1.1.0, 커밋 `0bde1f8`)

## 현재 결론

커스텀 차트 흐름, 판정바/키뷰어, 설정 파일(플레이마다 재로드)이 동작하는 상태입니다. 2026-09-28 전체 점검에서
코드 버그 후보 몇 가지를 찾았고(아래 "알려진 문제"), **코드는 아직 고치지 않았습니다**. 문서는 같은 날 현재 코드
기준으로 다시 정리했습니다. 2026-09-29에 코드 전체와 디컴파일 원본을 다시 대조해 #13~#21과 사소한 것 목록을
추가했습니다(기록만 했고, 이 중 #18만 같은 날 코드를 고쳤습니다 — "2026-09-29 수정" 절 참고).

## 검증된 상태

- `sxtg2.LogicTests` 7개 통과 (2026-09-28 재실행 확인)
- `dotnet build` 성공 (경고 0개) — 2026-08-09 v1.1.0 커밋 시점 기록, 2026-09-28에는 다시 빌드하지 않음
- 실제 게임에서 커스텀 차트 흐름, 판정바 등급 색, 키뷰어 표시, v1.1.0 설정 재로드(`MaxScore` 1000000 → 2000000이
  재시작 없이 다음 플레이에 반영) 확인

## 알려진 문제 (2026-09-28 / 09-29 점검, 코드 미수정)

디컴파일 원본(`sxtg2/`)과 대조하거나 파서를 직접 실행해서 확인한 것들입니다. "확인 필요"는 실게임 확인이 아직 없는 것입니다.
#1~#12는 09-28, #13~#21은 09-29, #22~#25는 10-03 재점검에서 추가했습니다. 수정된 항목은 행 앞에 **(날짜 수정됨)**으로 표시했습니다.

| # | 문제 | 위치 | 영향 |
| --- | --- | --- | --- |
| 1 | 끝(`03`/`05`) 없는 홀드가 `tickTime = null`인 `HoldNote`로 주입됨 → 게임 `CheckHoldTick`에서 매 프레임 `NullReferenceException` (확인 필요) | `Processors/CustomChartInjector.cs` `CreateNote` | 그 노트를 치는 순간부터 판정 루프 나머지(미스 판정·점수·오토플레이) 중단 |
| 2 | 노트 스킨 파일명 매칭 회귀: 노트 이름 `_Blue(Clone)`에서 `Blue(Clone)`을 뽑아 `Blue.png`가 매칭 안 됨 (`12348f5`에서 로더 단순화 때 발생) | `Loaders/CustomNoteLoaders.cs` `ExtractNoteType` | 커스텀 노트 스킨 미적용 |
| 3 | `config.txt`의 `BlockSave=0`이 무효 — MelonPreferences `BlockSaveBestRanking`(기본 `true`)과 OR | `Helpers/ModHelpers.cs` `ModLog.BlockSaveBestRanking` | 기록 저장을 켤 수 없음 |
| 4 | BMS 헤더 값의 콜론을 데이터 줄로 오인 (`#TITLE Remix 2011:0101` → 가짜 노트 2개, 실행 확인) | `Loaders/BmsParser.cs` `ParseNoteData` | 곡 시작부 가짜 노트 |
| 5 | 씬 이름에 `play`가 들어가면 플레이 씬으로 판정 → `PlayLoading`도 해당 | `Main/Main.cs` `UpdatePlaySceneState` | 로딩 화면 오버레이, 설정 재로드 2회 |
| 6 | 빌드 스크립트가 `Any CPU`로 빌드 → csproj의 `x64` 조건 블록 미적용 | `build*.bat`, `sxtg2.csproj` | Release 최적화 없음, Debug에 `DEBUG` 상수 없음 |
| 7 | 진단용 로깅 훅 4개가 켜져 있음 (확인창 계층 덤프, 결과 화면 `FindObjectsOfType<GameObject>`) | `Features/MusicSelectFeature.cs` | 로그 증가, 결과 화면 부하 |
| 8 | 자켓이 없으면 요청마다 파일 9개 재확인 + 경고 | `Hooks/GameplayHooks.cs` `TrackDataMediaHook` | 로그 증가 |
| 9 | `music.*`가 없으면 폴더의 첫 오디오(키음일 수 있음)를 BGM/미리듣기로 사용, 미리듣기 클립 미해제 | `Hooks/AudioHooks.cs` `BgmFileResolver` | 엉뚱한 BGM |
| 10 | 모든 노트에 `SetNativeSize()` → 게임 노트 크기 옵션이 무시될 수 있음 (확인 필요) | `Loaders/CustomNoteLoaders.cs` `NoteRendererRecovery` | 노트 크기 |
| 11 | 판정바 히트가 장착 위젯 수만큼 중복 등록, 위젯이 없으면 틱 없음 | `Features/JudgmentBarFeature.cs` | 판정바 표시 |
| 12 | BMS 채널 `02`(마디 길이) 무시, BPM 변화 미지원 | `Loaders/BmsParser.cs` | 해당 차트 타이밍 어긋남 |
| 13 | `BlockSave`가 막는 범위가 좁음: `ResultSaveBlockHook`은 `UserAccountModule.SavePlayData`/`LyrebirdServer.PostUserScore`(10-04 전에는 이 둘을 부르는 `ComparePlayResultHighScore`/`PostRequestPlayResult` 래퍼)만 건너뜀. 원본 `ManagerResult.Start`의 `playCount++`/`failCount++`, `lastPlayedTrackID` 저장, Steam 업적(`PUREBLUE_FIRST`, `FULLCOMBO_FIRST`, `COMET_FIRST` 등)은 그대로 실행됨. `BlockSave`를 끄면(#3 우회법) `GetPlayData`가 `CUSTOM_…` ID로 새 기록을 만들어 `SteamRemoteStorage`(Steam 클라우드)에 저장 | `Hooks/GameplayHooks.cs` `ResultSaveBlockHook`, 원본 `ManagerResult.cs:113,150-161,182-185,236-252` | 커스텀 차트/오토플레이/올퍼펙트/`MaxScore` 변경으로 Steam 업적 해금과 카운터 증가, 차단을 끄면 클라우드 세이브 오염 |
| 14 | 곡을 시작할 때 원본이 `LyrebirdServer.IncreaseTrackPlayCount(CUSTOM_…)`로 커스텀 트랙 ID를 공식 서버(`lyrebirdferdinant.com:3939`)에 보냄. 저장 차단 훅 범위 밖 | 원본 `ManagerMusicSelect.cs:540-544`, `LyrebirdServer.cs:256` | 커스텀 ID가 서버로 나감 |
| 15 | (확인 필요, 게임 업데이트 시) `NoteSpriteHook`의 `static readonly FieldRefAccess`가 필드 이름 변경으로 `TypeInitializationException`을 던지면 `Main.OnInitializeMelon`이 씬 이벤트 구독(`activeSceneChanged`)까지 건너뜀. `NoteSpriteHook.Initialize()`가 구독보다 먼저, 별도 `try` 없이 호출됨. `CustomNotes` 폴더 생성 실패도 같은 결과 | `Main/Main.cs:27-38`, `Hooks/GameplayHooks.cs` | 판정바/키뷰어가 안 나오고 설정 재로드도 멈춤 |
| 16 | (확인 필요) 진단 훅과 핵심 훅이 한 클래스(`ManagerMusicSelectHook`)에 섞여 있고, 진단 전용 `confirmWindow` `FieldRef`가 정적 필드임. 필드가 사라지면 같은 클래스의 `PlayPreviewPrefix`가 예외를 던질 수 있고, 진단 훅 대상 메서드가 바뀌면 클래스 단위 패치 실패가 핵심 패치(`Awake`/`PlayPreview`)로 번질 수 있음 | `Features/MusicSelectFeature.cs` | 커스텀 곡 등록/미리듣기 중단 가능 |
| 17 | 주입 도중 예외가 나면 레인이 이미 비워진 채 반쯤 채워진 상태로 진행함(예외를 삼키고 반환, 호출부는 성공으로 보고 BGM 교체까지 진행, 노트 수는 도너 값) | `Processors/CustomChartInjector.cs` `InjectBmsNotesToLaneData` | 깨진 차트로 플레이 |
| 18 | **(2026-09-29 수정됨)** `EnableJudgmentBar=0`이어도 `RegisterHit`이 계속 `HitHistory`에 추가하는데 정리(`RemoveAll`)는 그리기 함수 안에만 있어 계속 늘어남. `OnGUI`가 프레임당 여러 이벤트로 불리는데 `Repaint` 필터가 없어 판정바/키뷰어가 매번 클로저와 문자열을 할당함 | `Features/JudgmentBarFeature.cs`, `Features/KeyViewerFeature.cs` | 메모리 증가(작음), 불필요한 GC |
| 19 | (확인 필요) 오토플레이가 게임의 `ManagerPlay.autoPlay`(public bool) 대신 private `AutoPlayJudge`를 Postfix에서 직접 호출하고, 시간 캐시 `CurrentTimeSeconds`는 플레이가 아닌 씬으로 갈 때만 -1로 리셋됨 → 씬 이름이 그대로인 리트라이에서 이전 판 값이 남아 첫 프레임에 노트가 조기 판정될 가능성. 메서드를 못 찾으면 매 프레임 `AccessTools`로 다시 찾고 경고도 없음 | `Hooks/GameplayHooks.cs` `AutoPlayHook`, `Main/Main.cs:69-72` | 오토플레이 리트라이 오동작 가능 |
| 20 | `config.txt`의 줄 끝 주석(`AutoPlay=1 # 메모`)은 값이 `1 # 메모`가 되어 경고 없이 기본값으로 무시됨. 오탈자 키도 경고 없음. 옵션 하나를 추가하려면 6곳(기본값, 템플릿, 파싱, Snapshot, 요약, 누락 항목 추가)을 고쳐야 함 | `Helpers/ModHelpers.cs` `LoadConfigFile` | 설정이 조용히 안 먹음 |
| 21 | `trackinfo.txt`를 ANSI(메모장 CP949)로 저장하면 `StreamReader`가 UTF-8로 읽어 `제목:` 키가 깨지고, 제목이 경고 없이 `커스텀 차트`가 됨. 제목이 없으면 모든 곡이 같은 이름 | `Loaders/TrackInfoParser.cs`, `Features/MusicSelectFeature.cs` `CreateCustomTrack` | 곡 정보 누락 |
| 22 | **(2026-10-04 수정됨)** 저장 차단 훅이 `ComparePlayResultHighScore`/`PostRequestPlayResult` 래퍼를 통째로 건너뛰어, 래퍼 끝의 베스트 점수 표시 갱신(`bestscoreIndicator.targetNumber`/`StartUpdator`)까지 사라짐. `BlockSave`가 사실상 항상 켜져 있어 모든 플레이에 해당 | `Hooks/GameplayHooks.cs` `ResultSaveBlockHook`, 원본 `ManagerResult.cs:298-301` | 결과 화면 베스트 점수 표시가 안 돎 |
| 23 | (확인 필요) 모드가 `SXGTData.trackStartTiming`을 세팅하지 않아, BGM/BGA 시작 시각(`CurTime >= trackStartTiming`)이 **도너 패턴의 `0A` 마커 값**에 묶임. 0이 아니면 차트(BMS 0초)와 그만큼 어긋나고, 도너 곡이 바뀌면 모든 커스텀 곡의 싱크가 같이 바뀜. 값은 아직 모름(10-04에 주입 로그에 `trackStartTiming=`을 추가해 확인할 수 있게 함) | `Processors/CustomChartInjector.cs`, 원본 `ManagerPlay.cs:252,256`, `SXGTReader.cs:254-257` | 싱크 어긋남 가능 |
| 24 | **(2026-10-04 수정됨)** 누락 항목 자동 추가(`AppendSectionsMissingFrom`)가 플레이 씬 진입 재로드마다 실행되어, 사용자가 지우거나 주석 처리한 묶음을 모드가 다시 써 넣고 편집기에서 열어 둔 파일과 충돌함 | `Helpers/ModHelpers.cs` `LoadConfigFile` | 설정 파일이 멋대로 바뀜 |
| 25 | (확인 필요, 낮음) `.wav`는 항상 `streamAudio=true`로 로드하는데, 게임은 곡 종료를 `bgm.clip.length`로 판단(`ManagerPlay.cs:179,300`). 스트리밍 클립의 `length`가 처음부터 실제 길이로 나오는지 미확인 | `Hooks/AudioHooks.cs` `BGMPlayerHook.CreateRequest` | `.wav` 앨범에서 곡이 일찍/즉시 끝날 가능성 |

사소한 것: 색상명 `핑크` 미인식(`핑`으로 오타), 기본 `config.txt` 머리말의 "게임 실행 시 적용" 문구가 옛 설명,
켜기/끄기 값에 모르는 단어를 써도 경고 없음, `ParseFlexibleBool` 테스트가 실제 함수가 아닌 복사본을 검증,
테스트 프로젝트의 호출되지 않는 `Inspect*` 메서드, 빈 `clean.bat`, `build.bat` LF 줄바꿈(`goto` 오작동 가능성).

사소한 것 (2026-09-29 추가):

- BMS: `#BPM Infinity`가 `float.TryParse`를 통과함(`> 0`만 검사) → 모든 노트가 0초에 몰림.
- 로그: `ModLog.Verbose($"…")`의 보간 문자열이 로그 레벨과 무관하게 매번 만들어짐(노트 생성/판정마다).
  `AllPerfectJudgeHook`/`FastSlowMeter_OnGetJudge_Patch`의 `TargetMethods()`는 대상이 0개여도 경고가 없음.
- 연출: NoteSway는 진폭이 0이 돼도 x를 `BaseX`로 되돌리지 않음. NoteSpeedChaos 레인별 모드의 시드는
  `InstanceID % 1000`(음수 가능, 실행마다 다름)이라 레인 인덱스 기반이 더 낫음.
- 프로젝트 파일: `sxtg2.sln`의 프로젝트 GUID `{3B0C2BC5…}`가 csproj `ProjectGuid` `{222E1C89…}`와 다름.
  `AssemblyInfo.cs`의 `AssemblyTitle`/`AssemblyProduct`가 옛 이름 `sixgtar3`. 버전 문자열이 `Main.cs`(`MelonInfo`),
  `AssemblyInfo.cs`, README에 중복됨. csproj에 안 쓰는 참조(`System.Data`, `System.Xml.Linq`, `System.Net.Http`)와
  `RuntimeIdentifiers`가 남아 있음.
- 빌드 스크립트: `build.bat`/`build-release.bat`이 156줄짜리 거의 같은 파일, 끝의 `pause`가 자동 실행을 막음,
  `taskkill /IM VBCSCompiler.exe`는 다른 빌드까지 죽이는데 `UseSharedCompilation=false`와 겹침. `.gitattributes` 없음
  (작업 트리 줄바꿈이 파일마다 다름).
- 테스트: `sxtg2.LogicTests`가 `sxtg2.sln`에 없음. 콜론 헤더(#4), 끝 없는 홀드(#1), `TrackInfoParser`, 설정 파서
  (`ParseFloatSetting`/`ParseSideSetting`/`ParseShapeSetting`)는 테스트가 없음.
- 저장소 루트에 `Latest.log`(2026-01), `build_detailed.log`(455KB)가 남아 있음(`.gitignore` 대상이라 커밋은 안 됨).

저장소 정리 후보: `list_managed_games.txt`(개인 Steam 라이브러리 목록, 모드와 무관), `release/sxtg2.dll`(v1.0.0으로
소스보다 오래됨), `tools/merge_partial_classes.py`(끝난 일회성 스크립트). `release/`의 zip/dll은 git에 바이너리로
들어 있으니 GitHub Releases로 옮기는 편이 낫습니다.

### 수정 방향 제안 (2026-09-29, 아직 적용 안 함)

| # | 제안 |
| --- | --- |
| 3, 13 | 함께 설계: 커스텀 트랙, 오토플레이, 올퍼펙트, `MaxScore`≠기본일 때는 항상 차단하고, 원본 곡만 `BlockSave` 설정을 따르게 함. 업적은 `UserAccountModule.RequestAchievementUnlock` Prefix로 같은 조건에서 막음. `config.txt`와 MelonPreferences의 OR 결합은 없애거나 한쪽으로 통일 |
| 14 | `LyrebirdServer.IncreaseTrackPlayCount`(와 `GetHighScoreList`)에 `CustomTrackData` ID면 건너뛰는 Prefix |
| 15, 16 | `NoteSpriteHook.Initialize()`를 씬 구독 뒤로 옮기거나 별도 `try`로 감싸고, `FieldRefAccess`는 지연 해석. 진단 훅은 별도 클래스로 분리(또는 `LogLevel=2`에서만 동작) |
| 17, 1 | 임시 리스트에 노트를 만들고 성공했을 때만 `laneData`를 교체, 실패하면 예외를 올려 도너 패턴 유지. 짝 없는 홀드는 `ShortNote`로 바꾸거나 버리고 경고 로그(`MissingEndNotes`/`OrphanEndNotes`도 로그로 출력) |
| 18 | (적용됨) 판정바가 꺼져 있으면 `RegisterHit` 건너뛰기, 그리기 함수는 `Event.current.type == EventType.Repaint`일 때만 실행 |
| 19 | 플레이 시작 시 `ManagerPlay.Instance.autoPlay = true`로 세팅하는 방식을 검토(게임 기능 재사용). 현재 방식을 유지한다면 씬 전환마다 `CurrentTimeSeconds = -1` |
| 20 | 줄 끝 `#`/`//` 주석 제거 후 파싱, 모르는 키와 값에는 경고, 키→처리기 표로 6곳 수정을 1곳으로 |
| 21 | 필드를 하나도 못 읽었으면 경고, 기본 제목은 폴더 이름, BMS 헤더 `#TITLE`/`#ARTIST`/`#PLAYLEVEL` 읽기 |
| 23 | 값을 확인한 뒤(주입 로그의 `trackStartTiming=`), 0이 아니면 주입 때 `data.trackStartTiming = 0f`로 명시(또는 BMS에서 오프셋을 받음). 0이어도 도너 곡이 바뀔 때를 대비해 명시하는 편이 안전 |
| 25 | `.wav` 앨범으로 곡 종료 시점 확인. 문제면 `.wav`는 스트리밍하지 않고 통째로 로드 |
| 4 | 채널부(`#`와 `:` 사이)에 공백이 있으면 헤더로 보고 건너뜀 |
| 테스트 | `ParseFlexibleBool` 등 순수 함수를 Unity 무의존 파일로 빼서 모드와 테스트가 같은 소스를 링크. 위 회귀 케이스 추가 |

## 2026-10-04 수정: 저장 차단 위치, 설정 파일 재작성, 주입 로그 (#22, #24, #23 일부)

"확실한 것만" 기준으로 원본(`sxtg2/`)과 대조해 확인된 것만 고쳤습니다. 불확실한 것(#23 값 변경, #25)은 고치지 않았습니다.

- **#22 저장 차단을 말단 메서드로 이동** (`ResultSaveBlockHook`)
  - 이전: 래퍼 `ManagerResult.ComparePlayResultHighScore`/`PostRequestPlayResult`를 통째로 건너뜀 → 래퍼 끝의
    `bestscoreIndicator.targetNumber = …; StartUpdator(…)`(`ManagerResult.cs:300-301`)까지 사라져 결과 화면 베스트 점수가 갱신되지 않음.
  - 이후: 실제로 쓰고 보내는 `UserAccountModule.SavePlayData`와 `LyrebirdServer.PostUserScore`만 건너뜀. 둘 다 원본에서
    `ManagerResult` 말고는 부르는 곳이 없음(`ManagerResult.cs:298`, `:262`). 차단 조건(`BlockSave`/오토/올퍼펙트)은 그대로.
  - 안전 확인: `TrackPlayData`는 곡 ID와 무관하게 `levelDatas` 4칸을 만들고(`TrackPlayData.cs:19-30`), `GetPlayData`는
    저장소에 없으면 새로 만들어 돌려주며(`UserAccountModule.cs:299-308`), `GetUserName()`은 `SteamClient.SteamId`라
    Steam이 필수인 게임에서는 항상 동작. 그래서 커스텀 곡에서 래퍼가 그대로 실행돼도 예외 경로가 없음. 래퍼가 하는
    메모리 상 갱신은 저장되지 않음(`GetPlayData`는 매번 저장소에서 새로 읽음).
  - 대상 메서드를 못 찾으면 시작 시 경고 로그가 남음(예전에는 조용히 차단이 안 걸렸음).
  - 변하지 않은 것: 업적/플레이 횟수/서버 플레이 카운트는 여전히 안 막음(#13, #14).
- **#24 누락 항목 자동 추가는 게임 시작 때만**: `LoadConfigFile`에서 `AppendSectionsMissingFrom`을 `!isReload`일 때만 실행. 재로드는
  파일을 수정하지 않으므로 지우거나 주석 처리한 묶음이 되살아나지 않음(그동안 기본값으로 동작).
- **#23 확인용 로그만 추가**: 주입 로그에 `trackStartTiming=값(도너 값)`을 덧붙임. 값을 바꾸는 것은 도너 값을 확인한 뒤에 결정.
- **검증**: `dotnet build` 성공(경고 0개, 배포 없이 컴파일만), `sxtg2.LogicTests` 7개 통과. 실게임 확인 **미완료** —
  (1) 결과 화면에서 베스트 점수가 표시되고 `[차단] … UserAccountModule.SavePlayData`/`LyrebirdServer.PostUserScore` 로그가 나는지,
  (2) `config.txt`의 묶음을 지운 뒤 플레이를 시작해도 파일이 다시 쓰이지 않는지, (3) 주입 로그의 `trackStartTiming` 값을 확인해 주세요.

## 2026-09-29 수정: 판정바 히트 목록 누적과 OnGUI 낭비 (#18)

- **증상**: `EnableJudgmentBar=0`이어도 판정 훅이 `JudgmentBar.RegisterHit`을 계속 불러 `HitHistory`가 플레이 내내(세션 내내)
  늘어남. 정리(`RemoveAll`)는 `DrawJudgmentBar` 안에만 있는데, 꺼져 있으면 그 함수가 정리 전에 반환하기 때문. 또 `OnGUI`가
  프레임당 여러 이벤트(Layout/Repaint 등)로 불리는데 판정바/키뷰어가 이벤트마다 처리해 클로저와 문자열을 매번 할당함.
- **수정**
  - `JudgmentBar.RegisterHit`: `EnableJudgmentBar`가 꺼져 있으면 바로 반환(설정은 플레이 씬 진입 때만 바뀌므로 한 판 안에서는 일관됨).
  - `JudgmentBar.DrawJudgmentBar`, `KeyViewer.Draw`: `Event.current.type != EventType.Repaint`이면 바로 반환.
    실제로 그려지는 건 Repaint뿐이라 화면은 그대로이고, 나머지 이벤트의 정리/텍스트 조립/`GUI.*` 호출이 사라짐.
  - `DrawJudgmentBar`의 `duration`을 `const`로 바꿔 `RemoveAll` 람다가 지역 변수를 캡처하지 않게 함(호출마다 클로저 할당 제거).
- **검증**: `dotnet build` 성공(경고 0개, 배포 없이 컴파일만), `sxtg2.LogicTests` 7개 통과. 실게임 확인 **미완료** —
  판정바/키뷰어가 예전과 똑같이 그려지는지, `EnableJudgmentBar=0`에서 판정바가 안 나오는지 플레이해서 확인 필요.
- 이 시점에는 다른 #13~#21 항목이 코드 수정 전이었습니다(이후 10-04에 #22, #24를 수정 — 위 절 참고).

## 2026-09-29 추가 점검 (문서만 기록)

- **범위**: `sxtg2-mod/` 13개 파일 전체, `sxtg2.LogicTests`, 빌드 스크립트, 문서 전체를 읽고 디컴파일 원본
  (`ManagerResult`, `ManagerMusicSelect`, `ManagerPlay`, `RG_PS_Judgement`, `TrackData`, `UserAccountModule`,
  `FSForSteam`, `LyrebirdServer`)과 대조. 결과는 위 "알려진 문제" #13~#21, 사소한 것, 수정 방향 제안.
- **원본과 대조해서 안전하다고 확인한 것**
  - `ResultSaveBlockHook`의 대상 두 메서드(당시 `PostRequestPlayResult`, `ComparePlayResultHighScore`. 10-04부터 말단 메서드로 바뀜)는 `void`라
    Prefix로 건너뛰어도 반환값 문제가 없음.
  - 노트는 `NoteGenerator.Generate`가 `Object.Instantiate`로 만들고 풀링하지 않으므로 `GetInstanceID()` 캐시
    (NoteSway/NoteSpeedChaos)가 안전함.
  - 저장된 `lastSelectedSongIndex`가 `CUSTOM_…`여도, 곡 목록에 없는 ID면 `ManagerMusicSelect`가 0번 곡으로 폴백함
    (모드를 빼거나 폴더 이름을 바꿔도 곡 선택 화면이 깨지지 않음).
  - `Util.GetTrackDataListByVisibility()`는 호출마다 새 리스트를 만들므로 `InjectCustomTracks`의 "이미 주입됨"
    검사는 사실상 항상 통과하고, 곡 선택 화면에 들어갈 때마다 폴더를 다시 스캔한다는 문서 설명이 맞음.
  - `ManagerPlay.Update`는 `isGameStarted` 이후에만 `CheckGameFinished`를 호출함(#19 분석의 근거).
- **보존 관점 메모**: 이 저장소 모드에는 Steam/DLC 검증 우회 코드가 없음(과거 `SteamManifestLock`은 제거됨).
  원본 게임은 `FSForSteam.InitializeSteamWorks`에서 `SteamClient.Init(1802720)`이 실패하면 `Util.RequireAppQuit()`로
  종료하고, 모든 저장이 `SteamRemoteStorage`를 거치며, 점수/플레이 횟수는 `lyrebirdferdinant.com:3939`로 보냄.
  모드에는 게임 버전 가드가 없어서 업데이트되면 조용히 깨질 수 있음(→ 시작 시 게임 버전/`Assembly-CSharp` 해시를
  로그로 남기고 테스트한 버전과 다르면 경고하는 것을 권장). `BlockSave`를 "커스텀/치트일 때만 차단"으로 바꾸면
  서버가 닫힌 뒤에도 원본 곡 결과 화면이 서버를 호출하게 되므로, 그 전에 `PostToServer`의 실패 처리(`IncreaseTrackPlayCount`는
  콜백에 `null`을 넘김)를 확인해야 함.
- **문서 수정**: 위 항목을 `INSTALL_AND_LAYOUT`, `TROUBLESHOOTING`, `HOOK_SYSTEM`, `GAME_LOGIC`, `SCORE_SYSTEM`,
  `ARCHITECTURE`, `CODE_STRUCTURE`에 경고로 반영. `DEBUGGING_GUIDE.md`의 닫히지 않은 코드 펜스 수정,
  `90-archive/` 깨진 링크 18개 수정, `문서_구조_개선안.md`에 보관 배너 추가.
- **아직 안 한 문서 정리**: `DEBUGGING_GUIDE.md` 115행 이후의 범용 예시 코드(약 250줄) 정리, 알려진 문제가 여러 문서에
  중복된 것을 한 곳으로 모으기, `CURRENT_STATUS.md`를 변경 이력/알려진 문제/계획으로 나누기, 문서 폴더 이름의
  공백/한글(링크가 `%20`으로 깨지기 쉬움) 정리.

## 2026-09-28 문서 정리

- 2026-07 리팩터링(87개 → 13개 파일, 선언형 Harmony, `FetchBMSToModules` 단일 주입 경로)이 반영되지 않았던 문서를
  현재 코드 기준으로 다시 씀: `00-overview/*`, `CODE_STRUCTURE`, `HOOK_SYSTEM`, `BMS_SELECTION`, `MEDIA_SYSTEM`,
  `BMS_FORMAT`, `BMS_PARSING`, `GAME_LOGIC`, `TROUBLESHOOTING`, `INSTALL_AND_LAYOUT`, `DEBUGGING_GUIDE`.
- 부분 수정: `NOTE_SYSTEM`(주입/스킨 절), `SCORE_SYSTEM`(코드 경로, `Prepare`), `PLAY_OVERLAY`.
- 없어진 코드만 설명하던 `LANE_EXTRACTION`, `TYPE_SYSTEM`, `IMPLEMENTATION`, `PARSING_ALGORITHM`(→ `BMS_PARSING`에 통합),
  `AUDIO_SYSTEM`, `VIDEO_SYSTEM`(→ `MEDIA_SYSTEM`에 통합)은 `90-archive/`로 이동.
- 위 "알려진 문제"를 관련 문서마다 경고로 표시.

## 2026-08-09 v1.1.0: 플레이 씬 진입 때마다 config.txt 다시 읽기

- **추가한 것**: `Main`이 `activeSceneChanged`에서 플레이 씬으로 판정되면 `SaveCustomKeyConfig.Reload()` 호출.
  게임을 재시작하지 않아도 `config.txt` 수정이 다음 플레이부터 반영되고, 한 판 도중에는 값이 절대 바뀌지 않음.
  리트라이는 같은 이름의 `Play` 씬을 다시 로드하므로, 이전 상태와 비교하지 않고 전환마다 무조건 다시 읽음.
- `ResetToDefaults()`로 기본값을 한곳에 모으고 재로드 전에 적용 → 줄을 지우거나 주석 처리하면 기본값으로 돌아감.
- 파일을 먼저 읽은 뒤에 리셋 → 편집기가 파일을 잠근 순간이면 기존 값을 유지(설정이 날아가지 않음).
- `JudgeScoreMaxHook.Prepare()`가 항상 패치하도록 변경(기본값일 때 패치를 건너뛰면 나중에 바꾼 `MaxScore`가 반영될 수 없었음).
- 재로드 로그는 실제로 바뀐 항목만(`이전 → 이후`) 남기고, 변경 없음은 상세 레벨로.
- **검증**: 실게임에서 `MaxScore` 기본값 상태에서도 두 메서드가 패치되는 것, 세션 도중 바꾼 값(1000000 → 2000000)이
  재시작 없이 다음 플레이에 반영되는 것 확인.

## 2026-08-05 추가: 켜기/끄기 값 확장

- `ParseFlexibleBool`이 `트루`/`참`/`켜기`, `폴스`/`거짓`/`끄기`도 인식하도록 확장(기존: `1`/`0`, `true`/`false`, `t`/`f`,
  `on`/`off`, `yes`/`no`, `y`/`n`, `enable(d)`/`disable(d)`, `켜짐`/`꺼짐`, `사용`/`미사용`, `활성화`/`비활성화`).
  `sxtg2.LogicTests`에 테스트 추가(총 7개).

## 2026-08-03 추가: MusicSelect 확인창 Enter 흐름 및 시이(Shii) 캐릭터 조사

- **조사한 것**: 뮤직 셀렉트 화면에서 Enter(`SixtarInput.A`, 키보드 `KeyCode.Return`)를 눌렀을 때 실제로
  호출되는 메서드 체인을, 진단용 Harmony 훅으로 실게임에서 추적함.
- **확인된 호출 체인**:
  ```text
  InputListenerForPC.Update()
   -> ManagerMusicSelect.<GetKeyMap>b__77_6() (키맵 델리게이트)
   -> ManagerMusicSelect.OpenConfirmWindow(bool willFetchKey = true)
   -> ConfirmWindow.ActiveCharacterLayer(willFetchKey)
   -> characterLayer.SetActive(willFetchKey)
  ```
  `OpenConfirmWindow`의 `willFetchKey` 파라미터 하나가 `FetchKeyMap()` 호출 여부, `Startable`,
  `ActiveCharacterLayer` 세 곳에 동시에 영향을 준다(튜토리얼 흐름에서 `false`로 호출되면 시이가 꺼진 채
  확인창만 뜸 — `ManagerMusicSelect.cs:209`).
- **시이(Shii) 캐릭터 확인**: `confirmWindow.characterLayer`("Operator Layer" 오브젝트) 하위에
  `SHII_MODEL_211103`이라는 Live2D Cubism 모델이 항상 자식으로 존재함(에디터에서 미리 배치된 오브젝트이지,
  런타임에 `Instantiate`되는 게 아님). `ActiveCharacterLayer`는 이 오브젝트를 `SetActive`로 껐다 켤
  뿐이고, 켜진 뒤의 눈 깜빡임/물리 흔들림 등은 Live2D SDK의 `CubismUpdateController`가 `LateUpdate()`에서
  `ICubismUpdatable` 컴포넌트(`CubismEyeBlinkController`, `CubismPhysicsController`,
  `CubismExpressionController` 등)를 모아 자체적으로 구동함 — 게임 로직과는 무관.
- **에셋 실제 위치**: AssetBundle이 아니라 Unity `Resources` 시스템에 있음.
  `Assets/Resources/L2DCharacter/SHII_MODEL_211103/` 폴더에 `.moc3`/`.model3n`/텍스처와
  `IDLE`/`CLEAR_1`/`CLEAR_1_JP_0~2`/`CLEAR_2_FC`/`CLEAR_2_PB`/`CLEAR_IDLE`/`FAILED`/`FAILED_IDLE`
  모션 파일이 들어있음. 빌드 파일 기준으로 `resources.assets`에 컴파일되어 있고, `level8`(MusicSelect
  씬)과 `level12`(Result 씬) 양쪽에 인스턴스가 있음 — 확인창뿐 아니라 결과 화면에서도 같은 모델로
  클리어/실패 리액션을 보여주는 구조로 보임.
  (참고: `ManagerMusicSelect.instantiateOperatorCharacter(opCharID, opCharLayer)`는
  `Resources.Load<GameObject>("Rhythm Game Part/Operators/" + opCharID)` 경로를 쓰는데, 이는 시이의
  실제 경로(`L2DCharacter/...`)와 다른 별개 로더로 보이며, 이번 조사에서 호출 로그가 한 번도 안 찍힘 —
  시이는 이 경로를 타지 않음.)
- **진단용으로 추가한 것(남아있음, 추후 정리 대상)**: `Features/MusicSelectFeature.cs`의
  `ManagerMusicSelectHook.OpenConfirmWindowPostfix`(+`LogCharacterLayer`/`LogHierarchy`),
  `InstantiateOperatorCharacterPostfix`, `OperatorCharacterHook`(`SetUp`/`ShowDialogue`)는 전부 이번
  조사용으로 추가한 로깅 훅. 실제 게임 동작은 바꾸지 않지만, 기존 관례(`최근 정리 내역` 참고)대로
  다음에 이 영역을 건드릴 때 제거 대상.
- **검증**: `dotnet build` 성공(경고 0개), 게임 `Mods/`에 배포 완료, 실게임 로그로 위 내용 전부 확인.

## 계획 중: 커스텀 시이(Shii) Live2D 모델 교체

- 확인창/결과 화면에 나오는 시이(`SHII_MODEL_211103`, `Assets/Resources/L2DCharacter/SHII_MODEL_211103/`)를
  다른 Live2D 모델로 교체하는 걸 계획 중.
- 이 모델은 코드에서 `Resources.Load`로 동적 로드되는 게 아니라 `ConfirmWindow.characterLayer`
  필드에 씬 단계에서 미리 배치되어 있으므로, 교체하려면 (a) 같은 폴더 구조/파일명으로 리소스 파일
  자체를 치환하거나 (b) `ConfirmWindow.characterLayer`가 가리키는 오브젝트를 후킹으로 다른 프리팹으로
  바꿔치기하는 방식 중 하나가 필요함. `.moc3`/텍스처/모션 파일 세트를 그대로 유지한 채 내용만 바꾸는
  (a) 쪽이 코드 수정 없이 되는 가장 간단한 경로로 보임.
- 아직 실제 착수 전 — 조사만 완료된 상태.

## 2026-08-03 추가: 판정바 삼각(Triangle) 모양 옵션 추가 및 등급별 범위 박스 커스텀 확장

- **추가한 것**:
  - `JudgmentBarShape` (0 = 사각 바, 1 = 알약 캡슐, 2 = 삼각/다이아몬드 바, 기본값 0) — 기존 `JudgmentBarCapsule=1`과 100% 하위 호환 유지.
  - 삼각(Triangle) 모양은 중앙(0ms 기준선)에서 너비가 가장 넓고 양끝(±MaxMs) 오차 한계선으로 갈수록 뾰족해지는 다이아몬드/이등변삼각형 마스크 텍스처(`GetTriangleTexture`)를 생성하여 픽셀 안티에일리어싱 렌더링.
  - `JudgmentBarRangeShape` (-1 = 배경 트랙 모양 추종, 0 = 사각, 1 = 알약, 2 = 삼각, 기본값 -1) — 배경 트랙뿐만 아니라 내부 판정 범위 박스(BLUESTAR/WHITESTAR/YELLOWSTAR)에도 지정한 모양이 적용되도록 통일/분리 커스텀 구현.
  - `JudgmentBarSide` (`Left`/`Right`/`Center`, 기본값 `Center`) — 판정바를 화면 왼쪽/오른쪽 가장자리(여백 60px)에 붙이거나, 기존 기본 위치(세로=왼쪽 고정, 가로=정중앙)를 그대로 씀.
  - **키뷰어 색상 커스터마이징 (`KeyViewerPressedColor`, `KeyViewerNormalColor`, `KeyViewerGatePressedColor`)**: 키뷰어 입력 배경/미입력 배경/중앙 GATE 키 전용 눌림 색상을 설정 가능하게 추가. `#RRGGBB`, `#RRGGBBAA`, `R,G,B,A` 수치뿐 아니라 **한글 색상명**(`시안`, `마젠타`, `노랑`, `빨강`, `파랑`, `초록`, `흰색`, `검정`, `주황`, `보라`, `분홍`, `하늘색`, `민트` 등) 파싱을 완벽 지원(`ParseColorSetting`).
  - **결과 씬 오퍼레이터 레이어 로깅 (`ManagerResultHook`)**: 결과 화면(`RhythmGame.Result.ManagerResult`) 진입 시 오퍼레이터 관련 필드, 씬 내 `Operator Layer` / `characterLayer`, Live2D 모델(`SHII_MODEL_211103`) 및 `OperatorCharacter` 계층 구조(`LogHierarchy`)를 상세 진단 로깅.
- **검증**: `dotnet build` 성공 (경고 0개), `sxtg2.LogicTests` 6개 통과, `Mods/sxtg2.dll` 배포 완료.

## 2026-08-03 추가: 판정바 모양(캡슐/사각) + 좌우 위치 설정, config.txt 마이그레이션 누락 수정

> 2026-09-28 복구: 이 절의 제목과 앞부분이 바로 위 v1.0.1 절을 추가할 때 잘려 나가 있던 것을 커밋 `fc0616f`의
> 원문으로 되살렸습니다. 아래 "안쪽 범위 박스는 항상 사각형"은 이후 v1.0.1의 `JudgmentBarRangeShape`로 바뀌었습니다.

- **추가한 것**:
  - `JudgmentBarCapsule` (1 = 알약 캡슐, 0 = 사각 바, 기본값 0) — 가장 바깥쪽 배경 트랙의
    모양만 바꿈. 안쪽 등급 범위 박스(BLUESTAR/WHITESTAR/YELLOWSTAR)는 항상 사각형으로 유지
    (처음엔 안쪽 박스에도 캡슐을 적용했다가, 사용자 피드백으로 바깥쪽 트랙에만 적용하도록 수정함).
  - `JudgmentBarSide` (`Left`/`Right`/`Center`, 기본값 `Center`) — 판정바를 화면 왼쪽/오른쪽
    가장자리(여백 60px)에 붙이거나, 기존 기본 위치(세로=왼쪽 고정, 가로=정중앙)를 그대로 씀.
    둘 다 `JudgmentBarVertical`(세로/가로)과는 독립적인 설정.
  - 세로 바를 `Right`로 두면 히트 오차 텍스트 라벨이 자동으로 바 왼쪽으로 옮겨 붙어서
    화면 밖으로 잘리지 않음(`labelOnLeftOfBar` 분기).
- 캡슐 모양은 `JudgmentBarFeature.cs`의 `GetCapsuleTexture(w, h)`가 크기별 알파 마스크
  텍스처를 생성해 캐시하는 방식으로 구현(스타디움 형태, 반지름 = `min(가로,세로)/2`,
  픽셀 중심점과 선분 사이 거리로 1px 안티에일리어싱). 중앙선과 히트 틱은 항상 얇은 직선 그대로 둠.
- **버그 수정**: 기존 설치본의 `config.txt`에는 `JudgmentBarCapsule`/`JudgmentBarSide` 키가
  없는데도 모드가 파일에 자동으로 추가해주지 않는 문제가 있었음(`AppendSectionsMissingFrom`에
  이 두 키가 등록돼 있지 않았음 — `MaxScore`/`NoteSway`/`NoteSpeedChaos`만 자동 추가 대상이었음).
  두 키를 `AppendSectionsMissingFrom` 목록에 추가해서, 다음 게임 실행부터는 기존 `config.txt`
  파일 끝에 두 항목이 자동으로 덧붙여지도록 고침.
- **검증**: `dotnet build` 성공(경고 0개), `sxtg2.LogicTests` 6개 통과, `Mods/sxtg2.dll`에
  배포 완료. 실게임 확인 **미완료** — 캡슐 모양/좌우 배치가 의도한 대로 보이는지, 기존
  `config.txt`에 두 항목이 실제로 자동 추가되는지 플레이해보고 확인 필요.
- 자세한 내용: `02-systems/PLAY_OVERLAY.md`, `01-user-guide/INSTALL_AND_LAYOUT.md`

## 계획 중: 등급별 누적 판정 카운터 (BLUESTAR/WHITESTAR/YELLOWSTAR/REDSTAR)

- 판정바(`JudgmentBar`)처럼 플레이 중 실시간으로 4개 등급 누적 개수를 보여주는 위젯을 추가할 예정.
- 데이터는 원본 게임의 `RG_PS_Judgement.JudgeCount`(`JudgeCounter`, `int[4]`)에 이미 다 쌓이고
  있고, 원본에는 REDSTAR(미스)만 실시간으로 보여주는 `RedStarCounter` 위젯이 있음 — 이걸
  4개 등급 전부로 확장하는 개념.
- 자세한 내용(코드 위치, 구현 방향): `02-systems/PLAY_OVERLAY.md`의 "향후 계획: 등급별 누적
  판정 카운터" 절 참고.

## 2026-07-27 추가: 노트 속도 카오스 (NoteSpeedChaos)

- **추가한 것**: 노트마다 낙하 속도 배율을 다르게 주는 챌린지 기능(`NoteSpeedChaosHook`).
  `NoteSpeedChaosPerLane`으로 "노트마다"(완전 카오스)와 "레인마다"(같은 레인 안에서는 순서 유지)를
  고를 수 있다. 기본은 꺼짐.
- 노트끼리 서로 추월하는 것은 **의도된 동작**이다. 정상적인 SV(누적 거리 함수 기반)와는 다르며,
  커스텀 곡에서 BPM 변화를 쓰지 않으므로 SV는 구현하지 않았다.
- 홀드는 헤드와 길이에 같은 배율을 걸고 꼬리를 `헤드 + 길이`로 다시 잡아 몸통이 배율만큼
  늘어난다. 마스크를 옮겼으므로 `holdTexture` 상쇄도 다시 건다.
- `NoteGenerator`가 속도와 무관하게 고정 3초 전에 노트를 만들기 때문에, 배율이 1보다 작은 노트는
  그대로 두면 화면 안쪽에서 튀어나온다. `NoteGenerator.Start` Postfix에서 `notePreGenerateTime`을
  `3 / 최저배율`로 늘려 해결했다(`private readonly`지만 인스턴스 필드라 리플렉션으로 써진다).
- **검증**: `dotnet build` 성공(경고 0개), 로직 테스트 6개 통과, 게임 `Mods/`에 배포 완료.
  실게임 확인 **미완료** — 기본 배율 범위(0.6~1.8)가 적절한지 플레이해보고 조정 필요.
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`("노트 속도 카오스" 절),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(6절)

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
  패치를 아예 붙이지 않음. (→ 2026-08-09 v1.1.0부터는 항상 패치함. 위 v1.1.0 항목 참고)
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
- **이후 변화**: 2026-07-21~26 파일 통합으로 위 세 파일은 `Loaders/CustomNoteLoaders.cs`와 `Hooks/GameplayHooks.cs`로
  합쳐졌고, `12348f5`에서 로더가 단순화되면서 `Blue.png` 방식 파일명 규칙이 사라짐(알려진 문제 #2).
- 자세한 내용: `02-systems/NOTE_SYSTEM.md`("노트 스킨" 절), `02-systems/HOOK_SYSTEM.md`(NoteSpriteHook),
  `01-user-guide/INSTALL_AND_LAYOUT.md`(5절, CustomNotes 폴더 규칙)

## 2026-07-18 추가: SaveCustomKey 폴더 생성 로직

- `{게임 설치 폴더}\SaveCustomKey\` 폴더를 모드 초기화 시 자동 생성하는 로직만 추가함
  (`Helpers/SaveCustomKeyFolderHelper.cs`). 아직 이 폴더를 실제로 읽고 쓰는 기능은 없음 — 향후
  커스텀 키 프리셋 파일 저장/불러오기 기능을 위한 준비 단계.
- **이후 변화**: 이 폴더는 키 프리셋이 아니라 모드 설정 파일 `config.txt`의 위치가 됨
  (`Helpers/ModHelpers.cs`의 `SaveCustomKeyConfig`, 2026-07-26 `869f25a`부터).
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
  (현재 `Main.OnApplicationQuit()`은 종료 로그 한 줄만 남기고 이벤트 구독 해제는 하지 않음. `SceneDetector`는 이후 삭제됨)

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

- 2026-07-21 ~ 07-26 구조 정리
  - partial 클래스 병합과 죽은/진단 코드 정리(`95674e8`)
  - 수동 Harmony 패치를 `[HarmonyPatch]` 선언형으로 전환(`53f4174`)
  - 커스텀 차트 파이프라인 단순화(`ba60f89`): `TextHook`, `SXGTDataHook`, `CustomPlayStartupFlow`,
    `BmsFileResolver`, `SceneDetector` 등 제거, `ManagerPlay.FetchBMSToModules` Prefix 하나로 통합
  - 파일 통합(`12348f5`): `Hooks/*` 하위 폴더 파일들을 `GameplayHooks.cs`/`AudioHooks.cs` 등으로 합침
    (2026-05-15 기준 87개였던 C# 파일이 이후 기능 추가를 포함해 현재 13개)
- (이하 2026-05-15 ~ 07-18 정리. 여기 나오는 `BmsFileResolver`/`SXGTDataHook`도 위 정리 때 다시 없어짐)
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
  -> ManagerMusicSelectHook.AwakePostfix -> TrackDataAnalyzer가 hwa 폴더마다 CustomTrackData 주입
  -> PlayPreviewPrefix가 커스텀 미리듣기, TrackDataMediaHook이 커스텀 자켓

PlayLoading / Play (씬 진입마다 config.txt 재로드)
  -> ManagerPlay.Set: 도너 트랙의 패턴/오디오 로드 (TrackDataMediaHook)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
       -> BmsParser가 CustomTrackData.BmsPath 파싱
       -> CustomChartInjector가 같은 SXGTData의 레인을 비우고 커스텀 노트 주입, 노트 수 갱신
       -> BGMPlayerHook/BGAPlayerHook이 미디어 교체
  -> 매 프레임: BGABGMSyncHook, 판정바/키뷰어, 노트 연출 훅, 오토플레이

Result
  -> ResultSaveBlockHook이 조건부로 기록 저장/전송 차단
```

자세한 흐름은 `00-overview/DOCUMENTATION.md`.

## 남은 주의점

- 위 "알려진 문제" 표의 항목들(특히 #1 홀드 끝 누락, #2 노트 스킨, #3/#13 BlockSave의 차단 범위, #14 서버로 나가는 커스텀 ID).
- `BlockSave`는 이름과 달리 업적, 플레이 횟수, 서버 플레이 카운트는 막지 않습니다(#13, #14). 오토플레이/올퍼펙트/
  `MaxScore` 변경/커스텀 차트를 쓰는 동안은 Steam 업적이 열릴 수 있다는 점을 알고 사용하세요.
- 진단용 로깅 훅(`OpenConfirmWindowPostfix`, `InstantiateOperatorCharacterPostfix`, `OperatorCharacterHook`,
  `ManagerResultHook`)은 조사가 끝나면 제거하거나 `LogLevel=2`에서만 동작하게 바꿀 대상입니다.
- 게임 업데이트로 private 필드/메서드 이름이나 판정 메서드의 `1000000f` 리터럴이 바뀌면 해당 기능이 조용히
  꺼질 수 있습니다. 업데이트 후에는 `[JudgeScoreMax] ... 교체 완료` 로그와 커스텀 차트 주입 로그를 먼저 확인하세요.
