#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Screenbox.Core.Messages;
using Screenbox.Core.Models;
using Windows.Storage;

namespace Screenbox.Core.Services;

public sealed class PlaybackTrackProfileStore : ObservableRecipient, IPlaybackTrackProfileStore, IRecipient<SuspendingMessage>
{
    private const string SaveFileName = "playback_track_profiles.json";

    public event EventHandler? ProfilesChanged;

    public bool IsLoaded { get; private set; }

    public IReadOnlyList<PlaybackTrackProfile> Profiles => _profiles;

    private readonly IFilesService _filesService;
    private readonly List<PlaybackTrackProfile> _profiles = new();

    public PlaybackTrackProfileStore(IFilesService filesService)
    {
        _filesService = filesService;
        IsActive = true;
    }

    public void Receive(SuspendingMessage message)
    {
        message.Reply(SaveAsync());
    }

    public async Task LoadAsync()
    {
        if (IsLoaded) return;

        try
        {
            PersistentPlaybackTrackProfiles stored =
                await _filesService.LoadFromDiskAsync<PersistentPlaybackTrackProfiles>(
                    ApplicationData.Current.LocalFolder,
                    SaveFileName);

            _profiles.Clear();
            _profiles.AddRange(stored.Profiles.Where(IsValidProfile));
        }
        catch (FileNotFoundException)
        {
            // First run with no configured playback track profiles.
        }
        catch (Exception e)
        {
            LogService.Log(e);
        }
        finally
        {
            IsLoaded = true;
            ProfilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            PersistentPlaybackTrackProfiles stored = new()
            {
                Profiles = _profiles.ToList()
            };
            await _filesService.SaveToDiskAsync(ApplicationData.Current.LocalFolder, SaveFileName, stored);
        }
        catch (FileLoadException)
        {
            // File in use. Skipped.
        }
        catch (Exception e)
        {
            LogService.Log(e);
        }
    }

    public PlaybackTrackProfile? GetProfile(PlaybackTrackScope scope, string location)
    {
        string normalizedLocation = NormalizeLocation(scope, location);
        return _profiles.FirstOrDefault(profile =>
            profile.Scope == scope &&
            NormalizeLocation(profile.Scope, profile.Location).Equals(normalizedLocation, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetProfileAsync(PlaybackTrackProfile profile)
    {
        await LoadAsync();
        PlaybackTrackProfile normalizedProfile = new()
        {
            Scope = profile.Scope,
            Location = NormalizeLocation(profile.Scope, profile.Location),
            Audio = profile.Audio,
            Subtitle = profile.Subtitle
        };

        _profiles.RemoveAll(existing =>
            existing.Scope == normalizedProfile.Scope &&
            NormalizeLocation(existing.Scope, existing.Location).Equals(normalizedProfile.Location, StringComparison.OrdinalIgnoreCase));

        if (IsValidProfile(normalizedProfile))
            _profiles.Add(normalizedProfile);

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await SaveAsync();
    }

    public async Task RemoveProfileAsync(PlaybackTrackScope scope, string location)
    {
        await LoadAsync();
        string normalizedLocation = NormalizeLocation(scope, location);
        int removed = _profiles.RemoveAll(profile =>
            profile.Scope == scope &&
            NormalizeLocation(profile.Scope, profile.Location).Equals(normalizedLocation, StringComparison.OrdinalIgnoreCase));

        if (removed == 0) return;

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await SaveAsync();
    }

    private static bool IsValidProfile(PlaybackTrackProfile profile)
    {
        return profile.Scope == PlaybackTrackScope.Global || !string.IsNullOrWhiteSpace(profile.Location);
    }

    private static string NormalizeLocation(PlaybackTrackScope scope, string location)
    {
        return scope == PlaybackTrackScope.Global ? string.Empty : location.Trim();
    }
}
