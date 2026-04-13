#nullable enable

using System;
using System.Collections.Generic;
using Screenbox.Core.Models;

namespace Screenbox.Core.Controllers;

internal static class ChapterSkipPlanner
{
    public static ChapterSkipPlan CreatePlan(
        TimeSpan position,
        IReadOnlyList<ChapterSkipRange> ranges,
        TimeSpan naturalDuration,
        TimeSpan endBoundaryTolerance,
        TimeSpan rangeMergeTolerance)
    {
        for (int i = 0; i < ranges.Count; i++)
        {
            ChapterSkipRange range = ranges[i];
            if (!ShouldSkip(position, range, endBoundaryTolerance)) continue;

            return ExpandRange(i, ranges, naturalDuration, endBoundaryTolerance, rangeMergeTolerance);
        }

        return ChapterSkipPlan.None;
    }

    public static bool ShouldSkip(TimeSpan position, ChapterSkipRange range, TimeSpan endBoundaryTolerance)
    {
        return position >= range.StartTime &&
               position < range.EndTime - endBoundaryTolerance;
    }

    public static bool EndsAtMediaEnd(TimeSpan endTime, TimeSpan naturalDuration, TimeSpan endBoundaryTolerance)
    {
        return naturalDuration > TimeSpan.Zero &&
               endTime >= naturalDuration - endBoundaryTolerance;
    }

    private static ChapterSkipPlan ExpandRange(
        int startRangeIndex,
        IReadOnlyList<ChapterSkipRange> ranges,
        TimeSpan naturalDuration,
        TimeSpan endBoundaryTolerance,
        TimeSpan rangeMergeTolerance)
    {
        ChapterSkipRange startRange = ranges[startRangeIndex];
        TimeSpan startTime = startRange.StartTime;
        TimeSpan endTime = startRange.EndTime;
        int endChapterIndex = startRange.ChapterIndex;

        for (int i = startRangeIndex + 1; i < ranges.Count; i++)
        {
            ChapterSkipRange nextRange = ranges[i];
            if (nextRange.StartTime > endTime + rangeMergeTolerance)
                break;

            if (nextRange.EndTime > endTime)
            {
                endTime = nextRange.EndTime;
            }

            endChapterIndex = nextRange.ChapterIndex;
        }

        ChapterSkipPlanAction action = EndsAtMediaEnd(endTime, naturalDuration, endBoundaryTolerance)
            ? ChapterSkipPlanAction.EndMedia
            : ChapterSkipPlanAction.Seek;

        return new ChapterSkipPlan(
            action,
            startRange.ChapterIndex,
            endChapterIndex,
            startTime,
            endTime);
    }
}

internal enum ChapterSkipPlanAction
{
    None,
    Seek,
    EndMedia
}

internal readonly struct ChapterSkipPlan
{
    public static ChapterSkipPlan None { get; } = new(
        ChapterSkipPlanAction.None,
        -1,
        -1,
        TimeSpan.Zero,
        TimeSpan.Zero);

    public ChapterSkipPlanAction Action { get; }

    public int StartChapterIndex { get; }

    public int EndChapterIndex { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan EndTime { get; }

    public ChapterSkipPlan(
        ChapterSkipPlanAction action,
        int startChapterIndex,
        int endChapterIndex,
        TimeSpan startTime,
        TimeSpan endTime)
    {
        Action = action;
        StartChapterIndex = startChapterIndex;
        EndChapterIndex = endChapterIndex;
        StartTime = startTime;
        EndTime = endTime;
    }
}
