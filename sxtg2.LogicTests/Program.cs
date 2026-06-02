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
            ("ParseBmsFileWithStatistics_DetectsMissingAndOrphanEnd", ParseBmsFileWithStatistics_DetectsMissingAndOrphanEnd),
            ("ParseBmsFileWithStatistics_UsesCacheForSamePath", ParseBmsFileWithStatistics_UsesCacheForSamePath),
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
        Assert.True(result!.Notes.Count == 2, $"예상 노트 수 2, 실제 {result.Notes.Count}");

        var normal = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Normal);
        var longStart = result.Notes.SingleOrDefault(n => n.NoteType == BmsParser.NoteType.Long);

        Assert.NotNull(normal, "Normal 노트가 없습니다.");
        Assert.NotNull(longStart, "Long 시작 노트가 없습니다.");
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
