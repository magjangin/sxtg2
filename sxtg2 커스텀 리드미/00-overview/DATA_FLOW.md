# 데이터 흐름

기준일: 2026-10-05 (v1.1.0)

파일이 어떤 코드를 거쳐 어떤 게임 데이터가 되는지를 데이터 관점으로 정리합니다. 시간 순서는 [DOCUMENTATION.md](DOCUMENTATION.md).

## BMS 파일 → 게임 노트

```text
hwa\{앨범}\*.bms|bme|bml  (폴더당 첫 번째 하나)
  -> TrackDataAnalyzer.FindFirstBms            (곡 선택 씬, 경로만 CustomTrackData.BmsPath에 저장)
  -> BmsParser.ParseBmsFileWithStatistics      (플레이 시작 시, 경로+수정시각 캐시)
       ParseResult { BaseBpm, Notes: List<ParsedNote>, Statistics }
  -> CustomChartInjector.SetParsedChart / InjectBmsNotesToLaneData
       임시 리스트에 ShortNote / HoldNote(+FinishHoldNote) 생성 (끝이 없는 홀드는 ShortNote, 끝이 없는 OPEN은 1초 HoldNote)
  -> (성공했을 때만) SXGTData.laneData[레인]    (게임이 도너 패턴으로 만든 인스턴스를 재사용)
     SXGTData.bpm, totalNotes, totalNoteWithTicks, scorePerNote, trackStartTiming(=0) 갱신
  -> NoteGenerator / RG_PS_Judgement            (게임 원본이 FetchBMS로 가져감)
```

## 곡 정보 → TrackData

```text
hwa\{앨범}\{BMS파일명}.txt | trackinfo.txt | info.txt | 첫 *.txt
  -> TrackInfoParser.ParseTrackInfo            TrackInfo { Title, Artist, Difficulties }
  -> TrackDataAnalyzer.CreateCustomTrack
       CustomTrackData : TrackData
         ID = CUSTOM_{FNV-1a 해시(앨범/파일명)}
         DisplayName/AlphabetName = Title ?? 앨범 폴더 이름 ?? "커스텀 차트"
         Composer/ComposerEN = Artist ?? 도너 작곡가
         Level/Level_LITE = Difficulties (4칸) ?? 도너 값
         AlbumFolder, BmsPath, ResourceDonor = trackDatas[0]
  -> ManagerMusicSelect.trackDatas.Add(...)  (+ TrackDataAnalyzer.CustomTracksById에 등록: Util.FindTrackByID 보완용)
```

## 미디어 파일 → 재생 대상

```text
music.ogg|mp3|wav (없으면 가장 큰 오디오) -> BgmFileResolver -> BGMPlayerHook (UnityWebRequest) -> ManagerPlay.bgm.clip
*.mp4 (이름순 첫 번째)                -> BgaFileResolver -> BGAPlayerHook                    -> ManagerPlay.bgaPlayer.url
demo.* -> music.* -> 가장 큰 오디오 -> BgmFileResolver.FindPreviewForAlbum -> 미리듣기 코루틴 -> ManagerMusicSelect.previewSource.clip
thumb.png | thumbnail.png | jacket.png | cover.png | image.png
                                     -> ThumbnailLoader -> CustomTrackData.CustomJacket -> TrackData.GetJacketSprite()/GetThumbSprite() 결과
```

모든 파일 경로는 `MediaUrl.FromPath`로 `file://` URL이 됩니다(`%`, `#`, `?`만 이스케이프).
매 프레임 `BGABGMSyncHook`이 `bgm.time`을 기준으로 `bgaPlayer.time`/`playbackSpeed`를 맞춥니다(영상이 BGM보다 먼저 끝났으면 건드리지 않음).

## 도너 트랙 리소스 → 커스텀 트랙

```text
ResourceDonor.GetAudioClip()               -> 커스텀 트랙.GetAudioClip()        (커스텀 BGM 로드 전 기본 BGM)
ResourceDonor.GetLoadingAnimation()        -> 커스텀 트랙.GetLoadingAnimation()
ResourceDonor.GetSixtarPatternDirectory()  -> 커스텀 트랙.GetSixtarPatternDirectory() (게임이 먼저 읽는 패턴, 이후 교체됨)
```

