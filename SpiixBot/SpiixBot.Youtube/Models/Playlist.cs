using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Youtube.Models
{
    public class Playlist
    {
        public List<Video> Videos { get; }
        public string Id { get; internal set; }
        public string Title { get; internal set; } = "N/A";
        public string Url => $"https://www.youtube.com/playlist?list={Id}";

        public Playlist(List<Video> videos)
        {
            Videos = videos;
        }

        public int GetCombinedDuration()
        {
            int duration = 0;

            foreach (Video video in Videos)
            {
                duration += video.GetDurationInSeconds();
            }

            return duration;
        }
    }
}
