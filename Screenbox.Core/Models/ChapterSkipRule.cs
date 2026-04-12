#nullable enable

namespace Screenbox.Core.Models;

public sealed class ChapterSkipRule
{
    public bool IsEnabled { get; set; } = true;

    public ChapterMatchMode MatchMode { get; set; }

    public string Pattern { get; set; } = string.Empty;

    public int Index { get; set; }
}
