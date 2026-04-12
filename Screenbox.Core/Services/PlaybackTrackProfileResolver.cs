#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Screenbox.Core.Models;

namespace Screenbox.Core.Services;

public sealed class PlaybackTrackProfileResolver
{
    public PlaybackTrackProfile? GetEffectiveProfile(string mediaLocation, IEnumerable<PlaybackTrackProfile> profiles)
    {
        if (string.IsNullOrWhiteSpace(mediaLocation)) return null;

        PlaybackTrackProfile[] availableProfiles = profiles
            .Where(IsValidProfile)
            .ToArray();

        PlaybackTrackProfile? fileProfile = availableProfiles.FirstOrDefault(profile =>
            profile.Scope == PlaybackTrackScope.File &&
            profile.Location.Equals(mediaLocation, StringComparison.OrdinalIgnoreCase));

        if (fileProfile != null) return fileProfile;

        PlaybackTrackProfile? folderProfile = availableProfiles
            .Where(profile => profile.Scope == PlaybackTrackScope.Folder && IsMediaInFolder(mediaLocation, profile.Location))
            .OrderByDescending(profile => NormalizeFolderPath(profile.Location).Length)
            .FirstOrDefault();

        if (folderProfile != null) return folderProfile;

        return availableProfiles.FirstOrDefault(profile => profile.Scope == PlaybackTrackScope.Global);
    }

    private static bool IsValidProfile(PlaybackTrackProfile profile)
    {
        return profile.Scope == PlaybackTrackScope.Global || !string.IsNullOrWhiteSpace(profile.Location);
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
