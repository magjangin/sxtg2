using sxtg2.Helpers;
using sxtg2.Loaders;

namespace sxtg2.LogicTests;

internal static class Program
{
    private static int Main()
    {
        var tests = new (string Name, Action Run)[]
        {
            ("ParseBmsFromText_ParsesBasicNotesAndLengths", ParseBmsFromText_ParsesBasicNotesAndLengths),
            ("ParseBmsFromText_UsesThreeCharacterNoteValuesForExtendedWavKeys", ParseBmsFromText_UsesThreeCharacterNoteValuesForExtendedWavKeys),
            ("ParseBmsFromText_UsesBpmRegardlessOfHeaderOrder", ParseBmsFromText_UsesBpmRegardlessOfHeaderOrder),
            ("ParseBmsFromText_PairsOpenAndCloseNotes", ParseBmsFromText_PairsOpenAndCloseNotes),
            ("ParseBmsFileWithStatistics_DetectsMissingAndOrphanEnd", ParseBmsFileWithStatistics_DetectsMissingAndOrphanEnd),
            ("ParseBmsFileWithStatistics_UsesCacheForSamePath", ParseBmsFileWithStatistics_UsesCacheForSamePath),
            ("ParseFlexibleBool_SupportsComprehensiveTrueFalseKeywords", ParseFlexibleBool_SupportsComprehensiveTrueFalseKeywords),
            ("ParseBmsFromText_IgnoresHeaderLinesContainingColons", ParseBmsFromText_IgnoresHeaderLinesContainingColons),
            ("ParseBmsFromText_DoesNotTreatWavCommandAsExtendedKey", ParseBmsFromText_DoesNotTreatWavCommandAsExtendedKey),
            ("ParseBmsFromText_IgnoresInvalidBpmValues", ParseBmsFromText_IgnoresInvalidBpmValues),
            ("ParseBmsFromText_ReportsOpenAndHoldWithoutEnd", ParseBmsFromText_ReportsOpenAndHoldWithoutEnd),
            ("ConfigParsing_StripsInlineCommentsButKeepsHexColors", ConfigParsing_StripsInlineCommentsButKeepsHexColors),
            ("ConfigParsing_ParsesValuesAndKeepsDefaultsForBadInput", ConfigParsing_ParsesValuesAndKeepsDefaultsForBadInput),
        };

        var failed = new List<string>();

        foreach (var test in tests)
        {
            try
            {
                test.Run();
                Console.WriteLine($"[PASS] {test.Name}");
            }
            catch (Exception ex)
            {
                failed.Add($"{test.Name}: {ex.Message}");
                Console.WriteLine($"[FAIL] {test.Name}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"총 {tests.Length}개 테스트, 실패 {failed.Count}개");

        if (failed.Count > 0)
        {
            foreach (var f in failed)
            {
                Console.WriteLine($" - {f}");
            }
            return 1;
        }

        return 0;
    }

    private static void ParseBmsFromText_ParsesBasicNotesAndLengths()
    {
        string bms = """
#BPM 150
#00111:0102
#00211:0003
""";

        var result = BmsParser.ParseBmsFromText(bms, "inline");
        Assert.NotNull(result, "결과가 null입니다.");
        Assert.True(result!.BaseBpm == 150f, $"예상 BPM 150, 실제 {result.BaseBpm}");
        Assert.True(result!.Notes.Count == 2, $"예상 노트 수 2, 실제 {result.Notes.Count}");

        var normal = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Normal);
        var longStart = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Long);

        Assert.NotNull(normal, "Normal 노트가 없습니다.");
        Assert.NotNull(longStart, "Long 시작 노트가 없습니다.");
        Assert.True(Math.Abs(normal!.Time - 1.6f) < 0.0001f, $"예상 첫 노트 시각 1.6, 실제 {normal.Time}");
        Assert.True(longStart!.Length > 0f, "Long 노트 길이가 계산되지 않았습니다.");
    }

    private static void ParseBmsFromText_UsesThreeCharacterNoteValuesForExtendedWavKeys()
    {
        string bms = """
#BPM 150
#WAV001 normal.wav
#WAV00A scratch.wav
#WAV010 accent.wav
#00111:001002
#00211:000003
""";

        var result = BmsParser.ParseBmsFromText(bms, "inline extended wav");
        Assert.NotNull(result, "결과가 null입니다.");
        Assert.True(result!.Notes.Count == 2, $"예상 노트 수 2, 실제 {result.Notes.Count}");

        var normal = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Normal);
        var longStart = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Long);

