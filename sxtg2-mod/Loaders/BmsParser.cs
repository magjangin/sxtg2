using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MelonLoader;

namespace sxtg2.Loaders
{
    public static class BmsParser
    {
        public enum NoteType
        {
            Normal,
            Long,
            HoldEnd,
            Open,
            Close
        }

        public class ParsedNote
        {
            public float Time { get; set; }
            public int Lane { get; set; }
            public NoteType NoteType { get; set; }
            public float Length { get; set; }
            public string OriginalNoteValue { get; set; }
        }

        public class ParseResult
        {
            public float BaseBpm { get; set; }
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
            public List<MissingEndNoteInfo> MissingEndNotes { get; } =
                new List<MissingEndNoteInfo>();
            public List<MissingEndNoteInfo> OrphanEndNotes { get; } =
                new List<MissingEndNoteInfo>();
        }

        public class MissingEndNoteInfo
        {
            public int Lane { get; set; }
            public float Time { get; set; }
            public string NoteType { get; set; }
        }

        private static readonly Dictionary<string, int> LaneMapping =
            new Dictionary<string, int>
            {
                { "16", 0 },
                { "11", 1 },
                { "12", 2 },
                { "13", 3 },
                { "14", 4 },
                { "15", 5 },
                { "18", 6 }
            };

        private static readonly Dictionary<string, NoteType> NoteTypeMapping =
            new Dictionary<string, NoteType>
            {
                { "01", NoteType.Normal },
                { "02", NoteType.Long },
                { "03", NoteType.HoldEnd },
                { "04", NoteType.Open },
                { "05", NoteType.Close }
            };

        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, (long Version, ParseResult Result)> FileCache =
            new Dictionary<string, (long, ParseResult)>(StringComparer.OrdinalIgnoreCase);

        private const int DefaultNoteValueWidth = 2;
        private const int ExtendedNoteValueWidth = 3;
        private const float DefaultBpm = 150f;

        public static List<ParsedNote> ParseBmsFile(string filePath)
        {
            return ParseBmsFileWithStatistics(filePath)?.Notes ?? new List<ParsedNote>();
        }

        public static ParseResult ParseBmsFileWithStatistics(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return null;

            string fullPath = Path.GetFullPath(filePath);
            if (!File.Exists(fullPath))
                return null;

            long version = File.GetLastWriteTimeUtc(fullPath).Ticks;
            lock (CacheLock)
            {
                if (FileCache.TryGetValue(fullPath, out var cached) &&
                    cached.Version == version)
                {
                    return cached.Result;
                }
            }

            var result = ParseBmsFromLines(File.ReadAllLines(fullPath));
            if (result != null)
            {
                lock (CacheLock)
                {
                    FileCache[fullPath] = (version, result);
                }
            }

            return result;
        }

        public static ParseResult ParseBmsFromText(string text, string sourceName = "inline")
        {
            if (string.IsNullOrEmpty(text))
                return null;

            return ParseBmsFromLines(
                text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));
        }

