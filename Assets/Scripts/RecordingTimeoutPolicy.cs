using MajSimai;
using System;
using System.Collections.Generic;
using System.Globalization;

internal static class RecordingTimeoutPolicy
{
    internal const float GracePeriodSeconds = 10f;

    internal static float CalculateChartEndTime(IEnumerable<SimaiTimingPoint> timingPoints)
    {
        if (timingPoints == null)
            throw new ArgumentNullException(nameof(timingPoints));

        var chartEndTime = 0d;
        foreach (var timingPoint in timingPoints)
        {
            if (timingPoint?.Notes == null)
                continue;

            foreach (var note in timingPoint.Notes)
            {
                if (note == null)
                    continue;

                var noteEndTime = timingPoint.Timing;
                if (note.Type == SimaiNoteType.Slide)
                    noteEndTime = Math.Max(noteEndTime, note.SlideStartTime + note.SlideTime);
                else if (note.Type is SimaiNoteType.Hold or SimaiNoteType.TouchHold)
                    noteEndTime = Math.Max(noteEndTime, timingPoint.Timing + note.HoldTime);

                chartEndTime = Math.Max(chartEndTime, noteEndTime);
            }
        }

        return (float)Math.Max(0d, chartEndTime);
    }

    internal static float CalculateCutoffTime(float chartEndTime)
    {
        if (float.IsNaN(chartEndTime) || float.IsInfinity(chartEndTime))
            throw new ArgumentOutOfRangeException(nameof(chartEndTime));

        return Math.Max(0f, chartEndTime) + GracePeriodSeconds;
    }

    internal static bool HasReachedCutoff(float audioTime, float cutoffTime)
    {
        return cutoffTime > 0f &&
               !float.IsNaN(audioTime) &&
               !float.IsInfinity(audioTime) &&
               !float.IsNaN(cutoffTime) &&
               !float.IsInfinity(cutoffTime) &&
               audioTime >= cutoffTime;
    }

    internal static string FormatProgress(
        int tapCount,
        int tapSum,
        int holdCount,
        int holdSum,
        int slideCount,
        int slideSum,
        int touchCount,
        int touchSum,
        int breakCount,
        int breakSum,
        int mineCount,
        int mineSum)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "TAP {0}/{1}, HOD {2}/{3}, SLD {4}/{5}, TOH {6}/{7}, BRK {8}/{9}, MIN {10}/{11}",
            tapCount,
            tapSum,
            holdCount,
            holdSum,
            slideCount,
            slideSum,
            touchCount,
            touchSum,
            breakCount,
            breakSum,
            mineCount,
            mineSum);
    }
}