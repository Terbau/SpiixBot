using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Spotify.Models
{
    public class Playlist
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Url => $"https://open.spotify.com/playlist/{Id}";
        public List<Song> Songs { get; set; }

        public int GetCombinedDuration()
        {
            int duration = 0;

            foreach (Song song in Songs)
            {
                duration += song.Duration;
            }

            return duration;
        }
    }
}