        public static ParseResult ParseBmsFromLines(string[] lines)
        {
            if (lines == null)
                return null;

            try
            {
                float bpm = FindBaseBpm(lines);
                int valueWidth = DetectNoteValueWidth(lines);
                var notes = new List<ParsedNote>();

                foreach (string rawLine in lines)
                {
                    string line = rawLine?.Trim();
                    if (string.IsNullOrEmpty(line) || line[0] != '#')
                        continue;

                    int colon = line.IndexOf(':');
                    if (colon <= 1)
                        continue;

                    ParseNoteData(
                        line.Substring(1, colon - 1),
                        line.Substring(colon + 1),
                        valueWidth,
                        bpm,
                        notes);
                }

                var statistics = BuildStatistics(notes);
                PairHoldNotes(notes, statistics);
                notes.RemoveAll(note =>
                    note.NoteType == NoteType.HoldEnd ||
                    note.NoteType == NoteType.Close);
                statistics.TotalNotes = notes.Count;

                return new ParseResult
                {
                    BaseBpm = bpm,
                    Notes = notes,
                    Statistics = statistics
                };
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BmsParser] 파싱 오류: {ex.Message}");
                return null;
            }
        }

        private static float FindBaseBpm(IEnumerable<string> lines)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrEmpty(line) ||
                    !line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string[] split = line.Substring(1).Split(new[] { ' ', '\t' }, 2);
                if (split.Length == 2 &&
                    split[0].Equals("BPM", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(
                        split[1].Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out float bpm) &&
                    bpm > 0f)
                {
                    return bpm;
                }
            }

            return DefaultBpm;
        }

        private static int DetectNoteValueWidth(IEnumerable<string> lines)
        {
            foreach (string rawLine in lines)
            {
                string line = rawLine?.Trim();
                if (string.IsNullOrEmpty(line) ||
                    !line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int keyEnd = line.IndexOfAny(new[] { ' ', '\t' });
                string key = keyEnd >= 0
                    ? line.Substring(1, keyEnd - 1)
                    : line.Substring(1);
                if (key.Length == 6)
                    return ExtendedNoteValueWidth;
            }

            return DefaultNoteValueWidth;
        }

        private static void ParseNoteData(
            string channel,
            string data,
            int valueWidth,
            float bpm,
            List<ParsedNote> notes)
        {
            if (channel.Length < 2 || data.Length < valueWidth)
                return;

            string channelNumber = channel.Substring(channel.Length - 2);
            if (!LaneMapping.ContainsKey(channelNumber) &&
                channelNumber != "04" &&
                channelNumber != "05")
            {
                return;
            }

            int measure = 0;
            if (channel.Length > 2)
            {
                int.TryParse(
                    channel.Substring(0, channel.Length - 2),
                    out measure);
            }

            int objectCount = data.Length / valueWidth;
            for (int index = 0; index < objectCount; index++)
            {
                string value = data.Substring(index * valueWidth, valueWidth);
                if (value.All(character => character == '0'))
                    continue;

                string typeKey = valueWidth == ExtendedNoteValueWidth && value[0] == '0'
                    ? value.Substring(1)
                    : value;
                if (!NoteTypeMapping.TryGetValue(typeKey, out var noteType) ||
                    !TryResolveLane(noteType, channelNumber, out int lane))
                {
                    continue;
                }

                float measurePosition = measure + (float)index / objectCount;
                notes.Add(new ParsedNote
                {
                    Time = measurePosition * 240f / bpm,
                    Lane = lane,
                    NoteType = noteType,
                    OriginalNoteValue = value
                });
            }
        }

        private static bool TryResolveLane(
            NoteType noteType,
            string channel,
            out int lane)
        {
            if (noteType == NoteType.Open || noteType == NoteType.Close)
            {
                lane = 9;
                return true;
            }

            return LaneMapping.TryGetValue(channel, out lane);
        }

        private static ParseStatistics BuildStatistics(IEnumerable<ParsedNote> notes)
        {
            var statistics = new ParseStatistics();
            foreach (var note in notes)
            {
                switch (note.NoteType)
                {
                    case NoteType.Normal: statistics.NormalNotes++; break;
                    case NoteType.Long: statistics.LongNotes++; break;
                    case NoteType.HoldEnd: statistics.HoldEndNotes++; break;
                    case NoteType.Open: statistics.OpenNotes++; break;
                    case NoteType.Close: statistics.CloseNotes++; break;
                }
            }

            return statistics;
        }

        private static void PairHoldNotes(
            IEnumerable<ParsedNote> notes,
            ParseStatistics statistics)
        {
            foreach (var laneGroup in notes
                .GroupBy(note => note.Lane)
                .Select(group => new
                {
                    Lane = group.Key,
                    Notes = group.OrderBy(note => note.Time)
                }))
            {
                NoteType startType = laneGroup.Lane == 9
                    ? NoteType.Open
                    : NoteType.Long;
                NoteType endType = laneGroup.Lane == 9
                    ? NoteType.Close
                    : NoteType.HoldEnd;
                ParsedNote pending = null;

                foreach (var note in laneGroup.Notes)
                {
                    if (note.NoteType == startType)
                    {
                        if (pending != null)
                            AddUnpaired(statistics.MissingEndNotes, laneGroup.Lane, pending);
                        pending = note;
                    }
                    else if (note.NoteType == endType)
                    {
                        if (pending == null)
                        {
                            AddUnpaired(statistics.OrphanEndNotes, laneGroup.Lane, note);
                        }
                        else
                        {
                            pending.Length = note.Time - pending.Time;
                            pending = null;
                        }
                    }
                }

                if (pending != null)
                    AddUnpaired(statistics.MissingEndNotes, laneGroup.Lane, pending);
            }
        }

        private static void AddUnpaired(
            ICollection<MissingEndNoteInfo> target,
            int lane,
            ParsedNote note)
        {
            target.Add(new MissingEndNoteInfo
            {
                Lane = lane,
                Time = note.Time,
                NoteType = note.NoteType.ToString()
            });
        }
    }
}
