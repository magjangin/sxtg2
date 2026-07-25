using System;
using System.Collections.Generic;
using MelonLoader;
using RhythmGame;
using sxtg2.Loaders;

namespace sxtg2.Processors
{
    public static class CustomChartInjector
    {
        private const float DefaultBpm = 150f;
        private static BmsParser.ParseResult _parsedChart;

        public static void SetParsedChart(BmsParser.ParseResult chart)
        {
            _parsedChart = chart;
        }

        public static void InjectBmsNotesToLaneData(SXGTData data)
        {
            if (data == null)
            {
                MelonLogger.Warning("[CustomChartInjector] SXGTData가 null입니다.");
                return;
            }

            if (_parsedChart?.Notes == null || _parsedChart.Notes.Count == 0)
            {
                MelonLogger.Warning("[CustomChartInjector] 파싱된 노트가 없습니다.");
                return;
            }

            try
            {
                ClearLaneData(data);

                int totalNotes = 0;
                int totalNoteWithTicks = 0;
                float bpm = _parsedChart.BaseBpm > 0f ? _parsedChart.BaseBpm : DefaultBpm;

                foreach (var parsedNote in _parsedChart.Notes)
                {
                    Note note = CreateNote(parsedNote, bpm);
                    if (note == null || !data.laneData.TryGetValue(parsedNote.Lane, out var lane))
                        continue;

                    lane.Add(note);

                    if (parsedNote.Lane == 9 || parsedNote.Lane == 10)
                        continue;

                    totalNotes++;
                    totalNoteWithTicks++;

                    if (note is HoldNote holdNote &&
                        (holdNote.nColor == NoteColor.BLUE || holdNote.nColor == NoteColor.RED))
                    {
                        totalNoteWithTicks += holdNote.tickLength;
                    }
                }

                foreach (var lane in data.laneData.Values)
                {
                    lane.Sort((left, right) => left.timing.CompareTo(right.timing));
                }

                data.bpm.Clear();
                data.bpm.Add(bpm);
                data.totalNotes = totalNotes;
                data.totalNoteWithTicks = totalNoteWithTicks;
                data.scorePerNote = totalNotes > 0 ? data.maxScore / totalNotes : 0f;

                MelonLogger.Msg(
                    $"[CustomChartInjector] {_parsedChart.Notes.Count}개 주입, " +
                    $"totalNotes={totalNotes}, totalNoteWithTicks={totalNoteWithTicks}, BPM={bpm}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[CustomChartInjector] 노트 주입 실패: {ex}");
            }
        }

        private static void ClearLaneData(SXGTData data)
        {
            foreach (var lane in data.laneData.Values)
            {
                lane.Clear();
            }

            foreach (var laneNumber in new List<int>(data.unfinished.Keys))
            {
                data.unfinished[laneNumber] = null;
            }
        }

        private static Note CreateNote(BmsParser.ParsedNote parsedNote, float bpm)
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
                    var holdNote = new HoldNote(parsedNote.Time, parsedNote.Lane)
                    {
                        nColor = ResolveColor(parsedNote),
                        nAction = NoteAction.NONE
                    };

                    if (parsedNote.Length > 0f)
                    {
                        holdNote.FinishHoldNote(parsedNote.Length, bpm);
                    }

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
