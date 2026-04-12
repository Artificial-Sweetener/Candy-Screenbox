#nullable enable

using System.Collections.Generic;

namespace Screenbox.Core.Models;

public sealed class PersistentChapterSkipProfiles
{
    public int Version { get; set; } = 1;

    public List<ChapterSkipProfile> Profiles { get; set; } = new();
}
