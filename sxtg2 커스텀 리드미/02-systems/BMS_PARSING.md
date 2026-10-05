# BMS 파일 파싱 상세

기준일: 2026-09-28 (v1.1.0, `Loaders/BmsParser.cs` 기준)

입력 형식(채널/값 규칙)은 [BMS_FORMAT.md](BMS_FORMAT.md)에 있고, 이 문서는 **파서가 그걸 어떻게 읽는지**를 설명합니다.
예전 문서(옛 `PARSING_ALGORITHM.md`)의 `#BPMxx`/BPM 변화 처리, "모르는 값은 일반 노트" 규칙, `Main.ScanAndParseBmsFiles`
초기 스캔은 현재 코드에 없습니다.

## 목차

1. [언제 파싱되나](#언제-파싱되나)
2. [공개 API](#공개-api)
3. [파싱 단계](#파싱-단계)
4. [결과 구조](#결과-구조)
5. [캐시](#캐시)
6. [테스트](#테스트)
7. [현재 구현의 제한사항](#현재-구현의-제한사항)

---

## 언제 파싱되나

**플레이 시작 시 한 번**입니다. 게임 시작 시 `hwa` 전체를 미리 파싱하지 않습니다.

```text
ManagerPlay.FetchBMSToModules (원본)
  -> ManagerPlayHook.FetchBMSToModulesPrefix
     -> BmsParser.ParseBmsFileWithStatistics(customTrack.BmsPath)
     -> CustomChartInjector.SetParsedChart(result)
     -> CustomChartInjector.InjectBmsNotesToLaneData(_bms)
```

결과가 `null`이거나 노트가 0개면 `[ManagerPlayHook] 차트를 읽지 못해 원본 패턴을 유지합니다` 경고를 남기고
도너 트랙의 원래 패턴으로 플레이됩니다.

## 공개 API

| 메서드 | 설명 |
| --- | --- |
| `ParseBmsFileWithStatistics(string filePath)` | 파일을 읽어 `ParseResult` 반환. 경로가 비었거나 파일이 없으면 `null`. 캐시 사용 |
| `ParseBmsFile(string filePath)` | 위 결과의 `Notes`만 반환(실패 시 빈 리스트) |
| `ParseBmsFromText(string text, string sourceName = "inline")` | 문자열을 줄 단위로 나눠 파싱. `sourceName`은 현재 쓰이지 않음 |
| `ParseBmsFromLines(string[] lines)` | 실제 파싱 본체. 예외가 나면 `[BmsParser] 파싱 오류` 로그 후 `null` |

## 파싱 단계

`ParseBmsFromLines`는 줄 배열을 세 번 훑습니다.

### 1) 기본 BPM 찾기 (`FindBaseBpm`)

`#BPM`으로 시작하는 줄을 공백/탭으로 나눠, 키가 정확히 `BPM`이고 값이 **0 초과 10만 이하의 숫자**인 **첫 줄**의 값을 씁니다
(`CultureInfo.InvariantCulture`). 없으면 `150`. 줄 순서와 무관하므로 `#BPM`이 데이터 줄 뒤에 있어도 됩니다.
`float.TryParse`는 `Infinity`도 받아들이는데, 그대로 쓰면 BPM이 무한대가 되어 모든 노트 시각이 0초가 되므로 상한(`MaxBpm = 100000`)을 둡니다
(범위를 벗어난 줄은 건너뛰고 다음 `#BPM`을 봅니다).

### 2) 값 너비 판별 (`DetectNoteValueWidth`)

`#WAV`로 시작하는 줄의 키(첫 공백 전까지, `#` 제외)가 **`WAV` + 영숫자 3자리**(`WAV001`, `WAV00A`)면 3, 아니면 2.
파일에 그런 줄이 하나라도 있으면 **모든 데이터 줄을 3글자 단위**로 읽습니다. 길이만 6인 `#WAVCMD` 같은 명령 줄은 키음 번호가 아니라서 제외합니다
(`IsExtendedWavKey`). 예전에는 길이만 봐서 이런 줄이 있으면 `#00111:0100` 같은 일반 데이터 줄이 `010`+`0`으로 읽혀 노트가 0개가 됐습니다.
### 3) 데이터 줄 파싱 (`ParseNoteData`)

`#`로 시작하고 두 번째 글자 이후에 `:`가 있는 줄마다:

1. `채널부 = # 다음 ~ : 앞`, `데이터 = : 뒤`
2. **채널부 형식 검사**(`IsValidChannelPart`): `숫자(마디) + 영숫자 두 글자`여야 합니다. 공백이나 다른 문자가 섞인 줄(`#TITLE Remix 2011:0101`)은
   헤더로 보고 건너뜁니다(예전에는 마지막 두 글자 `11`을 레인 채널로 오인해 가짜 노트가 생겼음).
3. 채널 번호 = 채널부의 **마지막 두 글자**. 레인 채널(`11`~`16`, `18`)이나 `04`/`05`가 아니면 건너뜀
4. 마디 = 채널부의 앞부분(숫자). 없으면 0
5. 데이터를 값 너비로 나눠 각 칸마다
   - 전부 `0`이면 건너뜀
   - 3글자 모드에서 첫 글자가 `0`이면 떼어낸 값으로 종류 판별
   - `01`~`05`가 아니면 건너뜀
   - `04`/`05`면 레인 9, 아니면 채널로 레인 결정(`04`/`05` 채널에 `01`~`03`이 있으면 건너뜀)
   - `Time = (마디 + 칸/칸수) × 240 / BPM` 으로 `ParsedNote` 추가
### 4) 통계와 홀드 짝 맞추기

1. `BuildStatistics`: 종류별 개수(`NormalNotes`, `LongNotes`, `HoldEndNotes`, `OpenNotes`, `CloseNotes`)
2. `PairHoldNotes`: 레인별로 시간순 정렬 → 시작 뒤 첫 끝과 짝 → 시작 노트의 `Length` 설정.
   짝 없는 시작은 `MissingEndNotes`, 짝 없는 끝은 `OrphanEndNotes`에 레인/시간/종류를 기록
3. 끝 노트(`HoldEnd`, `Close`)를 리스트에서 제거
4. `TotalNotes = 남은 노트 수`

결과 노트 리스트는 **정렬되어 있지 않습니다**(파일에 나온 순서). 레인별 정렬은 `CustomChartInjector`가 합니다.
## 결과 구조

```csharp
public class ParseResult
{
    public float BaseBpm { get; set; }
    public List<ParsedNote> Notes { get; set; }
    public ParseStatistics Statistics { get; set; }
}

public class ParsedNote
{
    public float Time { get; set; }               // 초
    public int Lane { get; set; }                 // 0~6, 오픈은 9
    public NoteType NoteType { get; set; }        // Normal / Long / Open (끝 노트는 제거됨)
    public float Length { get; set; }             // 홀드 길이(초), 짝이 없으면 0
    public string OriginalNoteValue { get; set; } // 원본 값 ("01", "002" 등)
}
```

`ParseStatistics`의 `MissingEndNotes`/`OrphanEndNotes`는 `CustomChartInjector`가 경고 로그(레인/시각 예시 최대 5개)로 출력합니다.
`MissingEndNote.NoteType`은 짝이 없는 시작 노트의 종류(`"Long"`/`"Open"`)입니다.
## 캐시

- 키: `Path.GetFullPath(filePath)` (대소문자 무시), 버전: `File.GetLastWriteTimeUtc(...).Ticks`
- 같은 파일·같은 수정 시각이면 **같은 `ParseResult` 인스턴스**를 돌려줍니다. 리트라이 때 다시 파싱하지 않습니다.
- 결과 객체는 공유되므로 호출하는 쪽에서 `Notes`를 수정하면 안 됩니다(현재 `CustomChartInjector`는 읽기만 함).
- 파싱 실패(`null`)는 캐시하지 않습니다.

## 테스트

`sxtg2.LogicTests`가 `BmsParser.cs`와 `ConfigParsing.cs`를 링크해서 검증합니다(`run-logic-tests.bat`, 현재 13개).

- 기본 노트와 홀드 길이, 첫 노트 시각(마디 1 = 1.6초 @150BPM)
- 3글자 값 모드(`#WAV001`), `#WAVCMD` 같은 명령 줄은 3글자 모드를 켜지 않음
- `#BPM`이 데이터 줄 뒤에 있어도 인식, `#BPM Infinity`는 건너뜀(다음 유효한 `#BPM` 또는 150)
- 오픈/클로즈 짝(레인 9), 끝 없는 홀드/오픈 노트의 `MissingEndNotes` 기록
- 끝 누락/고아 끝 통계
- 헤더 줄의 콜론이 노트로 읽히지 않음
- 같은 경로 캐시(동일 참조)
- 설정 값 파서(켜기/끄기, 숫자 범위, 위치, 모양, 줄 끝 주석 제거)
## 현재 구현의 제한사항

1. **BPM 하나만 사용** — BPM 변화(`03`/`08`), `#BPMxx`, STOP(`09`) 미지원.
2. **마디 길이(`02`) 무시** — 쓰면 이후 타이밍이 전부 어긋남.
3. **폴더당 BMS 하나** — `BMS_SELECTION.md` 참고.
4. **인코딩** — UTF-8(BOM 인식)로 읽습니다. 데이터 줄은 ASCII라 문제없지만, 헤더는 어차피 `#BPM`/`#WAV` 외에는 쓰지 않습니다.
5. 끝 없는 홀드/오픈 노트는 파서가 `Length=0`으로 남기고, 처리는 주입 단계에서 합니다(일반 노트로 변환 / 기본 길이 1초, 경고 로그).
   `BMS_FORMAT.md`의 "홀드 짝 맞추기"와 "게이트와 오픈 노트" 참고.
## 관련 문서

- [BMS_FORMAT.md](BMS_FORMAT.md): 입력 형식
- [BMS_SELECTION.md](BMS_SELECTION.md): 어떤 BMS가 선택되는지
- [NOTE_SYSTEM.md](NOTE_SYSTEM.md): 파싱 결과를 게임 노트로 바꾸는 과정
- [GAME_LOGIC.md](GAME_LOGIC.md): 게임 쪽 노트/판정 구조
