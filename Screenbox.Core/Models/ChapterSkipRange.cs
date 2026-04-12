#nullable enable

using System;

namespace Screenbox.Core.Models;

public readonly struct ChapterSkipRange
{
    public int ChapterIndex { get; }

    public TimeSpan StartTime { get; }

    public TimeSpan EndTime { get; }

    public ChapterSkipRule Rule { get; }

    public ChapterSkipRange(int chapterIndex, TimeSpan startTime, TimeSpan endTime, ChapterSkipRule rule)
    {
        ChapterIndex = chapterIndex;
        StartTime = startTime;
        EndTime = endTime;
        Rule = rule;
    }
}
