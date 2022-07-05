using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpiixBot.Youtube.Models
{
    public class Video
    {
        public string Title { get; }
        public string Id { get; }
        public string Url { get; }
        public string ThumbnailUrl { get; }
        public string Duration { get; }
        public string StreamUrl { get; private set; }

        private int _intDuration = -1;

        private static readonly Regex _daysRegex = new Regex(@"(\d+)D");
        private static readonly Regex _hoursRegex = new Regex(@"(\d+)H");
        private static readonly Regex _minutesRegex = new Regex(@"(\d+)M");
        private static readonly Regex _secondsRegex = new Regex(@"(\d+)S");

        public Video(string title, string id, string thumbnailUrl, string duration)
        {
            Title = title;
            Id = id;
            Url = "https://www.youtube.com/watch?v=" + id;
            ThumbnailUrl = thumbnailUrl;
            Duration = duration;
        }

        public async Task<string> GetStreamUrlAsync(YoutubeClient client)
        {
            StreamUrl = await client.GetStreamUrlAsync(Id);
            return StreamUrl;
        }

        public int GetDurationInSeconds()
        {
            if (_intDuration != -1) return _intDuration;

            int duration = 0;

            string[] split = Duration.Split(':');

            for (int i = 0; i < split.Length; i++)
            {
                duration += Int32.Parse(split[i]) * (int)(Math.Pow(60, split.Length - i - 1));
            }

            _intDuration = duration;
            return duration;
        }

        public static string ParseDuration(string input)
        {
            // Examples:
            //   P8DT8H1S
            //   PT1H23M12S

            string days = "";
            string hours = "";
            string minutes = "";
            string seconds = "";

            if (input.Contains("D")) days = (_daysRegex.Match(input)).Groups[1].Value;
            if (input.Contains("H")) hours = (_hoursRegex.Match(input)).Groups[1].Value;
            if (input.Contains("M")) minutes = (_minutesRegex.Match(input)).Groups[1].Value;
            if (input.Contains("S")) seconds = (_secondsRegex.Match(input)).Groups[1].Value;

            int intHours = 0;
            int intMinutes = 0;
            int intSeconds = 0;

            if (days != "") intHours += Int32.Parse(days) * 60;
            if (hours != "") intHours += Int32.Parse(hours);
            if (minutes != "") intMinutes += Int32.Parse(minutes);
            if (seconds != "") intSeconds += Int32.Parse(seconds);

            string duration = "";
            if (intHours > 0) duration += $"{intHours}:";

            if (intMinutes >= 10) duration += $"{intMinutes}:";
            else duration += $"0{intMinutes}:";

            if (intSeconds >= 10) duration += intSeconds.ToString();
            else duration += $"0{intSeconds}";

            return duration;
        }
    }
}
