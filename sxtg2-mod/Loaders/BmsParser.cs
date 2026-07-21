using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace sxtg2.Loaders
{
    public static partial class BmsParser
    {
        public enum NoteType
        {
            Normal,    // 01
            Long,      // 02 (홀드 시작)
            HoldEnd,   // 03 (홀드 끝)
            Open,      // 04 (오픈 노트)
            Close      // 05 (클로즈 노트)
        }

        public class ParsedNote
        {
            public float Time { get; set; }
            public int Lane { get; set; }
            public NoteType NoteType { get; set; }
            public float Length { get; set; }
            public string OriginalNoteValue { get; set; }
        }

        public class BpmData
        {
            public float Tick { get; set; }
            public float Freq { get; set; }
        }

        public class ParseResult
        {
            public List<ParsedNote> Notes { get; set; }
            public ParseStatistics Statistics { get; set; }
        }

        public class ParseStatistics
        {
            public int TotalNotes { get; set; }
            public int NormalNotes { get; set; }
            public int LongNotes { get; set; }
            public int OpenNotes { get; set; }
            public int HoldEndNotes { get; set; }
            public int CloseNotes { get; set; }
            public List<MissingEndNoteInfo> MissingEndNotes { get; set; } = new List<MissingEndNoteInfo>();
            public List<MissingEndNoteInfo> OrphanEndNotes { get; set; } = new List<MissingEndNoteInfo>();
        }

        public class MissingEndNoteInfo
        {
            public int Lane { get; set; }
            public float Time { get; set; }
            public string NoteType { get; set; } // "Long" 또는 "Open"
        }

        // 레인 매핑: BMS 채널 → 게임 레인
        private static readonly Dictionary<string, int> LaneMapping = new Dictionary<string, int>
        {
            { "16", 0 },
            { "11", 1 },
            { "12", 2 },
            { "13", 3 },
            { "14", 4 },
            { "15", 5 },
            { "18", 6 }
        };

        // 노트 타입 매핑: BMS 노트 값 → NoteType
        private static readonly Dictionary<string, NoteType> NoteTypeMapping = new Dictionary<string, NoteType>
        {
            { "01", NoteType.Normal },
            { "02", NoteType.Long },
            { "03", NoteType.HoldEnd },
            { "04", NoteType.Open },
            { "05", NoteType.Close }
        };

        private static readonly object s_parseStatsFileCacheLock = new object();
        private static readonly Dictionary<string, (long LastWriteUtcTicks, ParseResult Result)> s_parseStatsFileCache =
            new Dictionary<string, (long, ParseResult)>(StringComparer.OrdinalIgnoreCase);
        private const int DefaultNoteValueWidth = 2;
        private const int ExtendedNoteValueWidth = 3;

        public static List<ParsedNote> ParseBmsFile(string filePath)
        {
            var result = ParseBmsFileWithStatistics(filePath);
            return result?.Notes ?? new List<ParsedNote>();
        }

        public static ParseResult ParseBmsFileWithStatistics(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string fullPath = Path.GetFullPath(filePath);
            long versionTicks = File.Exists(fullPath)
                ? new FileInfo(fullPath).LastWriteTimeUtc.Ticks
                : 0L;

            lock (s_parseStatsFileCacheLock)
            {
                if (s_parseStatsFileCache.TryGetValue(fullPath, out var cached) && cached.LastWriteUtcTicks == versionTicks)
                {
                    return cached.Result;
                }
            }

            var notes = new List<ParsedNote>();
            var dataList = new List<BpmData>();
            var bpmDict = new Dictionary<string, float>();
            var statistics = new ParseStatistics();

            try
            {
                var lines = File.ReadAllLines(filePath);
                int noteValueWidth = DetectNoteValueWidth(lines);

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || !line.StartsWith("#"))
                        continue;

                    // 헤더 라인 파싱 (공백 포함)
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
                                    // 기본 BPM
                                    if (float.TryParse(value, out float bpm))
                                    {
                                        bpmDict["00"] = bpm;
                                        var freq = 60f / bpm;
                                        dataList.Add(new BpmData { Tick = 0f, Freq = freq });
                                    }
                                }
                                else if (key.Length > 3)
                                {
                                    // #BPMXX 형식
                                    var bpmIndex = key.Substring(3);
                                    if (float.TryParse(value, out float bpm))
                                    {
                                        bpmDict[bpmIndex] = bpm;
                                    }
                                }
                            }
                        }
                    }
                    // 노트 데이터 라인 파싱 (콜론 포함)
                    else if (line.Contains(':'))
                    {
                        var colonIndex = line.IndexOf(':');
                        if (colonIndex < 1) continue;

                        var channel = line.Substring(1, colonIndex - 1);
                        var data = line.Substring(colonIndex + 1);

                        ParseNoteData(channel, data, notes, dataList, noteValueWidth);
                    }
                }

                // 노트 타입별 통계 수집
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
                statistics.TotalNotes = notes.Count;

                // 홀드 노트 길이 계산 (끝노트 누락 정보 수집)
                CalculateHoldNoteLengths(notes, statistics);

                // HoldEnd와 Close 노트 제거
                notes.RemoveAll(n => n.NoteType == NoteType.HoldEnd || n.NoteType == NoteType.Close);
                statistics.TotalNotes = notes.Count; // 제거 후 실제 노트 개수
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Error($"[BmsParser] 파싱 오류: {ex.Message}");
                return null;
            }

            var parseResult = new ParseResult
            {
                Notes = notes,
                Statistics = statistics
            };

            lock (s_parseStatsFileCacheLock)
            {
                s_parseStatsFileCache[fullPath] = (versionTicks, parseResult);
            }

            return parseResult;
        }

        private static int DetectNoteValueWidth(IEnumerable<string> lines)
        {
            foreach (var rawLine in lines)
            {
                var line = rawLine?.Trim();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase))
                    continue;

                var keyEnd = line.IndexOfAny(new[] { ' ', '\t' });
                var key = keyEnd >= 0 ? line.Substring(1, keyEnd - 1) : line.Substring(1);
                if (key.Length == 6)
                    return ExtendedNoteValueWidth;
            }

            return DefaultNoteValueWidth;
        }

    

        // ==========================================
        // Merged from separate partial files
        // ==========================================

        private static void ParseNoteData(string channel, string data, List<ParsedNote> notes, List<BpmData> dataList, int noteValueWidth)
        {
            try
            {
                ParseNoteChannelHeader(channel, out int measure, out string channelNum);

                if (!LaneMapping.ContainsKey(channelNum) && channelNum != "04" && channelNum != "05")
                    return;

                var objLength = data.Length / noteValueWidth;
                if (objLength == 0)
                    return;

                AppendParsedNotesFromChannelData(data, measure, channelNum, objLength, notes, dataList, noteValueWidth);
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

        private static void AppendParsedNotesFromChannelData(string data, int measure, string channelNum, int objLength, List<ParsedNote> notes, List<BpmData> dataList, int noteValueWidth)
        {
            for (int i = 0; i < data.Length; i += noteValueWidth)
            {
                if (i + noteValueWidth > data.Length)
                    break;

                var noteValue = data.Substring(i, noteValueWidth);
                if (IsEmptyNoteValue(noteValue))
                    continue;

                var noteTypeKey = NormalizeNoteTypeKey(noteValue);
                if (!NoteTypeMapping.ContainsKey(noteTypeKey))
                    continue;

                var noteType = NoteTypeMapping[noteTypeKey];
                if (!TryResolveNoteLane(noteType, channelNum, out int lane))
                    continue;

                var tick = (float)measure + ((float)(i / noteValueWidth) / objLength);
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

        private static bool IsEmptyNoteValue(string noteValue)
        {
            for (int i = 0; i < noteValue.Length; i++)
            {
                if (noteValue[i] != '0')
                    return false;
            }

            return true;
        }

        private static string NormalizeNoteTypeKey(string noteValue)
        {
            if (noteValue.Length == ExtendedNoteValueWidth && noteValue[0] == '0')
                return noteValue.Substring(1);

            return noteValue;
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


    
    }
}
