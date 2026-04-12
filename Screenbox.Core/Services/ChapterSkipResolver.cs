#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Screenbox.Core.Models;
using Windows.Media.Core;

namespace Screenbox.Core.Services;

public sealed class ChapterSkipResolver
{
    public ChapterSkipProfile? GetEffectiveProfile(string mediaLocation, IEnumerable<ChapterSkipProfile> profiles)
    {
        if (string.IsNullOrWhiteSpace(mediaLocation)) return null;

        ChapterSkipProfile[] availableProfiles = profiles
            .Where(profile => profile.Rules.Any(rule => rule.IsEnabled))
            .ToArray();

        ChapterSkipProfile? fileProfile = availableProfiles.FirstOrDefault(profile =>
            profile.Scope == ChapterSkipScope.File &&
            profile.Location.Equals(mediaLocation, StringComparison.OrdinalIgnoreCase));

        if (fileProfile != null) return fileProfile;

        ChapterSkipProfile? folderProfile = availableProfiles
            .Where(profile => profile.Scope == ChapterSkipScope.Folder && IsMediaInFolder(mediaLocation, profile.Location))
            .OrderByDescending(profile => NormalizeFolderPath(profile.Location).Length)
            .FirstOrDefault();

        if (folderProfile != null) return folderProfile;

        return availableProfiles.FirstOrDefault(profile => profile.Scope == ChapterSkipScope.Global);
    }

    public IReadOnlyList<ChapterSkipRange> GetSkipRanges(
        string mediaLocation,
        IReadOnlyList<ChapterCue> chapters,
        IEnumerable<ChapterSkipProfile> profiles,
        TimeSpan naturalDuration)
    {
        ChapterSkipProfile? profile = GetEffectiveProfile(mediaLocation, profiles);
        if (profile == null || chapters.Count == 0) return Array.Empty<ChapterSkipRange>();

        List<ChapterSkipRange> ranges = new();
        for (int chapterIndex = 0; chapterIndex < chapters.Count; chapterIndex++)
        {
            ChapterCue chapter = chapters[chapterIndex];
            TimeSpan endTime = GetChapterEndTime(chapters, chapterIndex, naturalDuration);
            if (endTime <= chapter.StartTime) continue;

            foreach (ChapterSkipRule rule in profile.Rules)
            {
                if (!IsMatch(rule, chapter, chapterIndex)) continue;
                ranges.Add(new ChapterSkipRange(chapterIndex, chapter.StartTime, endTime, rule));
                break;
            }
        }

        return ranges;
    }

    private static bool IsMatch(ChapterSkipRule rule, ChapterCue chapter, int chapterIndex)
    {
        if (!rule.IsEnabled) return false;

        return rule.MatchMode switch
        {
            ChapterMatchMode.Index => rule.Index == chapterIndex,
            ChapterMatchMode.TitleEquals => chapter.Title.Trim().Equals(rule.Pattern.Trim(), StringComparison.OrdinalIgnoreCase),
            ChapterMatchMode.TitleContains => chapter.Title.IndexOf(rule.Pattern, StringComparison.OrdinalIgnoreCase) >= 0,
            ChapterMatchMode.TitleRegex => IsRegexMatch(rule.Pattern, chapter.Title),
            _ => false
        };
    }

    private static bool IsRegexMatch(string pattern, string value)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        try
        {
            return Regex.IsMatch(value, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static TimeSpan GetChapterEndTime(IReadOnlyList<ChapterCue> chapters, int chapterIndex, TimeSpan naturalDuration)
    {
        ChapterCue chapter = chapters[chapterIndex];
        if (chapter.Duration > TimeSpan.Zero)
            return chapter.StartTime + chapter.Duration;

        if (chapterIndex + 1 < chapters.Count && chapters[chapterIndex + 1].StartTime > chapter.StartTime)
            return chapters[chapterIndex + 1].StartTime;

        return naturalDuration;
    }

    private static bool IsMediaInFolder(string mediaLocation, string folderLocation)
    {
        string folder = NormalizeFolderPath(folderLocation);
        if (string.IsNullOrEmpty(folder)) return false;

        string media = NormalizePath(mediaLocation);
        if (!media.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return false;
        if (media.Length == folder.Length) return true;

        return media[folder.Length] is '\\' or '/';
    }

    private static string NormalizeFolderPath(string path)
    {
        return NormalizePath(path).TrimEnd('\\', '/');
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return path.Trim();
        }
    }
}
