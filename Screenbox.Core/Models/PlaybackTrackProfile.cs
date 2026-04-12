#nullable enable

namespace Screenbox.Core.Models;

public sealed class PlaybackTrackProfile
{
    public PlaybackTrackScope Scope { get; set; }

    public string Location { get; set; } = string.Empty;

    public TrackPreference Audio { get; set; } = TrackPreference.DefaultAudio();

    public SubtitlePreference Subtitle { get; set; } = SubtitlePreference.Auto();
}
