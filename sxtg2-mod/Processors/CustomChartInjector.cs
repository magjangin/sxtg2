using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using RhythmGame;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static class CustomChartInjector
    {
        private const float DefaultBpm = 150f;

        /// <summary>
        /// 끝(`05`)이 없는 오픈 노트에 주는 기본 길이(초). 게임은 오픈 노트의 길이를 게이트 열림/닫힘 애니메이션 시간으로 쓴다
        /// (`animator.speed = 1f / t`). 0이면 속도가 무한대가 되므로, 애니메이션 원래 속도(1배)가 되는 1초를 준다.
        /// </summary>
        private const float DefaultOpenLength = 1f;

        private const int MaxLoggedExamples = 5;

        private static BmsParser.ParseResult _parsedChart;

        public static void SetParsedChart(BmsParser.ParseResult chart)
        {
            _parsedChart = chart;
        }

        /// <summary>임시 목록에 만든 노트와, 로그/점수 보정에 쓰는 집계.</summary>
        private sealed class BuildResult
        {
            public readonly Dictionary<int, List<Note>> Lanes = new Dictionary<int, List<Note>>();
            public int TotalNotes;
            public int TotalNoteWithTicks;
            public int HoldsWithoutEnd;
            public int OpensWithoutEnd;
            public int SkippedUnknownLane;
        }

        /// <summary>
        /// 파싱한 노트를 SXGTData에 넣는다. 노트는 먼저 임시 목록에 전부 만들고, 끝까지 성공했을 때만 게임 데이터를 바꾼다.
        /// 중간에 예외가 나도 SXGTData는 도너 패턴 그대로 남는다(예전에는 레인을 먼저 비워서 반쯤 빈 차트로 진행했다).
        /// </summary>
        /// <returns>주입에 성공하면 true. 실패하면 도너 패턴이 그대로 쓰인다.</returns>
        public static bool InjectBmsNotesToLaneData(SXGTData data)
        {
            if (data == null)
            {
                MelonLogger.Warning("[CustomChartInjector] SXGTData가 null입니다.");
                return false;
            }

            if (_parsedChart?.Notes == null || _parsedChart.Notes.Count == 0)
            {
                MelonLogger.Warning("[CustomChartInjector] 파싱된 노트가 없습니다.");
                return false;
            }

            try
            {
                float bpm = _parsedChart.BaseBpm > 0f ? _parsedChart.BaseBpm : DefaultBpm;

                BuildResult built = BuildLaneNotes(data, bpm);
                ApplyToGameData(data, built, bpm);

                MelonLogger.Msg(
                    $"[CustomChartInjector] {_parsedChart.Notes.Count}개 주입, " +
                    $"totalNotes={built.TotalNotes}, totalNoteWithTicks={built.TotalNoteWithTicks}, BPM={bpm}, trackStartTiming=0");

                LogChartWarnings(built.HoldsWithoutEnd, built.OpensWithoutEnd, built.SkippedUnknownLane);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 노트 주입 실패 - 도너 패턴을 그대로 사용합니다: {ex}");
                return false;
            }
        }

        /// <summary>게임 데이터는 건드리지 않고, 레인별 노트 목록(시각순)과 점수 보정용 집계를 만든다.</summary>
        private static BuildResult BuildLaneNotes(SXGTData data, float bpm)
        {
            var result = new BuildResult();
            foreach (int lane in data.laneData.Keys)
                result.Lanes[lane] = new List<Note>();

            foreach (var parsedNote in _parsedChart.Notes)
            {
                if (!result.Lanes.TryGetValue(parsedNote.Lane, out var laneNotes))
                {
                    result.SkippedUnknownLane++;
                    continue;
                }

                Note note = CreateNote(parsedNote, bpm, ref result.HoldsWithoutEnd, ref result.OpensWithoutEnd);
                if (note == null)
                    continue;

                laneNotes.Add(note);

                if (parsedNote.Lane == 9 || parsedNote.Lane == 10)
                    continue;

                result.TotalNotes++;
                result.TotalNoteWithTicks++;

                if (note is HoldNote holdNote &&
                    (holdNote.nColor == NoteColor.BLUE || holdNote.nColor == NoteColor.RED))
                {
                    result.TotalNoteWithTicks += holdNote.tickLength;
                }
            }

            // LINQ OrderBy는 안정 정렬이라, 같은 시각의 노트가 파일에 나온 순서를 유지한다.
            foreach (int lane in result.Lanes.Keys.ToList())
                result.Lanes[lane] = result.Lanes[lane].OrderBy(note => note.timing).ToList();

            return result;
        }

        /// <summary>만들어 둔 노트와 집계를 게임의 SXGTData에 한 번에 반영한다.</summary>
        private static void ApplyToGameData(SXGTData data, BuildResult built, float bpm)
        {
            foreach (var pair in built.Lanes)
            {
                var target = data.laneData[pair.Key];
                target.Clear();
                target.AddRange(pair.Value);
            }

            foreach (int laneNumber in new List<int>(data.unfinished.Keys))
                data.unfinished[laneNumber] = null;

            data.bpm.Clear();
            data.bpm.Add(bpm);
            data.totalNotes = built.TotalNotes;
            data.totalNoteWithTicks = built.TotalNoteWithTicks;
            data.scorePerNote = built.TotalNotes > 0 ? data.maxScore / built.TotalNotes : 0f;

            // BGM/BGA는 CurTime >= trackStartTiming에서 시작한다. 이 값은 원래 도너 패턴의 값(`0A` 마커)이라 도너 곡이
            // 바뀌면 모든 커스텀 곡의 싱크가 같이 밀린다. BMS는 0초에 시작하므로 0으로 고정한다.
            data.trackStartTiming = 0f;
        }

        private static void LogChartWarnings(int holdsWithoutEnd, int opensWithoutEnd, int skippedUnknownLane)
        {
            var stats = _parsedChart.Statistics;

            if (holdsWithoutEnd > 0)
            {
                MelonLogger.Warning(
                    $"[CustomChartInjector] 끝(03)이 없는 홀드 {holdsWithoutEnd}개를 일반 노트로 바꿨습니다. " +
                    "채보에서 홀드 시작(02) 뒤에 끝(03)을 넣어 주세요. 위치 예: " + DescribeExamples(stats?.MissingEndNotes, "Long"));
            }

            if (opensWithoutEnd > 0)
            {
                MelonLogger.Warning(
                    $"[CustomChartInjector] 끝(05)이 없는 오픈 노트 {opensWithoutEnd}개에 기본 길이 {DefaultOpenLength:0.#}초를 줬습니다. " +
                    "위치 예: " + DescribeExamples(stats?.MissingEndNotes, "Open"));
            }

            if (stats != null && stats.OrphanEndNotes.Count > 0)
            {
                MelonLogger.Warning(
                    $"[CustomChartInjector] 시작이 없는 끝 노트(03/05) {stats.OrphanEndNotes.Count}개는 무시했습니다. " +
                    "위치 예: " + DescribeExamples(stats.OrphanEndNotes, null));
            }

            if (skippedUnknownLane > 0)
                MelonLogger.Warning($"[CustomChartInjector] 게임에 없는 레인의 노트 {skippedUnknownLane}개를 건너뛰었습니다.");
        }

        private static string DescribeExamples(List<BmsParser.MissingEndNoteInfo> notes, string noteType)
        {
            if (notes == null)
                return "(없음)";

            var parts = notes
                .Where(info => noteType == null || info.NoteType == noteType)
                .Take(MaxLoggedExamples)
                .Select(info => $"레인 {info.Lane} @ {info.Time:0.###}초")
                .ToArray();
            return parts.Length == 0 ? "(없음)" : string.Join(", ", parts);
        }

        private static Note CreateNote(
            BmsParser.ParsedNote parsedNote,
            float bpm,
            ref int holdsWithoutEnd,
            ref int opensWithoutEnd)
        {
            switch (parsedNote.NoteType)
            {
                case BmsParser.NoteType.Normal:
                    return new ShortNote(parsedNote.Time, parsedNote.Lane)
                    {
                        nColor = ResolveColor(parsedNote)
                    };

                case BmsParser.NoteType.Long:
                case BmsParser.NoteType.Open:
                    float length = parsedNote.Length;
                    if (length <= 0f)
                    {
                        if (parsedNote.NoteType == BmsParser.NoteType.Long)
                        {
                            // 끝이 없는 홀드를 HoldNote로 넣으면 FinishHoldNote가 불리지 않아 tickTime이 null이고,
                            // 게임의 CheckHoldTick이 헤드를 친 순간부터 매 프레임 NullReferenceException을 낸다.
                            // 일반 노트로 바꿔서 판정 루프가 멈추지 않게 한다.
                            holdsWithoutEnd++;
                            return new ShortNote(parsedNote.Time, parsedNote.Lane)
                            {
                                nColor = ResolveColor(parsedNote)
                            };
                        }

                        // 오픈 노트는 판정 커서(레인 9)가 HoldNote여야 넘어가므로 일반 노트로 바꿀 수 없다.
                        opensWithoutEnd++;
                        length = DefaultOpenLength;
                    }

                    var holdNote = new HoldNote(parsedNote.Time, parsedNote.Lane)
                    {
                        nColor = ResolveColor(parsedNote),
                        nAction = NoteAction.NONE
                    };
                    holdNote.FinishHoldNote(length, bpm);
                    return holdNote;

                default:
                    return null;
            }
        }

        private static NoteColor ResolveColor(BmsParser.ParsedNote note)
        {
            if (note.NoteType == BmsParser.NoteType.Open)
                return NoteColor.OPEN;

            return note.Lane == 4 || note.Lane == 5
                ? NoteColor.RED
                : NoteColor.BLUE;
        }
    }
}
