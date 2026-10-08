# 현재 상태

기준일: 2026-10-05 (코드 v1.1.0, 알려진 문제 일괄 수정 반영)

## 현재 결론

커스텀 차트 흐름, 판정바/키뷰어, 설정 파일(플레이마다 재로드)이 동작합니다. 2026-09-28 ~ 10-05 점검에서 찾은 문제 33개와
추가로 찾은 1개 중, 코드로 고칠 수 있는 것은 2026-10-05에 한꺼번에 고쳤습니다(아래 "일괄 수정 내역").
남은 것은 새 기능이 필요한 것(마디 길이/BPM 변화)과 효과가 불확실해 보류한 것(자켓 텍스처 메모리)뿐입니다.

**일괄 수정은 컴파일, 단위 테스트, 게임 어셈블리 대조까지 확인했지만 실게임에서는 아직 확인하지 않았습니다**(아래 "실게임에서 확인할 것").

## 검증된 상태

- `sxtg2.LogicTests` 13개 통과 (2026-10-05). 설정 값 파서는 이제 복사본이 아니라 `ConfigParsing.cs`를 직접 링크해 검증합니다.
- `dotnet build`(Debug) 경고 0개, `build.bat`/`build-release.bat`을 임시 게임 폴더로 실행해 Debug/Release 모두 성공
  (Release DLL이 Debug보다 작음 → `x64` 구성의 최적화 적용 확인). 게임 폴더에는 배포하지 않았습니다.
- Harmony 패치 메서드 29개의 대상 메서드와 주입 매개변수(`___필드`, `__N`, 매개변수 이름)를 실제 게임 `Assembly-CSharp.dll`에
  대해 검사하는 임시 하네스로 확인 — 전부 해석됨. 일부 패치 클래스는 Unity 내부 호출(ECall) 때문에 게임 밖에서는 실제 적용까지는
  못 해 봤습니다.
- 새 주입기(`CustomChartInjector`)를 실제 게임 클래스(`SXGTData`/`ShortNote`/`HoldNote`)로 실행해 확인: 끝 없는 홀드는 일반 노트로,
  끝 없는 오픈 노트는 기본 길이(1초)로 바뀌고 `tickTime`이 채워지며, `trackStartTiming`이 0이 되고, 중간에 예외가 나도 도너 데이터가
  그대로 남습니다.
- 2026-10-05 플레이 세션 로그(10-04 수정까지 포함된 빌드): 오류/경고 0건, `[차단] … UserAccountModule.SavePlayData`/
  `LyrebirdServer.PostUserScore` 정상, 주입 로그의 `trackStartTiming=0`(도너 값이 0이라 싱크 정상).

## 일괄 수정 내역 (2026-10-05)

번호는 이전 "알려진 문제" 표의 번호입니다(설명은 git 이력과 아래 날짜별 절에 있습니다). 아래 날짜별 절과 다른 문서에서 "알려진 문제 #N"이라고 쓴 곳도 이 번호이며, 위 표에서 수정된 항목입니다.

