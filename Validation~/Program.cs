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
    AssertEqual(1, ForceYellowAppearance.CountSlideSegments("1-3[8:1]"));
    AssertEqual(2, ForceYellowAppearance.CountSlideSegments("1-3[8:1]-5[8:1]"));
    AssertEqual(1, ForceYellowAppearance.CountSlideSegments("1pp3[8:1]"));
    AssertEqual(1, ForceYellowAppearance.CountSlideSegments("1V35[8:1]"));
    AssertEqual(1, ForceYellowAppearance.CountSlideSegments("1w5[8:1]"));
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