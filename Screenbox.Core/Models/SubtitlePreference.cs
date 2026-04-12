#nullable enable

using System.Collections.Generic;

namespace Screenbox.Core.Models;

public sealed class SubtitlePreference
{
    public SubtitleSelectionMode Mode { get; set; }

    public List<TrackCandidate> Candidates { get; set; } = new();

    public static SubtitlePreference Auto() => new()
    {
        Mode = SubtitleSelectionMode.Auto
    };
}
