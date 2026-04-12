#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Screenbox.Core.Contexts;
using Screenbox.Core.Enums;
using Screenbox.Core.Helpers;
using Screenbox.Core.Messages;
using Screenbox.Core.Models;
using Screenbox.Core.Playback;
using Screenbox.Core.Services;
using Windows.Storage;
using Windows.Storage.Search;

namespace Screenbox.Core.ViewModels;

public sealed partial class CompositeTrackPickerViewModel : ObservableRecipient,
    IRecipient<PlaylistCurrentItemChangedMessage>
{
    private static readonly string[] SubtitleLabelHints = ["sign", "song", "forced", "full", "default"];

    public ObservableCollection<string> SubtitleTracks { get; }

    public ObservableCollection<string> AudioTracks { get; }

    public ObservableCollection<string> VideoTracks { get; }

    private PlaybackSubtitleTrackList? ItemSubtitleTrackList => MediaPlayer?.PlaybackItem?.SubtitleTracks;

    private PlaybackAudioTrackList? ItemAudioTrackList => MediaPlayer?.PlaybackItem?.AudioTracks;

    private PlaybackVideoTrackList? ItemVideoTrackList => MediaPlayer?.PlaybackItem?.VideoTracks;

    private IMediaPlayer? MediaPlayer => _playerContext.MediaPlayer;

    /// <summary>
    /// The currently selected subtitle track UI index.
    /// <list type="bullet">
    /// <item><description><c>0</c> = subtitles disabled (corresponds to the prepended "Disable" option in the UI).</description></item>
    /// <item><description><c>1</c> to <c>SubtitleTracks.Count</c> = the <c>SelectedIndex</c> of an enabled subtitle track in the UI; the
    /// actual underlying subtitle track index is typically obtained by subtracting <c>1</c> from this value.</description></item>
    /// </list>
    /// </summary>
    [ObservableProperty] private int _subtitleTrackIndex;

    /// <summary>
    /// The currently selected audio track index. <c>-1</c> means no track is selected.
    /// </summary>
    [ObservableProperty] private int _audioTrackIndex;

    /// <summary>
    /// The currently selected video track index. <c>-1</c> means no track is selected.
    /// </summary>
    [ObservableProperty] private int _videoTrackIndex;

    private readonly IFilesService _filesService;
    private readonly IPlaybackTrackProfileStore _trackProfileStore;
    private readonly PlaybackTrackProfileResolver _trackProfileResolver;
    private readonly PlaybackTrackSelector _trackSelector;
    private readonly PlayerContext _playerContext;
    private MediaViewModel? _currentMedia;
    private bool _flyoutOpened;
    private CancellationTokenSource? _cts;

    public CompositeTrackPickerViewModel(PlayerContext playerContext, IFilesService filesService,
        IPlaybackTrackProfileStore trackProfileStore,
        PlaybackTrackProfileResolver trackProfileResolver, PlaybackTrackSelector trackSelector)
    {
        _filesService = filesService;
        _trackProfileStore = trackProfileStore;
        _trackProfileResolver = trackProfileResolver;
        _trackSelector = trackSelector;
        _playerContext = playerContext;
        SubtitleTracks = new ObservableCollection<string>();
        AudioTracks = new ObservableCollection<string>();
        VideoTracks = new ObservableCollection<string>();

        IsActive = true;
    }

    /// <summary>
    /// Try load a subtitle in the same directory with the same name
    /// </summary>
    public async void Receive(PlaylistCurrentItemChangedMessage message)
    {
        _cts?.Cancel();
        _currentMedia = null;
        NotifyProfileCommandCanExecuteChanged();
        if (MediaPlayer is not VlcMediaPlayer player) return;
        if (message.Value is not { Source: StorageFile file, MediaType: MediaPlaybackType.Video } media)
            return;

        _currentMedia = media;
        var playbackItem = media.Item.Value;
        if (playbackItem == null) return;
        var playbackSubtitleTrackList = playbackItem.SubtitleTracks;
        IReadOnlyList<StorageFile> subtitles = await GetSubtitlesForFile(file, message.NeighboringFilesQuery);
        foreach (StorageFile subtitleFile in subtitles)
        {
            // Preload subtitle but don't select it
            playbackSubtitleTrackList.AddExternalSubtitle(player, subtitleFile, false);
        }

        try
        {
            using var cts = new CancellationTokenSource();
            _cts = cts;
            await playbackItem.Media.WaitForParsed(TimeSpan.FromSeconds(5), cts.Token);

            if (cts.IsCancellationRequested || !ReferenceEquals(_currentMedia, media)) return;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (_cts?.IsCancellationRequested == true || ReferenceEquals(_currentMedia, media))
                _cts = null;
        }

        await _trackProfileStore.LoadAsync();
        if (!ReferenceEquals(_currentMedia, media)) return;

        PlaybackTrackProfile? profile = _trackProfileResolver.GetEffectiveProfile(media.Location, _trackProfileStore.Profiles);
        _trackSelector.Apply(playbackItem.AudioTracks, playbackItem.SubtitleTracks, profile, LanguageHelper.GetPreferredLanguage());

        if (_flyoutOpened)
        {
            RefreshTrackLists();
        }

        NotifyProfileCommandCanExecuteChanged();
    }

    private async Task<IReadOnlyList<StorageFile>> GetSubtitlesForFile(StorageFile sourceFile, StorageFileQueryResult? neighboringFilesQuery = null)
    {
        IReadOnlyList<StorageFile> subtitles = Array.Empty<StorageFile>();
        string rawName = Path.GetFileNameWithoutExtension(sourceFile.Name);

        // 1. Define your separators
        char[] separators = [' ', '.', '_', '-', '[', ']', '(', ')', '{', '}', ',', ';', '"', '\''];

        // 2. Break the name into tokens, removing empty entries to avoid double wildcards (**)
        string[] tokens = rawName.Split(separators, StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0) return subtitles;

        // If we have a neighboring files query from the playlist, use it and filter for subtitles
        if (neighboringFilesQuery != null)
        {
            try
            {
                var escapedTokens = tokens.Select(token => Regex.Escape(token)).ToList();

                // STRATEGY A: Strict "Skeleton" Match
                var strictRegexPattern = "^" + string.Join(".*", escapedTokens) + ".*$";
                IReadOnlyList<StorageFile> files = await neighboringFilesQuery.GetFilesAsync(0, 50);
                subtitles = files.Where(f =>
                       f.IsSupportedSubtitle() && Regex.IsMatch(f.Name, strictRegexPattern, RegexOptions.IgnoreCase))
                    .ToArray();
                if (subtitles.Count == 0 && tokens.Length > 1)
                {
                    // STRATEGY B: Fallback (Partial Tokens Match)
                    var fallbackPattern = "^" + string.Join(".*", escapedTokens.Take(Math.Min(escapedTokens.Count - 1, 3))) + ".*$";
                    subtitles = files.Where(f =>
                            f.IsSupportedSubtitle() && Regex.IsMatch(f.Name, fallbackPattern, RegexOptions.IgnoreCase))
                        .ToArray();
                }
            }
            catch (Exception e)
            {
                LogService.Log(e);
            }
        }
        else
        {
            // Fallback to creating a new query with subtitle filter

            // STRATEGY A: Strict "Skeleton" Match
            // "Iron.Man.2008" -> "Iron*Man*2008*"
            string strictPattern = string.Join("*", tokens) + "*";

            QueryOptions options = new(CommonFileQuery.DefaultQuery, FilesHelpers.SupportedSubtitleFormats)
            {
                ApplicationSearchFilter = $"System.FileName:~\"{strictPattern}\""
            };

            var query = await _filesService.GetNeighboringFilesQueryAsync(sourceFile, options);
            if (query != null)
            {
                subtitles = await query.GetFilesAsync(0, 50);

                // STRATEGY B: Fallback (Partial Tokens Match)
                // If "Iron*Man*2008*" fails, try "Iron*Man*"
                if (subtitles.Count == 0 && tokens.Length > 1)
                {
                    string fallbackPattern = string.Join("*", tokens.Take(Math.Min(tokens.Length - 1, 3))) + "*";
                    options.ApplicationSearchFilter = $"System.FileName:~\"{fallbackPattern}\"";
                    query.ApplyNewQueryOptions(options);
                    subtitles = await query.GetFilesAsync(0, 50);
                }
            }
        }

        return subtitles;
    }

    partial void OnSubtitleTrackIndexChanged(int value)
    {
        if (ItemSubtitleTrackList == null) return;

        // VM index 0 maps to actual track index -1, which is "Disable"
        // Decrement value by 1 to convert from display index to actual subtitle track index
        value = Math.Max(-1, value - 1);
        if (value >= ItemSubtitleTrackList.Count) return;
        ItemSubtitleTrackList.SelectedIndex = value;

        if (_flyoutOpened)
            NotifyProfileCommandCanExecuteChanged();
    }

    partial void OnAudioTrackIndexChanged(int value)
    {
        if (ItemAudioTrackList != null && value >= 0 && value < ItemAudioTrackList.Count)
            ItemAudioTrackList.SelectedIndex = value;

        if (_flyoutOpened)
            NotifyProfileCommandCanExecuteChanged();
    }

    partial void OnVideoTrackIndexChanged(int value)
    {
        if (ItemVideoTrackList != null && value >= 0 && value < ItemVideoTrackList.Count)
            ItemVideoTrackList.SelectedIndex = value;
    }

    /// <summary>
    /// Adds a subtitle file to the current media. Sends a <see cref="Core.Messages.FailedToLoadSubtitleNotificationMessage"/> on failure.
    /// </summary>
    [RelayCommand]
    private async Task AddSubtitleAsync()
    {
        try
        {
            if (ItemSubtitleTrackList == null || MediaPlayer is not VlcMediaPlayer player) return;
            StorageFile? file = await _filesService.PickFileAsync(FilesHelpers.SupportedSubtitleFormats.Add("*").ToArray());
            if (file == null) return;

            ItemSubtitleTrackList.AddExternalSubtitle(player, file, true);
            Messenger.Send(new SubtitleAddedNotificationMessage(file));
        }
        catch (Exception e)
        {
            Messenger.Send(new FailedToLoadSubtitleNotificationMessage(e.Message));
        }
    }


    public void OnFlyoutOpening()
    {
        RefreshTrackLists();

        _flyoutOpened = true;
        NotifyProfileCommandCanExecuteChanged();
    }

    public void OnFlyoutClosed()
    {
        _flyoutOpened = false;
    }

    private void UpdateAudioTrackList()
    {
        if (ItemAudioTrackList == null) return;
        ItemAudioTrackList.Refresh();
        var trackLabels = ItemAudioTrackList.Select(track => track.Label).ToList();
        AudioTracks.SyncItems(trackLabels);
    }

    private void UpdateVideoTrackList()
    {
        if (ItemVideoTrackList == null) return;
        ItemVideoTrackList.Refresh();
        var trackLabels = ItemVideoTrackList.Select(track => track.Label).ToList();
        VideoTracks.SyncItems(trackLabels);
    }

    private void UpdateSubtitleTrackList()
    {
        if (ItemSubtitleTrackList == null) return;
        var trackLabels = ItemSubtitleTrackList.Select(track => track.Label).ToList();
        SubtitleTracks.SyncItems(trackLabels);
    }

    [RelayCommand(CanExecute = nameof(CanSaveFileTrackPreference))]
    private async Task SaveFileTrackPreferenceAsync()
    {
        if (_currentMedia is not { Source: StorageFile file }) return;

        await SaveTrackPreferenceAsync(PlaybackTrackScope.File, file.Path);
    }

    [RelayCommand(CanExecute = nameof(CanSaveFolderTrackPreference))]
    private async Task SaveFolderTrackPreferenceAsync()
    {
        if (_currentMedia is not { Source: StorageFile file }) return;

        StorageFolder? folder = await file.GetParentAsync();
        if (folder == null || string.IsNullOrWhiteSpace(folder.Path)) return;

        await SaveTrackPreferenceAsync(PlaybackTrackScope.Folder, folder.Path);
    }

    [RelayCommand(CanExecute = nameof(CanSaveFileTrackPreference))]
    private async Task ClearFileTrackPreferenceAsync()
    {
        if (_currentMedia is not { Source: StorageFile file }) return;

        await _trackProfileStore.RemoveProfileAsync(PlaybackTrackScope.File, file.Path);
    }

    [RelayCommand(CanExecute = nameof(CanSaveFolderTrackPreference))]
    private async Task ClearFolderTrackPreferenceAsync()
    {
        if (_currentMedia is not { Source: StorageFile file }) return;

        StorageFolder? folder = await file.GetParentAsync();
        if (folder == null || string.IsNullOrWhiteSpace(folder.Path)) return;

        await _trackProfileStore.RemoveProfileAsync(PlaybackTrackScope.Folder, folder.Path);
    }

    private async Task SaveTrackPreferenceAsync(PlaybackTrackScope scope, string location)
    {
        if (ItemAudioTrackList == null || ItemSubtitleTrackList == null || string.IsNullOrWhiteSpace(location)) return;

        PlaybackTrackProfile profile = new()
        {
            Scope = scope,
            Location = location,
            Audio = CreateAudioPreference(ItemAudioTrackList),
            Subtitle = CreateSubtitlePreference(ItemSubtitleTrackList)
        };

        await _trackProfileStore.SetProfileAsync(profile);
    }

    private bool CanSaveFileTrackPreference() =>
        _currentMedia is { Source: StorageFile file } && !string.IsNullOrWhiteSpace(file.Path);

    private bool CanSaveFolderTrackPreference() =>
        _currentMedia is { Source: StorageFile file } && !string.IsNullOrWhiteSpace(file.Path);

    private static TrackPreference CreateAudioPreference(PlaybackAudioTrackList audioTracks)
    {
        if (audioTracks.SelectedIndex < 0 || audioTracks.SelectedIndex >= audioTracks.Count)
        {
            return new TrackPreference
            {
                Mode = TrackSelectionMode.First
            };
        }

        return new TrackPreference
        {
            Mode = TrackSelectionMode.Specific,
            Candidates = CreateAudioCandidates(audioTracks[audioTracks.SelectedIndex], audioTracks.SelectedIndex)
        };
    }

    private static SubtitlePreference CreateSubtitlePreference(PlaybackSubtitleTrackList subtitleTracks)
    {
        if (subtitleTracks.SelectedIndex < 0)
        {
            return new SubtitlePreference
            {
                Mode = SubtitleSelectionMode.Off
            };
        }

        return new SubtitlePreference
        {
            Mode = SubtitleSelectionMode.Specific,
            Candidates = CreateSubtitleCandidates(subtitleTracks[subtitleTracks.SelectedIndex], subtitleTracks.SelectedIndex)
        };
    }

    private static List<TrackCandidate> CreateAudioCandidates(MediaTrack track, int index)
    {
        List<TrackCandidate> candidates = new();
        AddLanguageCandidates(candidates, track, null);

        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(track.Label))
        {
            candidates.Add(new TrackCandidate { LabelEquals = track.Label });
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new TrackCandidate { Index = index });
        }

        return candidates;
    }

    private static List<TrackCandidate> CreateSubtitleCandidates(MediaTrack track, int index)
    {
        List<TrackCandidate> candidates = new();
        foreach (string hint in SubtitleLabelHints)
        {
            if (track.Label.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                AddLanguageCandidates(candidates, track, hint);
        }

        AddLanguageCandidates(candidates, track, null);

        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(track.Label))
        {
            candidates.Add(new TrackCandidate { LabelEquals = track.Label });
        }

        if (candidates.Count == 0)
        {
            candidates.Add(new TrackCandidate { Index = index });
        }

        return candidates;
    }

    private static void AddLanguageCandidates(List<TrackCandidate> candidates, MediaTrack track, string? labelContains)
    {
        if (!string.IsNullOrWhiteSpace(track.LanguageTag))
        {
            candidates.Add(new TrackCandidate
            {
                LanguageTag = track.LanguageTag,
                LabelContains = labelContains ?? string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(track.Language) &&
            !track.Language.Equals(track.LanguageTag, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(new TrackCandidate
            {
                LanguageName = track.Language,
                LabelContains = labelContains ?? string.Empty
            });
        }
    }

    private void RefreshTrackLists()
    {
        UpdateSubtitleTrackList();
        UpdateAudioTrackList();
        UpdateVideoTrackList();
        SubtitleTrackIndex = (ItemSubtitleTrackList?.SelectedIndex + 1) ?? 0;
        AudioTrackIndex = ItemAudioTrackList?.SelectedIndex ?? -1;
        VideoTrackIndex = ItemVideoTrackList?.SelectedIndex ?? -1;
    }

    private void NotifyProfileCommandCanExecuteChanged()
    {
        SaveFileTrackPreferenceCommand.NotifyCanExecuteChanged();
        SaveFolderTrackPreferenceCommand.NotifyCanExecuteChanged();
        ClearFileTrackPreferenceCommand.NotifyCanExecuteChanged();
        ClearFolderTrackPreferenceCommand.NotifyCanExecuteChanged();
    }
}
