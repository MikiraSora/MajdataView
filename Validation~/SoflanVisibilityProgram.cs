using MajSimai;
using OngekiFumenEditor.Core.Base;
using OngekiFumenEditor.Core.Base.Collections;
using OngekiFumenEditor.Core.Base.EditorObjects;
using OngekiFumenEditor.Core.Base.OngekiObjects;
using OngekiFumenEditor.Core.Modules.FumenVisualEditor;

var tests = new (string Name, Action Body)[]
{
    ("Thunderstorm Road group 4 is blocked at 1:01.6127", ThunderstormRoadBlockedTime),
    ("Thunderstorm Road group 4 becomes visible at the game boundary", ThunderstormRoadVisibleTime),
    ("MaiBug is applied before Soflan integration", MaiBugAppliedBeforeSoflanIntegration),
    ("ordinary positive speed keeps the standard visible window", OrdinaryPositiveSpeed),
    ("runtime visibility wiring covers Tap Star and EachLine", RuntimeVisibilityWiring)
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

static void ThunderstormRoadBlockedTime()
{
    var fixture = CreateThunderstormRoadFixture();
    const float blockedLeadMsec = 687.72284f;
    var currentMsec = fixture.NoteMsec - blockedLeadMsec;

    Expect(
        !IsVisible(fixture, currentMsec, fixture.NoteMsec, 900f),
        "group 4 note unexpectedly entered a visible source-time range");
}

static void ThunderstormRoadVisibleTime()
{
    var fixture = CreateThunderstormRoadFixture();
    const float firstVisibleSampleLeadMsec = 581.60284f;
    var currentMsec = fixture.NoteMsec - firstVisibleSampleLeadMsec;

    Expect(
        IsVisible(fixture, currentMsec, fixture.NoteMsec, 900f),
        "group 4 note was still blocked at the first game-visible sample");
}

static void MaiBugAppliedBeforeSoflanIntegration()
{
    var fixture = CreateThunderstormRoadFixture();
    const float firstVisibleSampleLeadMsec = 581.60284f;
    var currentMsec = fixture.NoteMsec - firstVisibleSampleLeadMsec;
    var visibleMsec = 2f * GetDefaultMsec(900f);
    var maiBugMsec = GetMaiBugAdjustMsec(900f);
    var adjustedCurrentY = ConvertToY(fixture, currentMsec + maiBugMsec);
    var unadjustedCurrentY = ConvertToY(fixture, currentMsec);

    Expect(Math.Abs(maiBugMsec - -11.111111f) < 0.0002f, $"unexpected MaiBug value {maiBugMsec}");
    Expect(adjustedCurrentY != unadjustedCurrentY, "fixture did not exercise the pre-integration offset");
    Expect(
        ContainsNote(fixture, adjustedCurrentY, visibleMsec, fixture.NoteMsec),
        "adjusted current Y should make the note visible");
}

static void OrdinaryPositiveSpeed()
{
    const float noteMsec = 2000f;
    var bpmList = new BpmList { FirstBpm = 120f };
    var fixture = new VisibilityFixture(bpmList, new SoflanList(), noteMsec);

    Expect(IsVisible(fixture, noteMsec - 500f, noteMsec, 900f), "near positive-speed note should be visible");
    Expect(!IsVisible(fixture, noteMsec - 600f, noteMsec, 900f), "distant positive-speed note should be blocked");
}

static void RuntimeVisibilityWiring()
{
    var manager = ReadRuntimeSource("Misc", "SoflanManager.cs");
    var noteDrop = ReadRuntimeSource("Notes", "NoteDrop.cs");
    var tap = ReadRuntimeSource("Notes", "TapBase.cs");
    var star = ReadRuntimeSource("Notes", "StarDrop.cs");
    var eachLine = ReadRuntimeSource("Notes", "EachLineDrop.cs");

    ExpectContains(manager, "FillVisibleMsecRangesForGamePreview(");
    ExpectContains(manager, "cache.Ranges[i].Contain(noteAudioMsec)");
    ExpectContains(noteDrop, "public bool isSoflanVisible { get; private set; } = true;");
    ExpectOrdered(tap, "if (!UpdateTapSoflanVisibility())", "var timing = getSoflanTimingDisplay = GetVisualSoflanTiming();");
    ExpectOrdered(star, "if (!UpdateTapSoflanVisibility())", "if (TryActivateSlide(shouldActivateSlide))");
    ExpectContains(eachLine, "SoflanManager.Instance.IsNoteVisible(");
    ExpectContains(eachLine, "sr.forceRenderingOff = true;");
}

static VisibilityFixture CreateThunderstormRoadFixture()
{
    const string chartText = """
        (215)
        {8}
        <HS4*1.5>,,,,,,,,,,,,,,,,
        <HS4*-999.0[4:1]~1.0[#0]~-3.0[4:1]~0.0[4:2]~1.5[8:2]>(8/3m/4m/6),
        """;
    var chart = SimaiParser.ParseChart(chartText.AsSpan(), 0, out _);
    var runtime = BuildRuntime(chart.NoteTimings.ToArray());
    var noteTiming = chart.NoteTimings.ToArray().Single(point => !point.IsEmpty);

    Expect(noteTiming.SoflanGroup == 4, $"fixture note group was {noteTiming.SoflanGroup}");
    Expect(noteTiming.Notes.Select(note => note.StartPosition).SequenceEqual(new[] { 8, 3, 4, 6 }), "fixture lanes changed");
    return new VisibilityFixture(
        runtime.BpmList,
        runtime.SoflanLists[4],
        (float)(noteTiming.Timing * 1000d));
}

static RuntimeSoflanData BuildRuntime(IEnumerable<SimaiTimingPoint> timingPoints)
{
    var bpmList = new BpmList();
    var soflanLists = new SoflanListMap();
    var lastHSpeedByGroup = new Dictionary<int, float>();
    var lastBpm = float.NaN;

    foreach (var timingPoint in timingPoints)
    {
        if (timingPoint.Bpm != lastBpm)
        {
            if (float.IsNaN(lastBpm))
            {
                bpmList.FirstBpm = timingPoint.Bpm;
            }
            else
            {
                bpmList.Add(new BPMChange
                {
                    TGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                        TimeSpan.FromSeconds(timingPoint.Timing),
                        bpmList),
                    BPM = timingPoint.Bpm
                });
            }

            lastBpm = timingPoint.Bpm;
        }

        var lastHSpeed = lastHSpeedByGroup.TryGetValue(timingPoint.SoflanGroup, out var speed)
            ? speed
            : 1f;
        if (timingPoint.HSpeed == lastHSpeed)
            continue;

        soflanLists[timingPoint.SoflanGroup].Add(new KeyframeSoflan
        {
            TGrid = TGridCalculator.ConvertAudioTimeToTGrid(
                TimeSpan.FromSeconds(timingPoint.Timing),
                bpmList),
            Speed = timingPoint.HSpeed,
            SoflanGroup = timingPoint.SoflanGroup
        });
        lastHSpeedByGroup[timingPoint.SoflanGroup] = timingPoint.HSpeed;
    }

    return new RuntimeSoflanData(bpmList, soflanLists);
}

