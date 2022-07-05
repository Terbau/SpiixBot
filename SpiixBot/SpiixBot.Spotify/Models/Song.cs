using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Spotify.Models
{
    public class Song
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string Artist { get; set; }
        public string ThumbnailUrl { get; set; }
        public bool Explicit { get; set; }
        public int DurationMs { get; set; }
        public int Duration => DurationMs / 1000;
        public string Uri => $"spotify:track:{Id}";
        public string ExternalUrl => $"https://open.spotify.com/track/{Id}";
    }
}
