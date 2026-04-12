#nullable enable

using System.Collections.Generic;

namespace Screenbox.Core.Models;

public sealed class TrackPreference
{
    public TrackSelectionMode Mode { get; set; }

    public List<TrackCandidate> Candidates { get; set; } = new();

    public static TrackPreference DefaultAudio() => new()
    {
        Mode = TrackSelectionMode.Default
    };
}
