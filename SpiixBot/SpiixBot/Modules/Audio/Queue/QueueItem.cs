using Discord.WebSocket;
using SpiixBot.Spotify.Models;
using SpiixBot.Youtube.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class QueueItem
    {
        public Video VideoInfo { get; set; }
        public Song SongInfo { get; set; }
        public string StreamUrl { get; set; }
        public ItemProvider Provider { get; set; }
        public bool IsPlaylistItem { get; set; } = false;
        public int SeekPosition { get; set; } = 0;
        public bool HasDummyVideoInfo { get; set; } = false;
        public bool IsBroken { get; set; } = false;
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public SocketGuildUser User { get; set; }

        public string Title => GetTitle();
        public int Duration => GetDuration();
        public string Url => GetUrl();

        private string GetTitle()
        {
            switch (Provider)
            {
                case ItemProvider.Spotify:
                    return SongInfo.Name;

                default:
                    return VideoInfo.Title;
            }
        }

        private int GetDuration()
        {
            switch (Provider)
            {
                case ItemProvider.Spotify:
                    return SongInfo.Duration;

                default:
                    return VideoInfo.GetDurationInSeconds();
            }
        }

        private string GetUrl()
        {
            switch (Provider)
            {
                case ItemProvider.Spotify:
                    return SongInfo.ExternalUrl;

                default:
                    return VideoInfo.Url;
            }
        }
    }
}
