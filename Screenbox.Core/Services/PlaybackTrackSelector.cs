#nullable enable

using System;
using System.Collections.Generic;
using Screenbox.Core.Helpers;
using Screenbox.Core.Models;
using Screenbox.Core.Playback;
using Windows.Globalization;

namespace Screenbox.Core.Services;

public sealed class PlaybackTrackSelector
{
    public void Apply(
        PlaybackAudioTrackList audioTracks,
        PlaybackSubtitleTrackList subtitleTracks,
        PlaybackTrackProfile? profile,
        string preferredLanguage)
    {
        if (profile == null)
        {
            ApplyDefault(audioTracks, subtitleTracks, preferredLanguage);
            return;
        }

        ApplyAudioPreference(audioTracks, profile.Audio);
        ApplySubtitlePreference(audioTracks, subtitleTracks, profile.Subtitle, preferredLanguage);
    }

    public int? FindAudioMatch(PlaybackAudioTrackList audioTracks, IReadOnlyList<TrackCandidate> candidates)
    {
        return FindMatch(audioTracks, candidates);
    }

    public int? FindSubtitleMatch(PlaybackSubtitleTrackList subtitleTracks, IReadOnlyList<TrackCandidate> candidates)
    {
        return FindMatch(subtitleTracks, candidates);
    }

    private void ApplyDefault(
        PlaybackAudioTrackList audioTracks,
        PlaybackSubtitleTrackList subtitleTracks,
        string preferredLanguage)
    {
        if (audioTracks.Count > 0)
        {
            audioTracks.SelectedIndex = 0;
        }

        if (audioTracks.SelectedIndex >= 0 &&
            audioTracks.SelectedIndex < audioTracks.Count &&
            TrackMatchesLanguage(audioTracks[audioTracks.SelectedIndex], preferredLanguage))
        {
            subtitleTracks.SelectedIndex = -1;
            return;
        }

        int? subtitleIndex = FindPreferredLanguageMatch(subtitleTracks, preferredLanguage);
        if (subtitleIndex.HasValue)
        {
            subtitleTracks.SelectedIndex = subtitleIndex.Value;
        }
        else if (subtitleTracks.SelectedIndex < 0)
        {
            subtitleTracks.SelectedIndex = -1;
        }
    }

    private void ApplyAudioPreference(PlaybackAudioTrackList audioTracks, TrackPreference? preference)
    {
        if (audioTracks.Count == 0) return;

        if (preference?.Mode == TrackSelectionMode.Specific)
        {
            int? matchedIndex = FindAudioMatch(audioTracks, preference.Candidates);
            audioTracks.SelectedIndex = matchedIndex ?? 0;
            return;
        }

        audioTracks.SelectedIndex = 0;
    }

    private void ApplySubtitlePreference(
        PlaybackAudioTrackList audioTracks,
        PlaybackSubtitleTrackList subtitleTracks,
        SubtitlePreference? preference,
        string preferredLanguage)
    {
        switch (preference?.Mode ?? SubtitleSelectionMode.Auto)
        {
            case SubtitleSelectionMode.Off:
                subtitleTracks.SelectedIndex = -1;
                return;
            case SubtitleSelectionMode.Specific when preference != null:
                subtitleTracks.SelectedIndex = FindSubtitleMatch(subtitleTracks, preference.Candidates) ?? -1;
                return;
            case SubtitleSelectionMode.Auto:
            default:
                ApplyDefaultSubtitle(audioTracks, subtitleTracks, preferredLanguage);
                return;
        }
    }

    private void ApplyDefaultSubtitle(
        PlaybackAudioTrackList audioTracks,
        PlaybackSubtitleTrackList subtitleTracks,
        string preferredLanguage)
    {
        if (audioTracks.SelectedIndex >= 0 &&
            audioTracks.SelectedIndex < audioTracks.Count &&
            TrackMatchesLanguage(audioTracks[audioTracks.SelectedIndex], preferredLanguage))
        {
            subtitleTracks.SelectedIndex = -1;
            return;
        }

        int? subtitleIndex = FindPreferredLanguageMatch(subtitleTracks, preferredLanguage);
        if (subtitleIndex.HasValue)
        {
            subtitleTracks.SelectedIndex = subtitleIndex.Value;
        }
        else if (subtitleTracks.SelectedIndex < 0)
        {
            subtitleTracks.SelectedIndex = -1;
        }
    }

    private static int? FindPreferredLanguageMatch<T>(IReadOnlyList<T> tracks, string preferredLanguage)
        where T : MediaTrack
    {
        if (string.IsNullOrWhiteSpace(preferredLanguage)) return null;

        for (int i = 0; i < tracks.Count; i++)
        {
            if (TrackMatchesLanguage(tracks[i], preferredLanguage))
                return i;
        }

        return null;
    }

    private static int? FindMatch<T>(IReadOnlyList<T> tracks, IReadOnlyList<TrackCandidate> candidates)
        where T : MediaTrack
    {
        foreach (TrackCandidate candidate in candidates)
        {
            for (int i = 0; i < tracks.Count; i++)
            {
                if (MatchesCandidate(tracks[i], i, candidate))
                    return i;
            }
        }

        return null;
    }

    private static bool MatchesCandidate(MediaTrack track, int index, TrackCandidate candidate)
    {
        if (candidate.Index.HasValue && candidate.Index.Value != index) return false;

        if (!string.IsNullOrWhiteSpace(candidate.LanguageTag) &&
            !TrackMatchesLanguage(track, candidate.LanguageTag))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.LanguageName) &&
            !candidate.LanguageName.Equals(track.Language, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.LabelContains) &&
            track.Label.IndexOf(candidate.LabelContains, StringComparison.CurrentCultureIgnoreCase) < 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(candidate.LabelEquals) &&
            !candidate.LabelEquals.Equals(track.Label, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool TrackMatchesLanguage(MediaTrack track, string language)
    {
        string requestedTag = LanguageHelper.NormalizeLanguageTag(language);
        string trackTag = LanguageHelper.NormalizeLanguageTag(track.LanguageTag);

        if (!string.IsNullOrEmpty(requestedTag) && !string.IsNullOrEmpty(trackTag))
        {
            return TagsMatch(trackTag, requestedTag);
        }

        if (!string.IsNullOrEmpty(requestedTag) &&
            new Language(requestedTag).DisplayName.Equals(track.Language, StringComparison.CurrentCultureIgnoreCase))
        {
            return true;
        }

        return language.Equals(track.Language, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool TagsMatch(string trackTag, string requestedTag)
    {
        if (trackTag.Equals(requestedTag, StringComparison.OrdinalIgnoreCase)) return true;

        string trackPrimary = GetPrimarySubtag(trackTag);
        string requestedPrimary = GetPrimarySubtag(requestedTag);
        return !string.IsNullOrEmpty(trackPrimary) &&
               trackPrimary.Equals(requestedPrimary, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetPrimarySubtag(string languageTag)
    {
        int separatorIndex = languageTag.IndexOf('-');
        return separatorIndex < 0 ? languageTag : languageTag.Substring(0, separatorIndex);
    }
}
