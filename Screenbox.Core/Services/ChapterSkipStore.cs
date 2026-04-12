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

public sealed class ChapterSkipStore : ObservableRecipient, IChapterSkipStore, IRecipient<SuspendingMessage>
{
    private const string SaveFileName = "chapter_skip_profiles.json";

    public event EventHandler? ProfilesChanged;

    public bool IsLoaded { get; private set; }

    public IReadOnlyList<ChapterSkipProfile> Profiles => _profiles;

    private readonly IFilesService _filesService;
    private readonly List<ChapterSkipProfile> _profiles = new();

    public ChapterSkipStore(IFilesService filesService)
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
            PersistentChapterSkipProfiles stored =
                await _filesService.LoadFromDiskAsync<PersistentChapterSkipProfiles>(
                    ApplicationData.Current.LocalFolder,
                    SaveFileName);

            _profiles.Clear();
            _profiles.AddRange(stored.Profiles.Where(IsValidProfile));
        }
        catch (FileNotFoundException)
        {
            // First run with no configured chapter skip rules.
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
            PersistentChapterSkipProfiles stored = new()
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

    public ChapterSkipProfile? GetProfile(ChapterSkipScope scope, string location)
    {
        string normalizedLocation = NormalizeLocation(scope, location);
        return _profiles.FirstOrDefault(profile =>
            profile.Scope == scope &&
            NormalizeLocation(profile.Scope, profile.Location).Equals(normalizedLocation, StringComparison.OrdinalIgnoreCase));
    }

    public async Task SetProfileAsync(ChapterSkipProfile profile)
    {
        await LoadAsync();
        ChapterSkipProfile normalizedProfile = new()
        {
            Scope = profile.Scope,
            Location = NormalizeLocation(profile.Scope, profile.Location),
            Rules = profile.Rules.ToList()
        };

        _profiles.RemoveAll(existing =>
            existing.Scope == normalizedProfile.Scope &&
            NormalizeLocation(existing.Scope, existing.Location).Equals(normalizedProfile.Location, StringComparison.OrdinalIgnoreCase));

        if (normalizedProfile.Rules.Count > 0)
            _profiles.Add(normalizedProfile);

        ProfilesChanged?.Invoke(this, EventArgs.Empty);
        await SaveAsync();
    }

    public async Task RemoveProfileAsync(ChapterSkipScope scope, string location)
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

    private static bool IsValidProfile(ChapterSkipProfile profile)
    {
        return profile.Scope == ChapterSkipScope.Global || !string.IsNullOrWhiteSpace(profile.Location);
    }

    private static string NormalizeLocation(ChapterSkipScope scope, string location)
    {
        return scope == ChapterSkipScope.Global ? string.Empty : location.Trim();
    }
}
