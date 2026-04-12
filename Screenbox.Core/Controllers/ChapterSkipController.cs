#nullable enable

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using CommunityToolkit.WinUI;
using Screenbox.Core.Contexts;
using Screenbox.Core.Events;
using Screenbox.Core.Messages;
using Screenbox.Core.Models;
using Screenbox.Core.Playback;
using Screenbox.Core.Services;
using Screenbox.Core.ViewModels;
using Windows.Media.Core;
using Windows.System;

namespace Screenbox.Core.Controllers;

public sealed class ChapterSkipController : ObservableRecipient,
    IRecipient<PropertyChangedMessage<IMediaPlayer?>>,
    IRecipient<PlaylistCurrentItemChangedMessage>
{
    private static readonly TimeSpan EvaluationDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan EndBoundaryTolerance = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan RecentSkipWindow = TimeSpan.FromSeconds(1);

    private readonly PlayerContext _playerContext;
    private readonly IChapterSkipStore _chapterSkipStore;
    private readonly ChapterSkipResolver _chapterSkipResolver;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly DispatcherQueueTimer _evaluationTimer;
    private MediaViewModel? _currentItem;
    private SkipMarker? _lastSkip;
    private bool _isEvaluating;
    private bool _evaluateAgain;

    private IMediaPlayer? MediaPlayer => _playerContext.MediaPlayer;

    public ChapterSkipController(
        PlayerContext playerContext,
        IChapterSkipStore chapterSkipStore,
        ChapterSkipResolver chapterSkipResolver)
    {
        _playerContext = playerContext;
        _chapterSkipStore = chapterSkipStore;
        _chapterSkipResolver = chapterSkipResolver;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _evaluationTimer = _dispatcherQueue.CreateTimer();

        _chapterSkipStore.ProfilesChanged += OnProfilesChanged;

        if (MediaPlayer != null)
            RegisterPlayer(MediaPlayer);

        IsActive = true;
        _ = _chapterSkipStore.LoadAsync();
    }

    public void Receive(PropertyChangedMessage<IMediaPlayer?> message)
    {
        if (message.Sender is not PlayerContext) return;

        if (message.OldValue != null)
            UnregisterPlayer(message.OldValue);

        if (message.NewValue != null)
            RegisterPlayer(message.NewValue);

        ResetSkipState();
        QueueEvaluation();
    }

    public void Receive(PlaylistCurrentItemChangedMessage message)
    {
        _currentItem = message.Value;
        ResetSkipState();
        QueueEvaluation();
    }

    private void RegisterPlayer(IMediaPlayer player)
    {
        player.PlaybackItemChanged += OnPlaybackItemChanged;
        player.NaturalDurationChanged += OnNaturalDurationChanged;
        player.ChapterChanged += OnChapterChanged;
        player.PositionChanged += OnPositionChanged;
    }

    private void UnregisterPlayer(IMediaPlayer player)
    {
        player.PlaybackItemChanged -= OnPlaybackItemChanged;
        player.NaturalDurationChanged -= OnNaturalDurationChanged;
        player.ChapterChanged -= OnChapterChanged;
        player.PositionChanged -= OnPositionChanged;
    }

    private void OnProfilesChanged(object? sender, EventArgs e)
    {
        ResetSkipState();
        QueueEvaluation();
    }

    private void OnPlaybackItemChanged(IMediaPlayer sender, ValueChangedEventArgs<PlaybackItem?> args)
    {
        ResetSkipState();
        QueueEvaluation();
    }

    private void OnNaturalDurationChanged(IMediaPlayer sender, ValueChangedEventArgs<TimeSpan> args)
    {
        QueueEvaluation();
    }

    private void OnChapterChanged(IMediaPlayer sender, ValueChangedEventArgs<ChapterCue?> args)
    {
        QueueEvaluation();
    }

    private void OnPositionChanged(IMediaPlayer sender, ValueChangedEventArgs<TimeSpan> args)
    {
        QueueEvaluation();
    }

    private void QueueEvaluation()
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            _evaluationTimer.Debounce(() => _ = EvaluateAsync(), EvaluationDelay);
            return;
        }

        _dispatcherQueue.TryEnqueue(() => _evaluationTimer.Debounce(() => _ = EvaluateAsync(), EvaluationDelay));
    }

    private async System.Threading.Tasks.Task EvaluateAsync()
    {
        if (_isEvaluating)
        {
            _evaluateAgain = true;
            return;
        }

        try
        {
            _isEvaluating = true;
            do
            {
                _evaluateAgain = false;
                await EvaluateOnceAsync();
            } while (_evaluateAgain);
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    private async System.Threading.Tasks.Task EvaluateOnceAsync()
    {
        IMediaPlayer? player = MediaPlayer;
        MediaViewModel? currentItem = _currentItem;
        if (player?.PlaybackItem == null || currentItem == null) return;
        if (!player.CanSeek || player.NaturalDuration <= TimeSpan.Zero) return;

        await _chapterSkipStore.LoadAsync();

        PlaybackChapterList chapters = player.PlaybackItem.Chapters;
        chapters.EnsureLoaded(player);
        if (chapters.Count == 0) return;

        TimeSpan position = player.Position;
        foreach (ChapterSkipRange range in _chapterSkipResolver.GetSkipRanges(
                     currentItem.Location,
                     chapters,
                     _chapterSkipStore.Profiles,
                     player.NaturalDuration))
        {
            if (!ShouldSkip(position, range)) continue;
            SkipRange(player, currentItem.Location, range, position);
            return;
        }

        if (_lastSkip != null && DateTimeOffset.Now - _lastSkip.AppliedAt > RecentSkipWindow)
            _lastSkip = null;
    }

    private void SkipRange(IMediaPlayer player, string mediaLocation, ChapterSkipRange range, TimeSpan position)
    {
        if (IsRecentSkip(mediaLocation, range)) return;

        TimeSpan targetPosition = range.EndTime > player.NaturalDuration ? player.NaturalDuration : range.EndTime;
        if (targetPosition <= position) return;

        _lastSkip = new SkipMarker(mediaLocation, range.ChapterIndex, range.StartTime, range.EndTime, DateTimeOffset.Now);
        player.Position = targetPosition;
    }

    private bool IsRecentSkip(string mediaLocation, ChapterSkipRange range)
    {
        return _lastSkip is { } lastSkip &&
               lastSkip.MediaLocation.Equals(mediaLocation, StringComparison.OrdinalIgnoreCase) &&
               lastSkip.ChapterIndex == range.ChapterIndex &&
               lastSkip.StartTime == range.StartTime &&
               lastSkip.EndTime == range.EndTime &&
               DateTimeOffset.Now - lastSkip.AppliedAt < RecentSkipWindow;
    }

    private static bool ShouldSkip(TimeSpan position, ChapterSkipRange range)
    {
        return position >= range.StartTime &&
               position < range.EndTime - EndBoundaryTolerance;
    }

    private void ResetSkipState()
    {
        _lastSkip = null;
    }

    private sealed class SkipMarker
    {
        public string MediaLocation { get; }

        public int ChapterIndex { get; }

        public TimeSpan StartTime { get; }

        public TimeSpan EndTime { get; }

        public DateTimeOffset AppliedAt { get; }

        public SkipMarker(
            string mediaLocation,
            int chapterIndex,
            TimeSpan startTime,
            TimeSpan endTime,
            DateTimeOffset appliedAt)
        {
            MediaLocation = mediaLocation;
            ChapterIndex = chapterIndex;
            StartTime = startTime;
            EndTime = endTime;
            AppliedAt = appliedAt;
        }
    }
}
