#nullable enable

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

Console.WriteLine("Chapter load state tests passed.");

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