        Assert.NotNull(normal, "3글자 Normal 노트가 없습니다.");
        Assert.NotNull(longStart, "3글자 Long 시작 노트가 없습니다.");
        Assert.True(normal!.OriginalNoteValue == "001", $"Normal 원본 값 예상 001, 실제 {normal.OriginalNoteValue}");
        Assert.True(longStart!.OriginalNoteValue == "002", $"Long 원본 값 예상 002, 실제 {longStart.OriginalNoteValue}");
        Assert.True(longStart.Length > 0f, "3글자 Long 노트 길이가 계산되지 않았습니다.");
    }

    private static void ParseBmsFromText_UsesBpmRegardlessOfHeaderOrder()
    {
        string bms = """
#00111:0100
#BPM 120
""";

        var result = BmsParser.ParseBmsFromText(bms);
        Assert.NotNull(result, "결과가 null입니다.");
        Assert.True(result!.BaseBpm == 120f, $"예상 BPM 120, 실제 {result.BaseBpm}");
        Assert.True(Math.Abs(result.Notes.Single().Time - 2f) < 0.0001f,
            $"예상 노트 시각 2.0, 실제 {result.Notes.Single().Time}");
    }

    private static void ParseBmsFromText_PairsOpenAndCloseNotes()
    {
        string bms = """
#BPM 150
#00111:0400
#00211:0005
""";

        var result = BmsParser.ParseBmsFromText(bms);
        Assert.NotNull(result, "결과가 null입니다.");
        var open = result!.Notes.SingleOrDefault(
            note => note.NoteType == BmsParser.NoteType.Open);
        Assert.NotNull(open, "Open 노트가 없습니다.");
        Assert.True(open!.Lane == 9, $"예상 Open 레인 9, 실제 {open.Lane}");
        Assert.True(open.Length > 0f, "Open/Close 길이가 계산되지 않았습니다.");
        Assert.True(result.Statistics.CloseNotes == 1,
            $"예상 Close 통계 1, 실제 {result.Statistics.CloseNotes}");
    }

    private static void ParseBmsFileWithStatistics_DetectsMissingAndOrphanEnd()
    {
        string bms = """
#BPM 120
#00111:0200
#00112:0003
""";

        var path = WriteTempBms(bms);
        try
        {
            var result = BmsParser.ParseBmsFileWithStatistics(path);
            Assert.NotNull(result, "파일 파싱 결과가 null입니다.");

            Assert.True(result!.Statistics.MissingEndNotes.Count >= 1, "MissingEndNotes가 비어 있습니다.");
            Assert.True(result.Statistics.OrphanEndNotes.Count >= 1, "OrphanEndNotes가 비어 있습니다.");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void ParseBmsFileWithStatistics_UsesCacheForSamePath()
    {
        string bms = """
#BPM 120
#00111:0100
""";

        var path = WriteTempBms(bms);
        try
        {
            var first = BmsParser.ParseBmsFileWithStatistics(path);
            var second = BmsParser.ParseBmsFileWithStatistics(path);

            Assert.NotNull(first, "첫 파싱 결과가 null입니다.");
            Assert.NotNull(second, "두 번째 파싱 결과가 null입니다.");
            Assert.True(ReferenceEquals(first, second), "같은 파일 경로 캐시 결과가 동일 참조가 아닙니다.");
        }
        finally
        {
            TryDelete(path);
        }
    }

    private static void ParseFlexibleBool_SupportsComprehensiveTrueFalseKeywords()
    {
        var trueCases = new[]
        {
            "true", "TRUE", "True", "트루", "참", "켜기", "켜짐", "활성화", "사용",
            "on", "ON", "1", "enable", "enabled", "y", "yes", "t"
        };

        var falseCases = new[]
        {
            "false", "FALSE", "False", "폴스", "거짓", "비활성화", "끄기", "꺼짐", "미사용",
            "off", "OFF", "0", "disable", "disabled", "n", "no", "f"
        };

        foreach (var tc in trueCases)
        {
            Assert.True(ConfigParsing.ParseFlexibleBool(tc, false), $"'{tc}'가 true로 해석되지 않았습니다.");
        }

        foreach (var fc in falseCases)
        {
            Assert.True(!ConfigParsing.ParseFlexibleBool(fc, true), $"'{fc}'가 false로 해석되지 않았습니다.");
        }

        Assert.True(ConfigParsing.ParseFlexibleBool(null, true), "null 입력 시 defaultValue(true)가 반환되어야 합니다.");
        Assert.True(!ConfigParsing.ParseFlexibleBool("", false), "빈 입력 시 defaultValue(false)가 반환되어야 합니다.");
        Assert.True(ConfigParsing.ParseFlexibleBool("unknown_value", true), "알 수 없는 입력 시 defaultValue(true)가 반환되어야 합니다.");
    }

    private static void ParseBmsFromText_IgnoresHeaderLinesContainingColons()
    {
        // 헤더 값에 콜론이 있으면 마지막 두 글자(11)를 레인 채널로 오인해 가짜 노트가 생기던 문제(알려진 문제 #4).
        string bms = """
#BPM 150
#TITLE Remix 2011:0101
#ARTIST someone : 11
#00111:0100
""";

        var result = BmsParser.ParseBmsFromText(bms);
        Assert.NotNull(result, "결과가 null입니다.");
        Assert.True(result!.Notes.Count == 1, $"헤더 줄이 노트로 읽히면 안 됩니다. 예상 1개, 실제 {result.Notes.Count}개");
        Assert.True(Math.Abs(result.Notes[0].Time - 1.6f) < 0.0001f, $"예상 시각 1.6, 실제 {result.Notes[0].Time}");
    }

    private static void ParseBmsFromText_DoesNotTreatWavCommandAsExtendedKey()
    {
        // `#WAVCMD`처럼 길이만 6인 명령 줄이 3글자 모드를 켜서 노트가 0개가 되던 문제(알려진 문제 #27).
        string bms = """
#BPM 150
#WAVCMD 01 01 x
#00111:0100
""";

        var result = BmsParser.ParseBmsFromText(bms);
        Assert.NotNull(result, "결과가 null입니다.");
        Assert.True(result!.Notes.Count == 1, $"예상 노트 1개, 실제 {result.Notes.Count}개");
    }

    private static void ParseBmsFromText_IgnoresInvalidBpmValues()
    {
        // `#BPM Infinity`가 통과하면 모든 노트가 0초에 몰리던 문제(알려진 문제 #28).
        var onlyInfinity = BmsParser.ParseBmsFromText("#BPM Infinity\n#00111:0100");
        Assert.NotNull(onlyInfinity, "결과가 null입니다.");
        Assert.True(onlyInfinity!.BaseBpm == 150f, $"무효한 BPM이면 기본값 150이어야 합니다. 실제 {onlyInfinity.BaseBpm}");
        Assert.True(Math.Abs(onlyInfinity.Notes.Single().Time - 1.6f) < 0.0001f, $"예상 시각 1.6, 실제 {onlyInfinity.Notes.Single().Time}");

        var fallsThrough = BmsParser.ParseBmsFromText("#BPM Infinity\n#BPM 120\n#00111:0100");
        Assert.True(fallsThrough!.BaseBpm == 120f, $"무효한 값은 건너뛰고 다음 유효한 #BPM(120)을 써야 합니다. 실제 {fallsThrough.BaseBpm}");
    }

    private static void ParseBmsFromText_ReportsOpenAndHoldWithoutEnd()
    {
        // 끝(03/05)이 없는 홀드/오픈 노트는 길이 0으로 남고 MissingEndNotes에 종류별로 기록된다.
        // 주입기는 이 정보로 홀드를 일반 노트로, 오픈 노트를 기본 길이로 바꾼다.
        string bms = """
#BPM 120
#00111:0200
#00104:0400
""";

        var result = BmsParser.ParseBmsFromText(bms);
        Assert.NotNull(result, "결과가 null입니다.");
        var hold = result!.Notes.Single(n => n.NoteType == BmsParser.NoteType.Long);
        var open = result.Notes.Single(n => n.NoteType == BmsParser.NoteType.Open);
        Assert.True(hold.Length == 0f, $"끝 없는 홀드 길이는 0이어야 합니다. 실제 {hold.Length}");
        Assert.True(open.Length == 0f && open.Lane == 9, $"끝 없는 오픈 노트는 레인 9, 길이 0이어야 합니다. 실제 레인 {open.Lane}, 길이 {open.Length}");
        Assert.True(result.Statistics.MissingEndNotes.Any(m => m.NoteType == "Long"), "MissingEndNotes에 홀드가 기록되어야 합니다.");
        Assert.True(result.Statistics.MissingEndNotes.Any(m => m.NoteType == "Open"), "MissingEndNotes에 오픈 노트가 기록되어야 합니다.");
    }

    private static void ConfigParsing_StripsInlineCommentsButKeepsHexColors()
    {
        Assert.True(ConfigParsing.StripInlineComment("1 # 메모") == "1", "줄 끝 # 주석을 떼야 합니다.");
        Assert.True(ConfigParsing.StripInlineComment("Left // 메모") == "Left", "줄 끝 // 주석을 떼야 합니다.");
        Assert.True(ConfigParsing.StripInlineComment("#26BFD9D9") == "#26BFD9D9", "맨 앞의 # 색상 값은 그대로여야 합니다.");
        Assert.True(ConfigParsing.StripInlineComment("#26BFD9D9 # 시안") == "#26BFD9D9", "색상 값 뒤의 주석만 떼야 합니다.");
        Assert.True(ConfigParsing.StripInlineComment("255,128,0") == "255,128,0", "주석이 없으면 그대로여야 합니다.");
        Assert.True(ConfigParsing.StripInlineComment("a#b") == "a#b", "공백 없이 붙은 #는 주석이 아닙니다.");
        Assert.True(ConfigParsing.ParseBoolSetting("AutoPlay", ConfigParsing.StripInlineComment("1 # 메모"), false), "`AutoPlay=1 # 메모`가 켜짐으로 읽혀야 합니다.");
    }

    private static void ConfigParsing_ParsesValuesAndKeepsDefaultsForBadInput()
    {
        Assert.True(ConfigParsing.ParseBoolSetting("K", "켜짐", false), "켜짐은 true여야 합니다.");
        Assert.True(!ConfigParsing.ParseBoolSetting("K", "꺼짐", true), "꺼짐은 false여야 합니다.");
        Assert.True(ConfigParsing.ParseBoolSetting("K", "몰라요", true), "모르는 단어는 기본값(true)을 유지해야 합니다.");
        Assert.True(ConfigParsing.ParseFloatSetting("K", "2.5", 1f, 0f, 10f) == 2.5f, "범위 안의 숫자는 그대로 읽어야 합니다.");
        Assert.True(ConfigParsing.ParseFloatSetting("K", "99", 1f, 0f, 10f) == 1f, "범위를 벗어나면 기본값을 유지해야 합니다.");
        Assert.True(ConfigParsing.ParseFloatSetting("K", "abc", 1f, 0f, 10f) == 1f, "숫자가 아니면 기본값을 유지해야 합니다.");
        Assert.True(ConfigParsing.ParseSideSetting("K", "왼쪽", "Center") == "Left", "왼쪽은 Left여야 합니다.");
        Assert.True(ConfigParsing.ParseSideSetting("K", "???", "Center") == "Center", "모르는 값은 기본값을 유지해야 합니다.");
        Assert.True(ConfigParsing.ParseShapeSetting("K", "캡슐", 0) == 1, "캡슐은 1이어야 합니다.");
        Assert.True(ConfigParsing.ParseShapeSetting("K", "삼각", 0) == 2, "삼각은 2여야 합니다.");
        Assert.True(ConfigParsing.ParseShapeSetting("K", "추종", 0) == -1, "추종은 -1이어야 합니다.");
        Assert.True(ConfigParsing.ParseShapeSetting("K", "9", 1) == 1, "범위 밖 숫자는 기본값을 유지해야 합니다.");
    }

    private static string WriteTempBms(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"sxtg2_logic_{Guid.NewGuid():N}.bms");
        File.WriteAllText(path, content);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LogicTests] 임시 BMS 삭제 건너뜀: {ex.Message}");
        }
    }
}

internal static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public static void NotNull(object value, string message)
    {
        if (value is null)
        {
            throw new InvalidOperationException(message);
        }
    }
}
