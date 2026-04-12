using LibVLCSharp.Shared.Structures;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Media.Core;

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

        public void Load(IMediaPlayer player)
        {
            if (player is not VlcMediaPlayer vlcPlayer || player.PlaybackItem != _item)
                return;

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

            vlcPlayer.Chapter = _chapters.FirstOrDefault();
            IsLoaded = true;
        }

        public void EnsureLoaded(IMediaPlayer player)
        {
            if (IsLoaded) return;
            Load(player);
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
