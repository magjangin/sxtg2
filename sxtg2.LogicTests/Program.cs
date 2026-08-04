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
            Assert.True(ParseFlexibleBool(tc, false), $"'{tc}'가 true로 해석되지 않았습니다.");
        }

        foreach (var fc in falseCases)
        {
            Assert.True(!ParseFlexibleBool(fc, true), $"'{fc}'가 false로 해석되지 않았습니다.");
        }

        Assert.True(ParseFlexibleBool(null, true), "null 입력 시 defaultValue(true)가 반환되어야 합니다.");
        Assert.True(!ParseFlexibleBool("", false), "빈 입력 시 defaultValue(false)가 반환되어야 합니다.");
        Assert.True(ParseFlexibleBool("unknown_value", true), "알 수 없는 입력 시 defaultValue(true)가 반환되어야 합니다.");
    }

    private static bool ParseFlexibleBool(string val, bool defaultValue)
    {
        if (string.IsNullOrEmpty(val))
            return defaultValue;

        var s = val.Trim().ToLowerInvariant();

        if (s == "1" || s == "true" || s == "t" || s == "on" || s == "켜짐" || s == "사용" || s == "활성화" || s == "enable" || s == "enabled" || s == "yes" || s == "y" || s == "트루" || s == "참" || s == "켜기")
            return true;

        if (s == "0" || s == "false" || s == "f" || s == "off" || s == "꺼짐" || s == "미사용" || s == "비활성화" || s == "disable" || s == "disabled" || s == "no" || s == "n" || s == "폴스" || s == "거짓" || s == "끄기")
            return false;

        return defaultValue;
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
    private static void InspectInputTypes()
    {
        string asmPath = @"H:\Sixtar Gate STARTRAIL custom mode\Sixtar Gate STARTRAIL_Data\Managed\Assembly-CSharp.dll";
        if (!File.Exists(asmPath)) return;

        var asm = System.Reflection.Assembly.LoadFrom(asmPath);
        Console.WriteLine("\n=== INSPECTING Assembly-CSharp.dll INPUT TYPES ===");

        foreach (var type in asm.GetTypes())
        {
            if (type.FullName == null) continue;
            if (type.FullName.Contains("Input") || type.FullName.Contains("KeyConfig") || type.FullName.Contains("Keyboard") || type.FullName.Contains("Control") || type.FullName.Contains("RG_PS_Judgement"))
            {
                Console.WriteLine($"TYPE: {type.FullName}");
                foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
                {
                    if (m.DeclaringType != type) continue;
                    var paramsInfo = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"   - {m.Name}({paramsInfo}) -> {m.ReturnType.Name}");
                }
            }
        }
    }
    private static void InspectInputCandidates()
    {
        string asmPath = @"H:\Sixtar Gate STARTRAIL custom mode\Sixtar Gate STARTRAIL_Data\Managed\Assembly-CSharp.dll";
        if (!File.Exists(asmPath)) return;

        var asm = System.Reflection.Assembly.LoadFrom(asmPath);
        Console.WriteLine("\n=== CANDIDATE INPUT TYPES & METHOD BODY CHECK ===");

        string[] typeNames = {
            "RhythmGame.Play.RG_Gear",
            "RhythmGame.Play.RG_Gear_Default",
            "RhythmGame.Play.RG_Gear_Gothic",
            "RhythmGame.Play.RG_Gear_Pianist",
            "RhythmGame.Play.RG_Gear_Sherbet",
            "RhythmGame.Play.RG_Gear_Stellar",
            "RhythmGame.Play.RG_Gear_Voyager",
            "RhythmGame.Play.RG_PS_Judgement"
        };

        foreach (var typeName in typeNames)
        {
            var type = asm.GetType(typeName);
            if (type == null)
            {
                Console.WriteLine($"TYPE NOT FOUND: {typeName}");
                continue;
            }

            Console.WriteLine($"\nTYPE: {type.FullName} (IsAbstract={type.IsAbstract})");
            foreach (var m in type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static))
            {
                if (m.DeclaringType != type) continue;
                var body = m.GetMethodBody();
                Console.WriteLine($"   - {m.Name} (IsAbstract={m.IsAbstract}, HasBody={body != null})");
            }
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
