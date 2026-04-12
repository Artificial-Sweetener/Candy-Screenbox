#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Screenbox.Core.Models;

namespace Screenbox.Core.Services;

public interface IChapterSkipStore
{
    event EventHandler? ProfilesChanged;

    bool IsLoaded { get; }

    IReadOnlyList<ChapterSkipProfile> Profiles { get; }

    Task LoadAsync();

    Task SaveAsync();

    ChapterSkipProfile? GetProfile(ChapterSkipScope scope, string location);

    Task SetProfileAsync(ChapterSkipProfile profile);

    Task RemoveProfileAsync(ChapterSkipScope scope, string location);
}
