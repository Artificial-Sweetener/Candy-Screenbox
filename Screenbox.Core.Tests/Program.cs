#nullable enable

using Screenbox.Core.Controllers;
using Screenbox.Core.Models;
using Screenbox.Core.Playback;

AssertFalse(
    "not current playback item",
    ChapterLoadState.CanReadChapters(false, false, TimeSpan.FromMinutes(5)));

AssertFalse(
    "opening state",
    ChapterLoadState.CanReadChapters(true, true, TimeSpan.FromMinutes(5)));

AssertFalse(
    "zero duration",
    ChapterLoadState.CanReadChapters(true, false, TimeSpan.Zero));

AssertTrue(
    "ready current media",
    ChapterLoadState.CanReadChapters(true, false, TimeSpan.FromMilliseconds(1)));

bool earlyAttempt = ChapterLoadState.CanReadChapters(true, true, TimeSpan.Zero);
bool laterAttempt = ChapterLoadState.CanReadChapters(true, false, TimeSpan.FromMinutes(5));
AssertFalse("early not-ready attempt", earlyAttempt);
AssertTrue("later ready attempt", laterAttempt);

TimeSpan endBoundaryTolerance = TimeSpan.FromMilliseconds(250);
TimeSpan mergeTolerance = TimeSpan.FromMilliseconds(250);
TimeSpan duration = TimeSpan.FromMinutes(5);

ChapterSkipPlan singleMiddlePlan = CreatePlan(
    TimeSpan.FromSeconds(65),
    duration,
    Range(1, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(90)));
AssertEqual("single middle action", ChapterSkipPlanAction.Seek, singleMiddlePlan.Action);
AssertEqual("single middle end", TimeSpan.FromSeconds(90), singleMiddlePlan.EndTime);
AssertEqual("single middle start chapter", 1, singleMiddlePlan.StartChapterIndex);
AssertEqual("single middle end chapter", 1, singleMiddlePlan.EndChapterIndex);

ChapterSkipPlan adjacentPlan = CreatePlan(
    TimeSpan.FromSeconds(10),
    duration,
    Range(0, TimeSpan.Zero, TimeSpan.FromSeconds(90)),
    Range(1, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(180)));
AssertEqual("adjacent action", ChapterSkipPlanAction.Seek, adjacentPlan.Action);
AssertEqual("adjacent collapsed end", TimeSpan.FromSeconds(180), adjacentPlan.EndTime);
AssertEqual("adjacent end chapter", 1, adjacentPlan.EndChapterIndex);

ChapterSkipPlan overlappingPlan = CreatePlan(
    TimeSpan.FromSeconds(10),
    duration,
    Range(0, TimeSpan.Zero, TimeSpan.FromSeconds(91)),
    Range(1, TimeSpan.FromSeconds(90), TimeSpan.FromSeconds(180)));
AssertEqual("overlapping action", ChapterSkipPlanAction.Seek, overlappingPlan.Action);
AssertEqual("overlapping collapsed end", TimeSpan.FromSeconds(180), overlappingPlan.EndTime);

ChapterSkipPlan gapPlan = CreatePlan(
    TimeSpan.FromSeconds(10),
    duration,
    Range(0, TimeSpan.Zero, TimeSpan.FromSeconds(90)),
    Range(1, TimeSpan.FromMilliseconds(90251), TimeSpan.FromSeconds(180)));
AssertEqual("gap action", ChapterSkipPlanAction.Seek, gapPlan.Action);
AssertEqual("gap does not collapse", TimeSpan.FromSeconds(90), gapPlan.EndTime);
AssertEqual("gap end chapter", 0, gapPlan.EndChapterIndex);

ChapterSkipPlan endingPlan = CreatePlan(
    TimeSpan.FromSeconds(10),
    duration,
    Range(0, TimeSpan.Zero, TimeSpan.FromSeconds(90)),
    Range(1, TimeSpan.FromSeconds(90), duration));
AssertEqual("ending chain action", ChapterSkipPlanAction.EndMedia, endingPlan.Action);
AssertEqual("ending chain end", duration, endingPlan.EndTime);

ChapterSkipPlan nearEndBoundaryPlan = CreatePlan(
    TimeSpan.FromMilliseconds(89750),
    duration,
    Range(0, TimeSpan.Zero, TimeSpan.FromSeconds(90)));
AssertEqual("near end boundary action", ChapterSkipPlanAction.None, nearEndBoundaryPlan.Action);

Console.WriteLine("Chapter load state and chapter skip planner tests passed.");

ChapterSkipPlan CreatePlan(TimeSpan position, TimeSpan naturalDuration, params ChapterSkipRange[] ranges)
{
    return ChapterSkipPlanner.CreatePlan(
        position,
        ranges,
        naturalDuration,
        endBoundaryTolerance,
        mergeTolerance);
}

static ChapterSkipRange Range(int chapterIndex, TimeSpan startTime, TimeSpan endTime)
{
    return new ChapterSkipRange(chapterIndex, startTime, endTime, new ChapterSkipRule());
}

static void AssertTrue(string name, bool condition)
{
    if (!condition)
        throw new InvalidOperationException($"Expected true: {name}");
}

static void AssertFalse(string name, bool condition)
{
    if (condition)
        throw new InvalidOperationException($"Expected false: {name}");
}

static void AssertEqual<T>(string name, T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected}, got {actual}: {name}");
}
