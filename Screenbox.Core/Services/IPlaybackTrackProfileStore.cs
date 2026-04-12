#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Screenbox.Core.Models;

namespace Screenbox.Core.Services;

public interface IPlaybackTrackProfileStore
{
    event EventHandler? ProfilesChanged;

    bool IsLoaded { get; }

    IReadOnlyList<PlaybackTrackProfile> Profiles { get; }

    Task LoadAsync();

    Task SaveAsync();

    PlaybackTrackProfile? GetProfile(PlaybackTrackScope scope, string location);

    Task SetProfileAsync(PlaybackTrackProfile profile);

    Task RemoveProfileAsync(PlaybackTrackScope scope, string location);
}
