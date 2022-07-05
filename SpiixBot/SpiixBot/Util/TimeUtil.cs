using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util
{
    public static class TimeUtil
    {
        public static string ToReadableTime(int inpSeconds)
        {
            int hours = inpSeconds / (60 * 60);
            int remainder = inpSeconds % (60 * 60);
            int minutes = remainder / 60;
            remainder = remainder % 60;
            int seconds = remainder;

            string fmt = "";
            if (hours > 0) fmt += $"{hours}:";

            fmt += $"{(minutes < 10 ? "0" + minutes.ToString() : minutes.ToString())}:{(seconds < 10 ? "0" + seconds.ToString() : seconds.ToString())}";

            return fmt;
        }

        public static string ToSpotifyReadableTime(int inpSeconds)
        {
            int hours = inpSeconds / (60 * 60);
            int remainder = inpSeconds % (60 * 60);
            int minutes = remainder / 60;
            remainder = remainder % 60;
            int seconds = remainder;

            var parts = new List<string>();

            if (hours > 0) parts.Add($"{hours} hr");
            parts.Add($"{minutes} min");

            return string.Join(' ', parts);
        }

        public static string ToFullReadableTime(int inpSeconds)
        {
            int days = inpSeconds / (24 * 60 * 60);
            int remainder = inpSeconds % (24 * 60 * 60);
            int hours = remainder / (60 * 60);
            remainder = remainder % (60 * 60);
            int minutes = remainder / 60;
            remainder = remainder % 60;
            int seconds = remainder;

            var parts = new List<string>();

            if (days > 0) parts.Add($"{days} d");
            if (hours > 0) parts.Add($"{hours} h");
            parts.Add($"{minutes} m");

            return string.Join(' ', parts);
        }

        public static string ToReadableAgoTime(int inpSeconds)
        {
            int hours = inpSeconds / (60 * 60);
            int remainder = inpSeconds % (60 * 60);
            int minutes = remainder / 60;
            remainder = remainder % 60;
            int seconds = remainder;

            var parts = new List<string>();

            if (hours > 0) parts.Add($"{hours}h");
            if (minutes > 0) parts.Add($"{minutes}m");
            if (parts.Count == 0 || seconds > 0) parts.Add($"{seconds}s");

            return string.Join(' ', parts) + " ago";
        }
    }
}
