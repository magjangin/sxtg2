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
                var timeline = BmsTimeline.Build(lines, bpm);
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
                        timeline,
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
            BmsTimeline timeline,
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

                notes.Add(new ParsedNote
                {
                    Time = timeline.TimeAt(measure, (double)index / objectCount),
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
            /// <summary>
        /// 마디·마디 안 위치를 초로 바꾼다. 마디 길이(#mmm02), 변속(#mmm03: 16진 정수 BPM, #mmm08: #BPMxx 정의 참조)을 반영한다.
        /// 변속·변박이 없는 차트는 예전 공식(마디 × 240 / BPM)과 똑같은 값이 나온다. STOP(#mmm09)은 아직 지원하지 않는다.
        /// 변속·변박 데이터는 노트 값 폭(2/3자리)과 상관없이 표준 BMS처럼 두 글자 단위로 읽는다.
        /// </summary>
        internal sealed class BmsTimeline
        {
            private readonly Dictionary<int, double> _measureLengths;
            private readonly List<KeyValuePair<double, double>> _bpmChanges; // (박자 위치, BPM), 위치순
            private readonly double _baseBpm;
            private readonly int _maxMeasure;
            private readonly double[] _measureStartBeats;

            private BmsTimeline(double baseBpm, Dictionary<int, double> measureLengths, List<KeyValuePair<double, double>> bpmChanges, int maxMeasure)
            {
                _baseBpm = baseBpm;
                _measureLengths = measureLengths;
                _maxMeasure = maxMeasure;
                _measureStartBeats = new double[maxMeasure + 2];
                for (int m = 1; m < _measureStartBeats.Length; m++)
                    _measureStartBeats[m] = _measureStartBeats[m - 1] + 4.0 * LengthOf(m - 1);
                _bpmChanges = bpmChanges;
            }

            public static BmsTimeline Build(string[] lines, float baseBpm)
            {
                var bpmDefinitions = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                var lengths = new Dictionary<int, double>();
                var rawChanges = new List<KeyValuePair<KeyValuePair<int, double>, double>>(); // ((마디, 위치), BPM)
                int maxMeasure = 0;

                foreach (string rawLine in lines)
                {
                    string line = rawLine?.Trim();
                    if (string.IsNullOrEmpty(line) || line[0] != '#')
                        continue;

                    // #BPMxx 값 (확장 BPM 정의). "#BPM 150"(기본 BPM)은 공백이 있어 여기 걸리지 않는다.
                    if (line.Length > 6 && line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase) && line[4] != ' ' && line[4] != '\t')
                    {
                        string[] split = line.Substring(4).Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (split.Length == 2 &&
                            double.TryParse(split[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double defined) &&
                            defined > 0)
                        {
                            bpmDefinitions[split[0]] = defined;
                        }
                        continue;
                    }

                    int colon = line.IndexOf(':');
                    if (colon != 6)
                        continue;

                    if (!int.TryParse(line.Substring(1, 3), NumberStyles.None, CultureInfo.InvariantCulture, out int measure))
                        continue;
                    string channel = line.Substring(4, 2);
                    string data = line.Substring(colon + 1).Trim();
                    if (measure > maxMeasure)
                        maxMeasure = measure;

                    if (channel == "02")
                    {
                        if (double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out double length) && length > 0)
                            lengths[measure] = length;
                    }
                    else if (channel == "03" || channel == "08")
                    {
                        int count = data.Length / 2;
                        for (int i = 0; i < count; i++)
                        {
                            string value = data.Substring(i * 2, 2);
                            if (value == "00")
                                continue;

                            double bpm;
                            if (channel == "03")
                            {
                                if (!int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hexBpm) || hexBpm <= 0)
                                    continue;
                                bpm = hexBpm;
                            }
                            else if (!bpmDefinitions.TryGetValue(value, out bpm))
                            {
                                continue;
                            }

                            rawChanges.Add(new KeyValuePair<KeyValuePair<int, double>, double>(
                                new KeyValuePair<int, double>(measure, (double)i / count), bpm));
                        }
                    }
                }

                var timeline = new BmsTimeline(baseBpm > 0 ? baseBpm : DefaultBpm, lengths, new List<KeyValuePair<double, double>>(), maxMeasure);
                foreach (var change in rawChanges)
                {
                    double beat = timeline.BeatAt(change.Key.Key, change.Key.Value);
                    timeline._bpmChanges.Add(new KeyValuePair<double, double>(beat, change.Value));
                }
                timeline._bpmChanges.Sort((left, right) => left.Key.CompareTo(right.Key));
                return timeline;
            }

            private double LengthOf(int measure)
            {
                return _measureLengths.TryGetValue(measure, out double length) ? length : 1.0;
            }

            private double BeatAt(int measure, double fraction)
            {
                if (measure < 0)
                    measure = 0;

                double start;
                if (measure < _measureStartBeats.Length)
                {
                    start = _measureStartBeats[measure];
                }
                else
                {
                    // 마디 표보다 뒤(노트만 있는 마디)는 기본 길이로 이어 붙인다.
                    int last = _measureStartBeats.Length - 1;
                    start = _measureStartBeats[last] + 4.0 * (measure - last);
                }

                return start + 4.0 * LengthOf(measure) * fraction;
            }

            public float TimeAt(int measure, double fraction)
            {
                double targetBeat = BeatAt(measure, fraction);
                double seconds = 0;
                double beat = 0;
                double bpm = _baseBpm;

                foreach (var change in _bpmChanges)
                {
                    if (change.Key > targetBeat)
                        break;

                    seconds += (change.Key - beat) * 60.0 / bpm;
                    beat = change.Key;
                    bpm = change.Value;
                }

                seconds += (targetBeat - beat) * 60.0 / bpm;
                return (float)seconds;
            }
        }
    }
}
