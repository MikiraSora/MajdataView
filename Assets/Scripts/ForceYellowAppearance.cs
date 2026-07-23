using System;
using System.IO;

#nullable enable

internal readonly struct ForceYellowSlideAppearance
{
    public bool MovingStarsAreForceYellow { get; }
    public bool[] PathSegmentsAreForceYellow { get; }

    public ForceYellowSlideAppearance(bool movingStarsAreForceYellow, bool[] pathSegmentsAreForceYellow)
    {
        MovingStarsAreForceYellow = movingStarsAreForceYellow;
        PathSegmentsAreForceYellow = pathSegmentsAreForceYellow;
    }
}

internal static class ForceYellowAppearance
{
    public static bool UsesEachVisual(bool isEach, bool isForceYellow)
    {
        return isEach || isForceYellow;
    }

    public static ForceYellowSlideAppearance ResolveSlide(
        bool isHeadForceYellow,
        int segmentCount,
        int[]? forceYellowSegmentIndices,
        string? rawContent)
    {
        if (segmentCount < 0)
            throw new ArgumentOutOfRangeException(nameof(segmentCount));

        var pathSegments = new bool[segmentCount];
        if (forceYellowSegmentIndices is null || forceYellowSegmentIndices.Length == 0)
            return new ForceYellowSlideAppearance(isHeadForceYellow, pathSegments);

        var previousIndex = -1;
        for (var i = 0; i < forceYellowSegmentIndices.Length; i++)
        {
            var segmentIndex = forceYellowSegmentIndices[i];
            if (segmentIndex < 0 || segmentIndex >= segmentCount)
            {
                throw InvalidSegmentIndex(
                    rawContent,
                    $"index {segmentIndex} is outside the valid range 0..{segmentCount - 1}");
            }
            if (segmentIndex <= previousIndex)
            {
                throw InvalidSegmentIndex(
                    rawContent,
                    $"index {segmentIndex} at array position {i} is not strictly greater than {previousIndex}");
            }

            pathSegments[segmentIndex] = true;
            previousIndex = segmentIndex;
        }

        return new ForceYellowSlideAppearance(isHeadForceYellow, pathSegments);
    }

    public static void ValidateNonSlideIndices(int[]? forceYellowSegmentIndices, string? rawContent)
    {
        if (forceYellowSegmentIndices is null || forceYellowSegmentIndices.Length == 0)
            return;

        throw InvalidSegmentIndex(rawContent, "slide segment indices are only valid for Slide notes");
    }

    public static int CountSlideSegments(string? rawContent)
    {
        if (string.IsNullOrEmpty(rawContent))
            return 0;

        var segmentCount = 0;
        var insideDuration = false;
        for (var i = 0; i < rawContent.Length; i++)
        {
            var current = rawContent[i];
            if (current == '[')
            {
                insideDuration = true;
                continue;
            }
            if (current == ']')
            {
                insideDuration = false;
                continue;
            }
            if (insideDuration || !IsSlideMark(current))
                continue;

            segmentCount++;
            if ((current == 'p' || current == 'q') &&
                i + 1 < rawContent.Length &&
                rawContent[i + 1] == current)
            {
                i++;
            }
        }

        return segmentCount;
    }

    private static bool IsSlideMark(char value)
    {
        return value == '-' || value == '^' || value == 'v' || value == '<' || value == '>' ||
               value == 'V' || value == 'p' || value == 'q' || value == 's' || value == 'z' ||
               value == 'w';
    }

    private static InvalidDataException InvalidSegmentIndex(string? rawContent, string reason)
    {
        return new InvalidDataException(
            $"Force Yellow slide segment index is invalid for note \"{rawContent ?? string.Empty}\": {reason}");
    }
}
