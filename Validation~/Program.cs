using System;
using System.IO;

var tests = new (string Name, Action Test)[]
{
    ("each visual combines natural and forced yellow", TestEachVisual),
    ("slide segment counting", TestSegmentCounting),
    ("head yellow propagates to connected moving stars", TestConnectedHead),
    ("no-head slide keeps a yellow moving star", TestNoHeadMovingStar),
    ("same-head branches do not share head yellow", TestSameHeadBranchIndependence),
    ("path indices stay independent from moving stars", TestPathOnly),
    ("legacy null indices are empty", TestLegacyNull),
    ("loader validates Force Yellow before instantiation", TestLoaderPreflightOrder),
    ("ordinary managed flags reach runtime components", TestOrdinaryRuntimeWiring),
    ("slide and wifi states reach runtime components", TestSlideRuntimeWiring),
    ("runtime sprite consumers combine natural each and Force Yellow", TestSpriteRuntimeWiring),
    ("negative index is rejected", () => ExpectInvalid(() => ForceYellowAppearance.ResolveSlide(false, 1, new[] { -1 }, "1-3[8:1]"))),
    ("out of range index is rejected", () => ExpectInvalid(() => ForceYellowAppearance.ResolveSlide(false, 2, new[] { 2 }, "1-3[8:1]-5[8:1]"))),
    ("duplicate index is rejected", () => ExpectInvalid(() => ForceYellowAppearance.ResolveSlide(false, 2, new[] { 1, 1 }, "1-3[8:1]-5[8:1]"))),
    ("unordered index is rejected", () => ExpectInvalid(() => ForceYellowAppearance.ResolveSlide(false, 2, new[] { 1, 0 }, "1-3[8:1]-5[8:1]"))),
    ("non-slide index is rejected", () => ExpectInvalid(() => ForceYellowAppearance.ValidateNonSlideIndices(new[] { 0 }, "1y")))
};

var failures = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception e)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: {e.Message}");
    }
}

Console.WriteLine($"{tests.Length - failures}/{tests.Length} validation cases passed");
return failures == 0 ? 0 : 1;

static void TestEachVisual()
{
    Assert(!ForceYellowAppearance.UsesEachVisual(false, false));
    Assert(ForceYellowAppearance.UsesEachVisual(true, false));
    Assert(ForceYellowAppearance.UsesEachVisual(false, true));
    Assert(ForceYellowAppearance.UsesEachVisual(true, true));
}

static void TestSegmentCounting()
{
    var singleSegments = new[]
    {
        "1-3[8:1]",
        "1^3[8:1]",
        "1v3[8:1]",
        "1<3[8:1]",
        "1>3[8:1]",
        "1V35[8:1]",
        "1p3[8:1]",
        "1q3[8:1]",
        "1pp3[8:1]",
        "1qq3[8:1]",
        "1s3[8:1]",
        "1z3[8:1]",
        "1w5[8:1]"
    };

    foreach (var rawContent in singleSegments)
        AssertEqual(1, ForceYellowAppearance.CountSlideSegments(rawContent));

    AssertEqual(2, ForceYellowAppearance.CountSlideSegments("1-3[8:1]-5[8:1]"));
    AssertEqual(3, ForceYellowAppearance.CountSlideSegments("1p3[8:1]q5[8:1]V71[8:1]"));
}

static void TestConnectedHead()
{
    var result = ForceYellowAppearance.ResolveSlide(true, 2, Array.Empty<int>(), "1-3[8:1]-5[8:1]");
    Assert(result.MovingStarsAreForceYellow);
    Assert(!result.PathSegmentsAreForceYellow[0]);
    Assert(!result.PathSegmentsAreForceYellow[1]);
}

static void TestNoHeadMovingStar()
{
    var result = ForceYellowAppearance.ResolveSlide(true, 1, Array.Empty<int>(), "1-3[8:1]");
    Assert(result.MovingStarsAreForceYellow);
    Assert(!result.PathSegmentsAreForceYellow[0]);
}

static void TestSameHeadBranchIndependence()
{
    var yellowBranch = ForceYellowAppearance.ResolveSlide(true, 1, Array.Empty<int>(), "1-3[8:1]");
    var independentBranch = ForceYellowAppearance.ResolveSlide(false, 1, Array.Empty<int>(), "1-5[8:1]");

    Assert(yellowBranch.MovingStarsAreForceYellow);
    Assert(!independentBranch.MovingStarsAreForceYellow);
}

static void TestPathOnly()
{
    var result = ForceYellowAppearance.ResolveSlide(false, 2, new[] { 1 }, "1-3[8:1]-5[8:1]");
    Assert(!result.MovingStarsAreForceYellow);
    Assert(!result.PathSegmentsAreForceYellow[0]);
    Assert(result.PathSegmentsAreForceYellow[1]);
}

static void TestLegacyNull()
{
    var result = ForceYellowAppearance.ResolveSlide(false, 1, null, "1-3[8:1]");
    Assert(!result.MovingStarsAreForceYellow);
    Assert(!result.PathSegmentsAreForceYellow[0]);
}