| # | 한 일 |
| --- | --- |
| 1, 26 | 끝(`03`) 없는 홀드는 일반 노트로, 끝(`05`) 없는 오픈 노트는 기본 길이 1초의 `HoldNote`로 바꾸고 경고 로그(레인/시각 예시)를 남김. `HoldNote`로 넣으면 `tickTime`이 null이라 게임 판정 루프가 멈추고, 오픈 노트 길이 0은 게이트 애니메이션 속도를 `1f / 0f`로 만들던 문제 |
| 2 | 노트 스킨: 노트 이름의 `(Clone)` 접미사를 떼서 `Blue.png`/`Blue_tail.png`/`Blue_hold.png`가 매칭됨. 예전 안내대로 만든 `Blue(Clone).png`도 계속 동작 |
| 3, 13 | 저장 차단 정책: 오토플레이/올퍼펙트/`MaxScore` 변경/**커스텀 곡**은 항상 차단, 원본 곡은 `config.txt`의 `BlockSave`를 따름(MelonPreferences와의 OR 제거 → `BlockSave=0`이 실제로 동작). 같은 조건에서 Steam 업적과 플레이/실패 횟수, 마지막 플레이 곡도 막음(`ResultTaintHook`) |
| 14, 34 | 커스텀 곡 ID를 공식 서버로 보내지 않음(`IncreaseTrackPlayCount` 차단, 랭킹 조회는 빈 목록으로 응답). 랭킹 창이 커스텀 곡에서 `NullReferenceException`을 내던 문제(`Util.FindTrackByID`가 못 찾음)도 수정 |
| 4 | BMS 데이터 줄의 채널부(`#`~`:`)가 `숫자 + 영숫자 두 글자` 형식인지 검사해 헤더 줄(`#TITLE Remix 2011:0101`)을 제외 |
| 27 | `#WAVCMD` 같은 6글자 `#WAV…` 명령 줄이 3글자 모드를 켜지 않음 |
| 28 | `#BPM`이 무한대/10만 초과면 무시하고 다음 유효한 값(없으면 150) 사용 |
| 5 | 판정바/키뷰어는 실제 플레이 씬(`Play`)에서만 그림. 설정 재로드는 `PlayLoading` 포함으로 그대로(Play 씬의 Awake/Start 전에 최신 값이 반영되게) |
| 6, 31 | 빌드 스크립트가 `x64`로 빌드(csproj의 `DEBUG` 상수, Release 최적화 적용). `build-release.bat`은 `build.bat Release` 호출로 통합, 저장소 위치는 스크립트 위치에서 구함, `GAME_PATH`/`NO_PAUSE` 환경 변수 지원. `tools/method_length_scan.py`가 모드 폴더를 스캔하도록 수정, 죽은 `tools/merge_partial_classes.py`와 빈 `clean.bat` 삭제, `.gitattributes`로 배치 파일 CRLF 고정 |
| 7, 16 | 진단 훅(확인창 계층, 오퍼레이터, 결과 화면 전체 스캔)을 `Features/DiagnosticHooks.cs`로 분리하고 `LogLevel=2`에서만 동작. 핵심 훅과 같은 클래스에 섞여 있던 것을 분리 |
| 8 | 자켓은 곡마다 한 번만 찾고 결과를 기억(없을 때 요청마다 파일 9개를 다시 확인하고 경고를 반복하던 문제) |
| 9 | `music.*`가 없으면 폴더의 첫 파일이 아니라 **가장 큰** 오디오 파일을 BGM으로 쓰고 경고. 새 미리듣기 클립이 만들어지면 이전 클립을 해제 |
| 10 | 커스텀 스프라이트를 쓰는 이미지에만 `SetNativeSize()`를 적용하고, 게임의 노트 크기 옵션(`noteSize/100`)을 다시 곱함. 스킨이 없는 노트는 건드리지 않음 |
| 11 | 같은 프레임에 같은 (판정, 오차)가 위젯 수만큼 중복 등록되던 것을 한 번만 받음. 위젯을 하나도 장착하지 않으면 틱이 안 나오는 것은 그대로(아래 남은 문제) |
| 15 | 씬 이벤트 구독을 노트 스킨 초기화보다 먼저 하고 스킨 초기화는 별도 `try`. 게임의 private 필드 접근은 `SafeAccess`로 감싸 필드가 없으면 경고 후 그 기능만 건너뜀(`TypeInitializationException` 방지) |
| 17 | 주입은 임시 목록에 전부 만든 뒤 성공했을 때만 게임 데이터를 교체(`InjectBmsNotesToLaneData`가 성공 여부를 반환). 실패하면 도너 패턴이 그대로 남음 |
| 19 | 오토플레이를 게임의 `ManagerPlay.autoPlay` 플래그(플레이 시작 때 `InitializePlayScene` Postfix에서 켬)로 바꿈. 홀드가 끝날 때 `OnLaneKeyUp`이 호출되지 않아 레인이 눌린 채 남던 것과 리트라이 때 시간 캐시가 남던 것이 함께 해결 |
| 20 | 설정: 값 뒤 줄 끝 주석(`# 메모`, `// 메모`) 제거, 알 수 없는 키/켜기·끄기 값/형식이 틀린 줄에 경고, 키→처리기 표로 파싱(옵션 추가가 한 줄), 색상 `핑크` 인식 |
| 21, 29 | 곡 정보: UTF-8이 아니면 경고, 필드를 하나도 못 읽으면 경고, 부제(`subtitle`)가 제목을 덮어쓰지 않음, 제목이 없으면 앨범 폴더 이름 사용. 앨범마다 찍히던 로그는 `LogLevel=2`로 |
| 22, 24, 23 | (10-04) 저장 차단을 말단 메서드로 이동, 누락 설정 항목 추가는 게임 시작 때만, `trackStartTiming`을 0으로 고정 |
| 25 | `.wav`도 5MB 이하면 스트리밍하지 않고 통째로 로드(스트리밍 클립의 `length` 불확실성 제거) |
| 32 | 긴 메서드를 분리(`DrawJudgmentBar`, `KeyViewer.Draw`, `LoadConfigFile`, `CheckAndSync`, `ParseColorSetting`, 주입기). `method_length_scan` 기준 60줄 이상 메서드 없음 |
| 33 | BGA 영상이 곡보다 짧으면 영상이 끝난 뒤에는 동기화 훅이 건드리지 않음 |
| (기타) | `MediaUrl.FromPath`로 파일 경로의 `%`, `#`, `?`를 이스케이프(폴더 이름에 있으면 BGM/BGA/미리듣기가 안 읽히던 문제). BGA 파일을 이름순으로 선택. `AllPerfectJudgeHook`/판정바 훅이 대상 0개면 경고(중복 대상은 한 번만 패치). NoteSway 진폭이 0이 되면 x를 기준 위치로 복원. `JudgeScoreMaxHook` 로그 중복 제거. `ResourceDonor` null 방어. `sxtg2.sln`의 프로젝트 GUID를 csproj와 맞춤, `AssemblyInfo`의 옛 이름 `sixgtar3` 정리, 버전은 `ModInfo.Version` 한 곳(`MelonInfo`/`AssemblyVersion`이 공유), csproj의 안 쓰는 참조 정리 |

## 알아 둘 동작 변화

- **`CustomNotes`의 `blue.png`/`red.png`가 이제 실제로 적용됩니다.** 예전에는 이름 매칭 문제로 로드만 되고 한 번도 적용되지 않았습니다.
  현재 폴더의 PNG는 `blue.png`(109×44), `red.png`(251×50)입니다. 크기나 모양이 마음에 들지 않으면 파일을 지우거나 이름을 바꾸세요.
- **커스텀 곡은 항상 기록/업적/플레이 횟수에서 제외됩니다**(`BlockSave` 설정과 무관). 원본 곡은 `BlockSave`를 따릅니다.
- **오토플레이는 게임 자체 오토플레이 경로로 동작합니다.** 키 입력은 무시되고 홀드도 정상적으로 끝납니다.
- **`LogLevel=1`(기본)에서는 확인창/결과 화면 진단 로그와 앨범별 곡 정보 로그가 사라집니다.** 조사하려면 `LogLevel=2`.
- `config.txt`에 모르는 키나 값을 쓰면 경고가 납니다(이전에는 조용히 무시).

## 실게임에서 확인할 것 (배포 후)

배포는 `build.bat`(게임 `Mods`로 복사)으로 합니다. 확인 항목:

1. 곡 선택, 플레이, 결과 화면이 이전처럼 동작하는지(오류/경고 로그가 없는지). 결과 화면에서 베스트 점수가 표시되는지.
2. 커스텀 노트 스킨(`blue.png`/`red.png`)의 크기와 모양, 노트 크기 옵션을 바꿨을 때 스킨 노트도 같이 바뀌는지.
3. 판정바와 키뷰어가 예전처럼 그려지는지(`PlayLoading` 로딩 화면에는 안 나와야 함), 틱이 겹쳐 진하게 보이지 않는지.
4. `AutoPlay=1`로 오토플레이: 홀드가 끝난 뒤 레인이 눌린 채 남지 않는지, 점수/결과 화면이 정상인지.
5. 커스텀 곡을 끝까지 플레이한 뒤 Steam 업적이 열리지 않는지(`[ResultTaint] … 반영하지 않습니다` 로그), 플레이 횟수가 늘지 않는지.
6. 곡 선택에서 커스텀 곡으로 랭킹 창을 열었을 때 오류 없이 빈 목록으로 열리는지.
7. 끝 없는 홀드/오픈 노트가 있는 차트(경고 로그가 나오는지), BGA가 곡보다 짧은 곡, `#`/`%`가 들어간 폴더 이름.
8. `config.txt`에서 `AutoPlay=1 # 메모`처럼 주석을 붙이거나 키를 오타 내면 경고가 나는지, 섹션을 지워도 파일이 다시 쓰이지 않는지.

## 남은 문제

| # | 문제 | 비고 |
| --- | --- | --- |
| 11 (일부) | 게임 설정에서 플레이 위젯을 하나도 장착하지 않으면 `OnGetJudge`가 호출되지 않아 판정바에 틱이 나오지 않음 | 정확히 노트당 1회가 필요하면 `RG_PS_Judgement.TryJudgeShortNote` 쪽으로 옮겨야 함. 위젯 1개만 장착하면 해결 |
| 12 | BMS 채널 `02`(마디 길이), BPM 변화(`03`/`08`, `#BPMxx`), STOP 미지원 | 새 기능이 필요함. 게임 자체 차트 형식(`SXGTReader`)과 모드 BMS 형식이 달라 설계부터 필요 |
| 30 | (보류, 낮음) 자켓을 원본 해상도 RGBA32 + 밉맵 `Texture2D`로 읽음 | 압축은 크기가 4의 배수가 아니면 안 되고(1952×1098 등), 축소는 GPU 읽기가 필요해 효과 대비 위험이 커서 보류 |
| 사소 | NoteSpeedChaos 레인별 모드의 시드가 `InstanceID % 1000`(음수 가능, 실행마다 다름) | 레인 계층 구조를 확인한 뒤 레인 인덱스 기반으로 바꾸는 것이 좋음 |
| 제안 | 게임 버전 가드가 없음 | 시작 시 게임 버전과 `Assembly-CSharp` 해시, 적용된 패치 수 요약을 로그로 남기는 것을 권장 |
| 정리 (해결) | `release/` 바이너리와 `list_managed_games.txt`를 저장소에서 삭제 | 2026-10-08 삭제 |
| 정리 | `sxtg2.LogicTests`가 `sxtg2.sln`에 없음 | `build.bat`이 sln을 빌드하므로, .NET 8 SDK 프로젝트를 넣으면 빌드 환경 의존이 늘어서 일부러 뺌 |
| 문서 | `DEBUGGING_GUIDE.md` 115행 이후의 범용 예시 코드(약 250줄), `CURRENT_STATUS.md`의 이력/문제/계획 혼재 | 구조 정리가 필요 |

## 2026-10-05 재점검 (코드 미수정, 문서만 기록)

2026-10-04 수정(`7a3beec`)을 다시 읽어 검증하고, 아직 안 본 부분을 원본(`sxtg2/`)과 대조했습니다. 결과는 위 "알려진 문제" #26~#30.

- **10-04 수정 검증**: 문제 없음.
  - 저장 차단을 말단 메서드로 옮긴 뒤 래퍼가 끝까지 실행되는데, `PostRequestPlayResult`가 역참조하는 `LyrebirdServer.Instance`는
    곡 선택 화면(`ManagerMusicSelect.cs:544`)도 똑같이 역참조하므로 정상 흐름에서는 항상 존재함. `GetUserName()`은
    `SteamClient.SteamId`라 Steam이 필수인 게임에서는 예외가 없고, `GetPlayData`는 저장소를 읽기만 함.
  - 설정 누락 섹션 추가는 시작 때 `JudgeScoreMaxHook.Prepare`와 `Main.OnInitializeMelon`에서 `Initialize`가 두 번 불려도
    멱등(두 번째는 이미 추가된 키를 봄). 새 모드 버전에서 키가 늘어나면 다음 게임 시작 때 정상 추가됨.
- **실행으로 확인한 것**(`BmsParser.cs`를 링크한 임시 콘솔 프로젝트, 저장소 밖):

  | 입력 | 결과 |
  | --- | --- |
  | `#BPM 150` + `#00111:0100` | 노트 1개, t=1.6 (정상) |
  | `#BPM Infinity` + `#00111:0100` | `bpm=∞`, t=0 (#28) |
  | `#WAVCMD 01 01 x` 줄 + `#00111:0100` | 노트 0개 (#27) |
  | `#WAV01 a.wav` 줄 + `#00111:0100` | 노트 1개 (2글자 키는 정상) |
  | `#00104:0400` (끝 없는 오픈) | `Open L9 len=0` (#26) |
  | `#00111:0200` (끝 없는 홀드) | `Long L1 len=0` (#1) |
- **원본 대조로 확인한 것**
  - `RG_Gear.IsGateOpened`는 기본 `false`, `ManagerPlay.OnLaneKeyDown/Up`은 게이트가 닫혀 있으면 GATE 입력을 무시함.
    오픈 노트(`nAction == NONE`)는 `CheckOpenState`에서 `SwitchGateOpenState(holdNote.duration)`로 게이트를 토글하고 곧바로
    판정 커서를 넘김(끝 시각은 쓰지 않고 `duration`은 애니메이션 길이). `98` 값 노트(`EnableAutoPlay`)는 게임이 직접
    `ManagerPlay.autoPlay`를 켜고 끔(#19 제안의 근거).
- **문제없음으로 확인한 것**: 레벨 잠금 UI(`RG_MS_UnlockLevel`, `IndicatorSelectableLevels.SetUnlocked`)는 호출하는 곳이 없는 죽은
  코드라 커스텀 곡의 난이도 선택에 영향이 없음. `CheckCruiseMode`는 빈 함수, `SXGTData.totalTicks`는 쓰는 곳이 없음.
- **문서 반영**: `BMS_FORMAT`(게이트/오픈 노트 절, 헤더 표 경고), `BMS_PARSING`, `NOTE_SYSTEM`, `GAME_LOGIC`(오픈 노트와 게이트 절),
  `INSTALL_AND_LAYOUT`, `TROUBLESHOOTING`(15번 신설), `BMS_SELECTION`, `MEDIA_SYSTEM`.
- **같은 날 추가 점검**(#31~#33): `tools/` 스크립트 전체와 모드의 메서드 길이, BGA 동기화 훅을 봤습니다. 도구의 잘못된 스캔 대상은
  실행해서 확인했고(`sxtg2/`에 `Assembly-CSharp.csproj`가 있음), 메서드 길이는 같은 도구로 `sxtg2-mod`를 측정한 값입니다.
  문서에는 `CODE_STRUCTURE`(도구 설명), `MEDIA_SYSTEM`, `TROUBLESHOOTING`(7번)에 반영했습니다.
- **원본 대조의 신뢰도**: 게임 `Assembly-CSharp.dll`(2025-11-15)이 디컴파일(2026-07-18)보다 오래돼서 디컴파일은 현재 게임
  DLL과 같은 버전입니다.
- **배포 상태(10-05 기준)**: 게임 `Mods\sxtg2.dll`은 2026-08-09 빌드(v1.1.0)입니다. 10-03/04에 고친 #18, #22, #24는 커밋되어
  있지만 `build.bat`으로 배포하기 전에는 게임에 반영되지 않습니다(위 "수정" 절의 실게임 확인 항목도 배포 후에 할 수 있음).

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

PlayLoading / Play (씬 진입마다 config.txt 재로드. 오버레이는 Play 씬에서만)
  -> ManagerPlay.Set: 도너 트랙의 패턴/오디오 로드 (TrackDataMediaHook)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
       -> BmsParser가 CustomTrackData.BmsPath 파싱
       -> CustomChartInjector가 임시 목록에 노트를 만들고 성공하면 같은 SXGTData에 교체(노트 수, trackStartTiming=0 갱신)
       -> BGMPlayerHook/BGAPlayerHook이 미디어 교체
  -> 매 프레임: BGABGMSyncHook, 판정바/키뷰어, 노트 연출 훅, 오토플레이

Result
  -> ResultSaveBlockHook이 SavePlayData/PostUserScore를 조건부로 차단, ResultTaintHook이 업적/플레이 횟수 보호
```

자세한 흐름은 `00-overview/DOCUMENTATION.md`.

## 남은 주의점

- 위 "남은 문제" 표(위젯 미장착 시 틱 없음, 마디 길이/BPM 변화 미지원, 자켓 메모리).
- 2026-10-05 일괄 수정은 실게임 확인 전입니다. 위 "실게임에서 확인할 것"을 배포 직후 한 번 훑어 보세요.
- 게임 업데이트로 private 필드/메서드 이름이나 판정 메서드의 `1000000f` 리터럴이 바뀌면 해당 기능이 꺼질 수 있습니다. 필드 접근은
  `SafeAccess`가 경고 로그(`[SafeAccess] … 필드를 찾지 못해 …`)를 남기고 그 기능만 건너뛰며, `[JudgeScoreMax] … 교체 완료`와
  `[AllPerfect] 판정 메서드 N개를 패치합니다`, 주입 로그(`[CustomChartInjector] …`)가 정상인지 업데이트 후에 먼저 확인하세요.
- 진단 로그(확인창 계층, 결과 화면 스캔)는 `LogLevel=2`에서만 나옵니다. 시이(Shii) 조사를 다시 하려면 `UserData\MelonPreferences.cfg`의
  `[sxtg2]`에서 `LogLevel = 2`로 바꾸세요.