## 노트 스킨

```text
CustomNotes\*.png  -> CustomNoteSpriteLoader.Initialize (모드 시작 시 1회, 키 = 파일명)
  -> NoteSpriteHook.GeneratePostfix: 노트 이름에서 타입 추출 (`(Clone)` 접미사는 떼고 비교)
       -> shortNote/tailNote/holdTexture Image.sprite 교체 + 스킨이 있는 Image만 SetNativeSize 후 noteSize/100 배율
```

노트 이름(`_Blue(Clone)`)에서 `(Clone)`을 떼고 파일명과 비교하므로 `blue.png` 같은 파일이 실제로 적용됩니다
(예전에는 `(Clone)`이 남아 있어 적용되지 않았음). 규칙은 [../02-systems/NOTE_SYSTEM.md](../02-systems/NOTE_SYSTEM.md).

## 설정

```text
SaveCustomKey\config.txt
  -> SaveCustomKeyConfig.Initialize        (모드 시작 시; 없으면 기본 파일 생성)
  -> SaveCustomKeyConfig.Reload            (플레이 씬 진입마다: 기본값으로 리셋 후 다시 파싱, 바뀐 항목만 로그)
       줄마다 ConfigParsing.StripInlineComment (` #`/` //` 이후 제거, `#RRGGBB`는 유지) -> 키별 처리기 표(SettingHandlers)
       모르는 키 / 값이 틀린 줄 / 알 수 없는 불리언 단어는 경고
  -> 정적 속성 (AutoPlay, EnableJudgmentBar, MaxScore, NoteSway …)
  -> 각 훅/오버레이가 매 프레임 읽음

UserData\MelonPreferences.cfg [sxtg2]
  -> ModLog.RegisterPreferences
       LogLevel                 -> ModLog.Level
       EnableAutoPlay / EnableAllPerfect
                                -> config.txt 값과 OR 결합 (ModLog.EnableAutoPlay 등)
       BlockSaveBestRanking     -> 쓰지 않음 (항목만 남아 있음, 저장 차단은 config.txt의 BlockSave만 따름)
```

`config.txt`에 없는 새 항목(판정바 모양/위치, 키뷰어 색, MaxScore, NoteSway, NoteSpeedChaos 묶음)은 게임 시작 때의
최초 로드(`Initialize`)에서만 파일 끝에 기본값 줄로 자동 추가됩니다. 플레이 씬 진입 재로드(`Reload`)는 파일을 수정하지 않습니다.

## 판정 데이터 → 판정바

```text
RG_PS_Judgement.TryJudgeShortNote
  -> ManagerPlay.WidgeInvoke(pw => pw.OnGetJudge(등급, note.timing - judgeTime))
  -> FastSlowMeter_OnGetJudge_Patch.Postfix   (장착 위젯 수만큼 호출됨)
  -> JudgmentBar.RegisterHit(오차초, 등급)     같은 프레임·같은 (등급, 오차) 중복은 무시, HitHistory (1.5초 보관)
  -> JudgmentBar.DrawJudgmentBar (OnGUI)
RG_PS_Judgement.JudgeRange -> JudgmentBar.RefreshJudgeRange (매 프레임) -> 배경 박스/스케일
```

## 점수

```text
CustomChartInjector -> SXGTData.totalNotes / totalNoteWithTicks
RG_PS_Judgement.Update:  JudgeScore = Lerp(0, GetMaxScore(), JudgeRatio / totalNotes)   // JudgeScoreMaxHook
ResultSaveBlockHook:     조건부로 UserAccountModule.SavePlayData / LyrebirdServer.PostUserScore 건너뜀
                         (오토/올퍼펙트/MaxScore 변경/커스텀 곡은 항상, 그 밖엔 BlockSave)
ResultTaintHook:         같은 조건(BlockSave 제외)에서 플레이 횟수·실패 횟수·마지막 플레이 곡을 되돌리고 업적 요청을 막음
```
