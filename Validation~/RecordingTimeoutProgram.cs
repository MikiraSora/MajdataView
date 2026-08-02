using MajSimai;

var tests = new (string Name, Action Body)[]
{
    ("slide tail defines the chart end", SlideTailDefinesChartEnd),
    ("hold and touch-hold tails define the chart end", HoldTailsDefineChartEnd),
    ("cutoff adds a ten second grace period", CutoffAddsGracePeriod),
    ("timeout starts exactly at the cutoff boundary", TimeoutBoundary),
    ("unfinished progress includes the reported slide count", ProgressIncludesSlideCount),
    ("runtime wiring uses parsed chart time and bounded FFmpeg exit", RuntimeWiring)
};

var failures = 0;
foreach (var (name, body) in tests)
{
    try
    {
        body();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} validation cases passed");
return failures == 0 ? 0 : 1;

static void SlideTailDefinesChartEnd()
{
    var timings = new[]
    {
        new SimaiTimingPoint(
            12d,
            new[]
            {
                new SimaiNote
                {
                    Type = SimaiNoteType.Slide,
                    SlideStartTime = 12.5d,
                    SlideTime = 4.25d
                }
            },
            string.Empty),
        new SimaiTimingPoint(30d, Array.Empty<SimaiNote>(), string.Empty)
    };

    ExpectClose(16.75f, RecordingTimeoutPolicy.CalculateChartEndTime(timings));
}

static void HoldTailsDefineChartEnd()
{
    var timings = new[]
    {
        new SimaiTimingPoint(
            20d,
            new[]
            {
                new SimaiNote { Type = SimaiNoteType.Hold, HoldTime = 3d },
                new SimaiNote { Type = SimaiNoteType.TouchHold, HoldTime = 5.5d }
            },
            string.Empty)
    };

    ExpectClose(25.5f, RecordingTimeoutPolicy.CalculateChartEndTime(timings));
}

static void CutoffAddsGracePeriod()
{
    ExpectClose(26.75f, RecordingTimeoutPolicy.CalculateCutoffTime(16.75f));
}

static void TimeoutBoundary()
{
    Expect(!RecordingTimeoutPolicy.HasReachedCutoff(26.749f, 26.75f), "stopped before the cutoff");
    Expect(RecordingTimeoutPolicy.HasReachedCutoff(26.75f, 26.75f), "did not stop at the cutoff");
    Expect(RecordingTimeoutPolicy.HasReachedCutoff(30f, 26.75f), "did not stop after the cutoff");
    Expect(!RecordingTimeoutPolicy.HasReachedCutoff(30f, 0f), "used an uninitialized cutoff");
}

static void ProgressIncludesSlideCount()
{
    var progress = RecordingTimeoutPolicy.FormatProgress(10, 10, 2, 2, 34, 82, 4, 4, 1, 1, 0, 0);
    Expect(progress.Contains("SLD 34/82", StringComparison.Ordinal), $"unexpected progress: {progress}");
}

static void RuntimeWiring()
{
    var loader = ReadRuntimeSource("JsonDataLoader.cs");
    var recorder = ReadRuntimeSource("ScreenRecorder.cs");
    var httpHandler = ReadRuntimeSource("HttpHandler.cs");

    ExpectContains(loader, "ChartEndTime = RecordingTimeoutPolicy.CalculateChartEndTime(loadedData.timingList);");
    ExpectContains(recorder, "RecordingTimeoutPolicy.HasReachedCutoff(timeProvider.AudioTime, CutoffTime)");
    ExpectOrdered(recorder, "counter.AllFinished && APObj == null", "RecordingTimeoutPolicy.HasReachedCutoff");
    ExpectContains(recorder, "yield return WaitForFfmpegExit(p);");
    ExpectContains(recorder, "process.Kill();");
    Expect(!recorder.Contains("p.WaitForExit();", StringComparison.Ordinal), "unbounded FFmpeg wait still exists");
    Expect(!httpHandler.Contains("getChartLength()", StringComparison.Ordinal), "recording still reads incomplete scene objects");
}

static string ReadRuntimeSource(params string[] relativeParts)
{
    var parts = new[] { FindRepositoryRoot(), "Assets", "Scripts" }
        .Concat(relativeParts)
        .ToArray();
    return File.ReadAllText(Path.Combine(parts));
}

static string FindRepositoryRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Assets", "Scripts", "JsonDataLoader.cs")))
                return directory.FullName;
            directory = directory.Parent;
        }
    }

    throw new DirectoryNotFoundException("Cannot locate the MajdataView repository root");
}

static void ExpectContains(string source, string expected)
{
    Expect(source.Contains(expected, StringComparison.Ordinal), $"missing source fragment: {expected}");
}

static void ExpectOrdered(string source, string first, string second)
{
    var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
    var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
    Expect(firstIndex >= 0, $"missing source fragment: {first}");
    Expect(secondIndex >= 0, $"missing source fragment: {second}");
    Expect(firstIndex < secondIndex, $"'{first}' must occur before '{second}'");
}

static void ExpectClose(float expected, float actual)
{
    Expect(Math.Abs(expected - actual) < 0.0001f, $"expected {expected}, got {actual}");
}

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