static void TestLoaderPreflightOrder()
{
    var source = ReadRuntimeSource("JsonDataLoader.cs");
    AssertOrdered(
        source,
        "if (!TryValidateForceYellowData(loadedData.timingList))",
        "noteParserTask = StartCoroutine(LoadNotes(loadedData.timingList, ignoreOffset, lastNoteTime));");
    AssertContains(source, "ForceYellowAppearance.ValidateNonSlideIndices(segmentIndices, note.RawContent);");
}

static void TestOrdinaryRuntimeWiring()
{
    var source = ReadRuntimeSource("JsonDataLoader.cs");

    AssertContains(
        GetSection(source, "if (note.Type == SimaiNoteType.Tap)", "else if (note.Type == SimaiNoteType.Hold)"),
        "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertContains(
        GetSection(source, "else if (note.Type == SimaiNoteType.Hold)", "else if (note.Type == SimaiNoteType.TouchHold)"),
        "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertContains(
        GetSection(source, "else if (note.Type == SimaiNoteType.TouchHold)", "else if (note.Type == SimaiNoteType.Touch)"),
        "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertContains(
        GetSection(source, "else if (note.Type == SimaiNoteType.Touch)", "else if (note.Type == SimaiNoteType.Slide)"),
        "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertDoesNotContain(source, "isEach = note.IsForceYellow");
}

static void TestSlideRuntimeWiring()
{
    var source = ReadRuntimeSource("JsonDataLoader.cs");
    var connected = GetSection(
        source,
        "private void InstantiateStarGroup",
        "private GameObject InstantiateWifi");
    var wifi = GetSection(
        source,
        "private GameObject InstantiateWifi",
        "private GameObject InstantiateStar");
    var slide = GetSection(
        source,
        "private GameObject InstantiateStar",
        "private bool detectJustType");

    AssertContains(connected, "o.IsForceYellow = forceYellowAppearance.MovingStarsAreForceYellow;");
    AssertContains(connected, "o.ForceYellowSlideSegmentIndices = forceYellowAppearance.PathSegmentsAreForceYellow[i]");

    AssertContains(wifi, "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertContains(wifi, "WifiCompo.isForceYellowMovingStar = forceYellowAppearance.MovingStarsAreForceYellow;");
    AssertContains(wifi, "WifiCompo.isForceYellowPath = forceYellowAppearance.PathSegmentsAreForceYellow[0];");

    AssertContains(slide, "NDCompo.isForceYellow = note.IsForceYellow;");
    AssertContains(slide, "SliCompo.isForceYellowMovingStar = forceYellowAppearance.MovingStarsAreForceYellow;");
    AssertContains(slide, "SliCompo.isForceYellowPath = forceYellowAppearance.PathSegmentsAreForceYellow[0];");
}

static void TestSpriteRuntimeWiring()
{
    AssertOccurrenceCount(
        ReadRuntimeSource("Notes", "TapDrop.cs"),
        "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow)",
        1);
    AssertOccurrenceCount(
        ReadRuntimeSource("Notes", "HoldDrop.cs"),
        "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow)",
        2);
    AssertOccurrenceCount(
        ReadRuntimeSource("Notes", "TouchDrop.cs"),
        "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow)",
        1);
    AssertOccurrenceCount(
        ReadRuntimeSource("Notes", "TouchHoldDrop.cs"),
        "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow)",
        1);
    AssertOccurrenceCount(
        ReadRuntimeSource("Notes", "StarDrop.cs"),
        "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellow)",
        2);

    var slide = ReadRuntimeSource("Notes", "SlideDrop.cs");
    AssertContains(slide, "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellowMovingStar)");
    AssertContains(slide, "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellowPath)");

    var wifi = ReadRuntimeSource("Notes", "WifiDrop.cs");
    AssertContains(wifi, "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellowMovingStar)");
    AssertContains(wifi, "ForceYellowAppearance.UsesEachVisual(isEach, isForceYellowPath)");
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

static string GetSection(string source, string startMarker, string endMarker)
{
    var start = source.IndexOf(startMarker, StringComparison.Ordinal);
    if (start < 0)
        throw new InvalidOperationException($"Missing section start: {startMarker}");

    var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
    if (end < 0)
        throw new InvalidOperationException($"Missing section end: {endMarker}");

    return source.Substring(start, end - start);
}

static void AssertOrdered(string source, string first, string second)
{
    var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
    var secondIndex = source.IndexOf(second, StringComparison.Ordinal);
    if (firstIndex < 0 || secondIndex < 0 || firstIndex >= secondIndex)
        throw new InvalidOperationException($"Expected \"{first}\" before \"{second}\"");
}

static void AssertContains(string source, string expected)
{
    if (!source.Contains(expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Missing expected runtime wiring: {expected}");
}

static void AssertDoesNotContain(string source, string unexpected)
{
    if (source.Contains(unexpected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Unexpected runtime wiring: {unexpected}");
}

static void AssertOccurrenceCount(string source, string value, int expected)
{
    var count = 0;
    var cursor = 0;
    while ((cursor = source.IndexOf(value, cursor, StringComparison.Ordinal)) >= 0)
    {
        count++;
        cursor += value.Length;
    }

    AssertEqual(expected, count);
}

static void ExpectInvalid(Action action)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }

    throw new InvalidOperationException("Expected InvalidDataException");
}

static void Assert(bool value)
{
    if (!value)
        throw new InvalidOperationException("Assertion failed");
}

static void AssertEqual<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
}
