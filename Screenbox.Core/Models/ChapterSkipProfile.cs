#nullable enable

using System.Collections.Generic;

namespace Screenbox.Core.Models;

public sealed class ChapterSkipProfile
{
    public ChapterSkipScope Scope { get; set; }

    public string Location { get; set; } = string.Empty;

    public List<ChapterSkipRule> Rules { get; set; } = new();
}
