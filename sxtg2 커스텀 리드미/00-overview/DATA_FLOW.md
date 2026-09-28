# 데이터 흐름

기준일: 2026-09-28 (v1.1.0)

파일이 어떤 코드를 거쳐 어떤 게임 데이터가 되는지를 데이터 관점으로 정리합니다. 시간 순서는 [DOCUMENTATION.md](DOCUMENTATION.md).

## BMS 파일 → 게임 노트

```text
hwa\{앨범}\*.bms|bme|bml  (폴더당 첫 번째 하나)
  -> TrackDataAnalyzer.FindFirstBms            (곡 선택 씬, 경로만 CustomTrackData.BmsPath에 저장)
  -> BmsParser.ParseBmsFileWithStatistics      (플레이 시작 시, 경로+수정시각 캐시)
       ParseResult { BaseBpm, Notes: List<ParsedNote>, Statistics }
  -> CustomChartInjector.SetParsedChart / InjectBmsNotesToLaneData
       ShortNote / HoldNote(+FinishHoldNote)
  -> SXGTData.laneData[레인]                   (게임이 도너 패턴으로 만든 인스턴스를 재사용)
     SXGTData.bpm, totalNotes, totalNoteWithTicks, scorePerNote 갱신
  -> NoteGenerator / RG_PS_Judgement            (게임 원본이 FetchBMS로 가져감)
```

## 곡 정보 → TrackData

```text
hwa\{앨범}\{BMS파일명}.txt | trackinfo.txt | info.txt | 첫 *.txt
  -> TrackInfoParser.ParseTrackInfo            TrackInfo { Title, Artist, Difficulties }
  -> TrackDataAnalyzer.CreateCustomTrack
       CustomTrackData : TrackData
         ID = CUSTOM_{FNV-1a 해시(앨범/파일명)}
         DisplayName/AlphabetName = Title ?? "커스텀 차트"
         Composer/ComposerEN = Artist ?? 도너 작곡가
         Level/Level_LITE = Difficulties (4칸) ?? 도너 값
         AlbumFolder, BmsPath, ResourceDonor = trackDatas[0]
  -> ManagerMusicSelect.trackDatas.Add(...)
```

## 미디어 파일 → 재생 대상

```text
music.ogg|mp3|wav (없으면 첫 오디오)  -> BgmFileResolver -> BGMPlayerHook (UnityWebRequest) -> ManagerPlay.bgm.clip
*.mp4 (첫 번째)                      -> BgaFileResolver -> BGAPlayerHook                    -> ManagerPlay.bgaPlayer.url
demo.* -> music.* -> 첫 오디오        -> BgmFileResolver.FindPreviewForAlbum -> 미리듣기 코루틴 -> ManagerMusicSelect.previewSource.clip
thumb.png | thumbnail.png | jacket.png | cover.png | image.png
                                     -> ThumbnailLoader -> CustomTrackData.CustomJacket -> TrackData.GetJacketSprite()/GetThumbSprite() 결과
```

매 프레임 `BGABGMSyncHook`이 `bgm.time`을 기준으로 `bgaPlayer.time`/`playbackSpeed`를 맞춥니다.

## 도너 트랙 리소스 → 커스텀 트랙

```text
ResourceDonor.GetAudioClip()               -> 커스텀 트랙.GetAudioClip()        (커스텀 BGM 로드 전 기본 BGM)
ResourceDonor.GetLoadingAnimation()        -> 커스텀 트랙.GetLoadingAnimation()
ResourceDonor.GetSixtarPatternDirectory()  -> 커스텀 트랙.GetSixtarPatternDirectory() (게임이 먼저 읽는 패턴, 이후 교체됨)
```

## 노트 스킨

```text
CustomNotes\*.png  -> CustomNoteSpriteLoader.Initialize (모드 시작 시 1회, 키 = 파일명)
  -> NoteSpriteHook.GeneratePostfix: 노트 이름에서 타입 추출 -> shortNote/tailNote/holdTexture Image.sprite
  -> NoteRendererRecovery: SetNativeSize + SetAllDirty
```

현재 노트 이름(`_Blue(Clone)`)과 파일명 규칙이 맞지 않는 알려진 문제가 있습니다([../02-systems/NOTE_SYSTEM.md](../02-systems/NOTE_SYSTEM.md)).

## 설정

```text
SaveCustomKey\config.txt
  -> SaveCustomKeyConfig.Initialize        (모드 시작 시; 없으면 기본 파일 생성)
  -> SaveCustomKeyConfig.Reload            (플레이 씬 진입마다: 기본값으로 리셋 후 다시 파싱, 바뀐 항목만 로그)
  -> 정적 속성 (AutoPlay, EnableJudgmentBar, MaxScore, NoteSway …)
  -> 각 훅/오버레이가 매 프레임 읽음

UserData\MelonPreferences.cfg [sxtg2]
  -> ModLog.RegisterPreferences
       LogLevel                 -> ModLog.Level
       EnableAutoPlay / EnableAllPerfect / BlockSaveBestRanking
                                -> config.txt 값과 OR 결합 (ModLog.EnableAutoPlay 등)
```

`config.txt`에 없는 새 항목(판정바 모양/위치, 키뷰어 색, MaxScore, NoteSway, NoteSpeedChaos 묶음)은 로드할 때
파일 끝에 기본값 줄로 자동 추가됩니다.

## 판정 데이터 → 판정바

```text
RG_PS_Judgement.TryJudgeShortNote
  -> ManagerPlay.WidgeInvoke(pw => pw.OnGetJudge(등급, note.timing - judgeTime))
  -> FastSlowMeter_OnGetJudge_Patch.Postfix   (장착 위젯 수만큼 호출됨)
  -> JudgmentBar.RegisterHit(오차초, 등급)     HitHistory (1.5초 보관)
  -> JudgmentBar.DrawJudgmentBar (OnGUI)
RG_PS_Judgement.JudgeRange -> JudgmentBar.RefreshJudgeRange (매 프레임) -> 배경 박스/스케일
```

## 점수

```text
CustomChartInjector -> SXGTData.totalNotes / totalNoteWithTicks
RG_PS_Judgement.Update:  JudgeScore = Lerp(0, GetMaxScore(), JudgeRatio / totalNotes)   // JudgeScoreMaxHook
ResultSaveBlockHook:     조건부로 ComparePlayResultHighScore / PostRequestPlayResult 건너뜀
```
