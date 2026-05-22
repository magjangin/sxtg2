# 용어집(Glossary) - sxtg2 문서 공통

문서가 늘어나면 용어가 헷갈리기 쉬워서, 공통 용어를 한 곳에 모아둡니다.

## 게임/데이터 구조

- **TrackData**: 곡 메타데이터(ID, 제목 표시용 문자열 등)를 담는 게임 쪽 타입
- **DisplayName**: 모드가 제목을 넣을 때는 인스턴스 **필드** `DisplayName`에 리플렉션 `SetValue`로 기록한다(`TrackDataAnalyzer.Inject`). 다른 코드에서 값을 읽을 때는 필드·프로퍼티 후보를 순서대로 시도하는 경로가 있다.
- **SXGTData**: 게임의 “차트 데이터”를 담는 핵심 객체(레인별 노트 리스트 등)
- **laneData**: `Dictionary<int, List<Note>>` 형태로 추정되는 레인별 노트 저장소
- **Note / ShortNote / HoldNote**: 게임 내부 노트 타입(일반/홀드)
- **nType**: 노트 타입 enum (예: `SHORT`, `HOLD`)
- **nColor**: 노트 색상 enum (예: `BLUE`, `RED`, `OPEN`)
- **duration**: 홀드 지속 시간(초)
- **tickTime**: 홀드 중간 판정/틱 타이밍 배열

## BMS 관련

- **Measure(마디)**: BMS의 `#MMMCC:...`에서 `MMM`에 해당
- **Channel(채널)**: BMS의 `CC`에 해당
- **Tick**: `measure + (slotIndex / totalSlots)`로 계산되는 위치 값(실수)
- **OPEN/CLOSE(04/05)**: 오픈 노트 쌍(레인 9로 매핑되는 특수 홀드)

## 모드/후킹 관련

- **Hook**: Harmony로 게임 메서드를 가로채는 패치 코드
- **Prefix/Postfix**: Harmony 패치 위치(원본 호출 전/후)
- **OnPlaySceneStart**: `ManagerPlay`의 주요 메서드 후킹 이후 공통으로 수행하는 플레이 시작 처리(주입/미디어 교체 등)

## 폴더/리소스

- **hwa 폴더**: 커스텀 리소스(BMS/BGM/BGA/썸네일/텍스트)를 두는 게임 폴더 하위 디렉토리
- **앨범 폴더**: `hwa` 아래 1단계 하위 폴더(트랙 매칭/리소스 탐색에 사용)





