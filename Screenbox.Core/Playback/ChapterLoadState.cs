#nullable enable

using System;

namespace Screenbox.Core.Playback
{
    internal static class ChapterLoadState
    {
        public static bool CanReadChapters(bool isCurrentPlaybackItem, bool isOpening, TimeSpan naturalDuration)
        {
            return isCurrentPlaybackItem &&
                   !isOpening &&
                   naturalDuration > TimeSpan.Zero;
        }
    }
}
