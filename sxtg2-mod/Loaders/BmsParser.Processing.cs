using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace sxtg2.Loaders
{
    public static partial class BmsParser
    {
        private static void ParseNoteData(string channel, string data, List<ParsedNote> notes, List<BpmData> dataList)
        {
            try
            {
                ParseNoteChannelHeader(channel, out int measure, out string channelNum);

                if (!LaneMapping.ContainsKey(channelNum) && channelNum != "04" && channelNum != "05")
                    return;

                var objLength = data.Length / 2;
                if (objLength == 0)
                    return;

                AppendParsedNotesFromChannelData(data, measure, channelNum, objLength, notes, dataList);
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[BmsParser] 노트 데이터 파싱 오류: {ex.Message}");
            }
        }

        private static void ParseNoteChannelHeader(string channel, out int measure, out string channelNum)
        {
            measure = 0;
            channelNum = channel;

            if (channel.Length >= 5)
            {
                var measureStr = channel.Substring(0, channel.Length - 2);
                channelNum = channel.Substring(channel.Length - 2);
                if (int.TryParse(measureStr, out int m))
                    measure = m;
            }
            else if (channel.Length >= 2)
            {
                channelNum = channel.Substring(channel.Length - 2);
                measure = 0;
            }
        }

        private static void AppendParsedNotesFromChannelData(string data, int measure, string channelNum, int objLength, List<ParsedNote> notes, List<BpmData> dataList)
        {
            for (int i = 0; i < data.Length; i += 2)
            {
                if (i + 1 >= data.Length)
                    break;

                var noteValue = data.Substring(i, 2);
                if (noteValue == "00")
                    continue;
                if (!NoteTypeMapping.ContainsKey(noteValue))
                    continue;

                var noteType = NoteTypeMapping[noteValue];
                if (!TryResolveNoteLane(noteType, channelNum, out int lane))
                    continue;

                var tick = (float)measure + ((float)(i / 2) / objLength);
                var time = CalculateTime(tick, dataList);

                notes.Add(new ParsedNote
                {
                    Time = time,
                    Lane = lane,
                    NoteType = noteType,
                    OriginalNoteValue = noteValue
                });
            }
        }

        private static bool TryResolveNoteLane(NoteType noteType, string channelNum, out int lane)
        {
            if (noteType == NoteType.Open || noteType == NoteType.Close)
            {
                lane = 9;
                return true;
            }

            if (LaneMapping.ContainsKey(channelNum))
            {
                lane = LaneMapping[channelNum];
                return true;
            }

            lane = 0;
            return false;
        }

        private static float CalculateTime(float tick, List<BpmData> dataList)
        {
            if (dataList == null || dataList.Count == 0)
            {
                return tick * 0.4f;
            }

            var firstBpm = dataList[0];
            return tick * 4f * firstBpm.Freq;
        }

        private static void AccumulateNoteTypeStatistics(List<ParsedNote> notes, ParseStatistics statistics)
        {
            foreach (var note in notes)
            {
                switch (note.NoteType)
                {
                    case NoteType.Normal:
                        statistics.NormalNotes++;
                        break;
                    case NoteType.Long:
                        statistics.LongNotes++;
                        break;
                    case NoteType.Open:
                        statistics.OpenNotes++;
                        break;
                    case NoteType.HoldEnd:
                        statistics.HoldEndNotes++;
                        break;
                    case NoteType.Close:
                        statistics.CloseNotes++;
                        break;
                }
            }
        }

        private static void CalculateHoldNoteLengths(List<ParsedNote> notes, ParseStatistics statistics)
        {
            var notesByLane = notes.GroupBy(n => n.Lane).ToDictionary(g => g.Key, g => g.OrderBy(n => n.Time).ToList());

            foreach (var laneGroup in notesByLane)
            {
                if (laneGroup.Key != 9)
                    ApplyStandardLaneHoldLengths(laneGroup.Key, laneGroup.Value, statistics);
                else
                    ApplyOpenLaneHoldLengths(laneGroup.Value, statistics);
            }
        }

        private static void ApplyStandardLaneHoldLengths(int lane, List<ParsedNote> laneNotes, ParseStatistics statistics)
        {
            ParsedNote pendingLongNote = null;

            for (int i = 0; i < laneNotes.Count; i++)
            {
                var note = laneNotes[i];

                if (note.NoteType == NoteType.Long)
                {
                    if (pendingLongNote != null)
                    {
                        statistics.MissingEndNotes.Add(new MissingEndNoteInfo
                        {
                            Lane = lane,
                            Time = pendingLongNote.Time,
                            NoteType = "Long"
                        });
                    }
                    pendingLongNote = note;
                }
                else if (note.NoteType == NoteType.HoldEnd)
                {
                    if (pendingLongNote != null)
                    {
                        pendingLongNote.Length = note.Time - pendingLongNote.Time;
                        pendingLongNote = null;
                    }
                    else
                    {
                        statistics.OrphanEndNotes.Add(new MissingEndNoteInfo
                        {
                            Lane = lane,
                            Time = note.Time,
                            NoteType = "HoldEnd"
                        });
                    }
                }
            }

            if (pendingLongNote != null)
            {
                statistics.MissingEndNotes.Add(new MissingEndNoteInfo
                {
                    Lane = lane,
                    Time = pendingLongNote.Time,
                    NoteType = "Long"
                });
            }
        }

        private static void ApplyOpenLaneHoldLengths(List<ParsedNote> laneNotes, ParseStatistics statistics)
        {
            const int lane = 9;
            ParsedNote pendingOpenNote = null;

            for (int i = 0; i < laneNotes.Count; i++)
            {
                var note = laneNotes[i];

                if (note.NoteType == NoteType.Open)
                {
                    if (pendingOpenNote != null)
                    {
                        statistics.MissingEndNotes.Add(new MissingEndNoteInfo
                        {
                            Lane = lane,
                            Time = pendingOpenNote.Time,
                            NoteType = "Open"
                        });
                    }
                    pendingOpenNote = note;
                }
                else if (note.NoteType == NoteType.Close)
                {
                    if (pendingOpenNote != null)
                    {
                        pendingOpenNote.Length = note.Time - pendingOpenNote.Time;
                        pendingOpenNote = null;
                    }
                    else
                    {
                        statistics.OrphanEndNotes.Add(new MissingEndNoteInfo
                        {
                            Lane = lane,
                            Time = note.Time,
                            NoteType = "Close"
                        });
                    }
                }
            }

            if (pendingOpenNote != null)
            {
                statistics.MissingEndNotes.Add(new MissingEndNoteInfo
                {
                    Lane = lane,
                    Time = pendingOpenNote.Time,
                    NoteType = "Open"
                });
            }
        }

        /// <summary>
        /// 텍스트에서 직접 BMS를 파싱합니다.
        /// </summary>
        public static ParseResult ParseBmsFromText(string bmsText, string displayName = "BMS Text")
        {
            var notes = new List<ParsedNote>();
            var dataList = new List<BpmData>();
            var bpmDict = new Dictionary<string, float>();
            var statistics = new ParseStatistics();

            try
            {
                using (var stringReader = new StringReader(bmsText))
                {
                    string line;
                    while ((line = stringReader.ReadLine()) != null)
                        ProcessBmsTextInputLine(line, notes, dataList, bpmDict);
                }

                dataList.Sort((a, b) => a.Tick.CompareTo(b.Tick));

                AccumulateNoteTypeStatistics(notes, statistics);
                statistics.TotalNotes = notes.Count;

                CalculateHoldNoteLengths(notes, statistics);
                notes.RemoveAll(n => n.NoteType == NoteType.HoldEnd || n.NoteType == NoteType.Close);
                statistics.TotalNotes = notes.Count;

                return new ParseResult
                {
                    Notes = notes,
                    Statistics = statistics
                };
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[BmsParser] BMS 파싱 실패 ({displayName}): {ex.Message}");
                return null;
            }
        }

        private static void ProcessBmsTextInputLine(string line, List<ParsedNote> notes, List<BpmData> dataList, Dictionary<string, float> bpmDict)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || !line.StartsWith("#"))
                return;

            if (line.Contains(' '))
            {
                var split = line.Substring(1).Split(new[] { ' ' }, 2);
                if (split.Length >= 2)
                {
                    var key = split[0];
                    var value = split[1];

                    if (key.Contains("BPM"))
                    {
                        if (key == "BPM")
                        {
                            if (float.TryParse(value, out float bpm))
                                bpmDict["00"] = bpm;
                        }
                        else if (key.Length == 5 && key.StartsWith("BPM"))
                        {
                            var bpmId = key.Substring(3);
                            if (float.TryParse(value, out float bpm))
                                bpmDict[bpmId] = bpm;
                        }
                    }
                }
                return;
            }

            if (!line.Contains(':'))
                return;

            var colonIndex = line.IndexOf(':');
            if (colonIndex < 1)
                return;

            var keyPart = line.Substring(1, colonIndex - 1);
            var valuePart = line.Substring(colonIndex + 1);

            if (keyPart.Length != 5)
                return;

            var measureStr = keyPart.Substring(0, 3);
            var channelStr = keyPart.Substring(3, 2);

            if (!int.TryParse(measureStr, out int measure) ||
                !int.TryParse(channelStr, out int channel))
                return;

            if (channel == 3 || (channel >= 11 && channel <= 19))
            {
                var channelKey = $"{measure:D3}{channel:D2}";
                ParseNoteData(channelKey, valuePart, notes, dataList);
            }
        }
    }
}
