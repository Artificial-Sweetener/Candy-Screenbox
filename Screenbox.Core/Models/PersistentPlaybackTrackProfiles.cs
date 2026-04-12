#nullable enable

using System.Collections.Generic;

namespace Screenbox.Core.Models;

public sealed class PersistentPlaybackTrackProfiles
{
    public int Version { get; set; } = 1;

    public List<PlaybackTrackProfile> Profiles { get; set; } = new();
}
