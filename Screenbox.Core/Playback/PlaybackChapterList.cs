using LibVLCSharp.Shared.Structures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Screenbox.Core.Playback
{
    public sealed class PlaybackChapterList : ReadOnlyCollection<ChapterCue>
    {
        private readonly List<ChapterCue> _chapters;
        private readonly PlaybackItem _item;

        public bool IsLoaded { get; private set; }

        internal PlaybackChapterList(PlaybackItem item) : base(new List<ChapterCue>())
        {
            _item = item;
            _chapters = (List<ChapterCue>)Items;
        }

        public bool TryLoad(IMediaPlayer player)
        {
            if (IsLoaded) return true;
            return Load(player);
        }

        public bool Load(IMediaPlayer player)
        {
            if (player is not VlcMediaPlayer vlcPlayer)
                return false;

            if (!CanReadChapters(vlcPlayer))
                return false;

            if (vlcPlayer.VlcPlayer.ChapterCount > 0)
            {
                List<ChapterDescription> chapterDescriptions = new();
                for (int i = 0; i < vlcPlayer.VlcPlayer.TitleCount; i++)
                {
                    chapterDescriptions.AddRange(vlcPlayer.VlcPlayer.FullChapterDescriptions(i));
                }

                Load(chapterDescriptions);
            }
            else
            {
                Load(vlcPlayer.VlcPlayer.FullChapterDescriptions());
            }

            IsLoaded = true;
            vlcPlayer.Chapter = _chapters.FirstOrDefault();
            return true;
        }

        private bool CanReadChapters(VlcMediaPlayer player)
        {
            return ChapterLoadState.CanReadChapters(
                player.PlaybackItem == _item,
                player.PlaybackState == MediaPlaybackState.Opening,
                player.NaturalDuration);
        }

        private void Load(IEnumerable<ChapterDescription> vlcChapters)
        {
            IEnumerable<ChapterCue> chapterCues = vlcChapters.Select(c => new ChapterCue
            {
                Title = c.Name ?? string.Empty,
                Duration = TimeSpan.FromMilliseconds(c.Duration),
                StartTime = TimeSpan.FromMilliseconds(c.TimeOffset)
            });

            _chapters.Clear();
            _chapters.AddRange(chapterCues);
        }
    }
}