static bool IsVisible(VisibilityFixture fixture, float currentMsec, float noteMsec, float speedValue)
{
    var visibleMsec = 2f * GetDefaultMsec(speedValue);
    var adjustedCurrentMsec = Math.Max(0f, currentMsec + GetMaiBugAdjustMsec(speedValue));
    return ContainsNote(fixture, ConvertToY(fixture, adjustedCurrentMsec), visibleMsec, noteMsec);
}

static bool ContainsNote(VisibilityFixture fixture, double currentY, float visibleMsec, float noteMsec)
{
    var ranges = new List<SoflanList.VisibleMsecRange>();
    var scratch = new SoflanList.VisibleRangeQueryScratch();
    fixture.SoflanList.FillVisibleMsecRangesForGamePreview(
        currentY,
        visibleMsec,
        fixture.BpmList,
        ranges,
        scratch);
    return ranges.Any(range => range.Contain(noteMsec));
}

static double ConvertToY(VisibilityFixture fixture, float audioMsec)
{
    return TGridCalculator.ConvertAudioTimeToY_PreviewMode(
        TimeSpan.FromMilliseconds(audioMsec),
        fixture.SoflanList,
        fixture.BpmList,
        1d);
}

static float GetDefaultMsec(float speedValue)
{
    return 240000f / speedValue;
}

static float GetMaiBugAdjustMsec(float speedValue)
{
    var speedRatio = speedValue / 150f;
    return (speedRatio - 1f) * (-0.5f / speedRatio) * 1.6f * 1000f / 60f;
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

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record RuntimeSoflanData(BpmList BpmList, SoflanListMap SoflanLists);
internal sealed record VisibilityFixture(BpmList BpmList, SoflanList SoflanList, float NoteMsec);
