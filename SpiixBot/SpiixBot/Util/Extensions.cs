using Newtonsoft.Json.Linq;
using RestSharp;
using SpiixBot.Spotify.Models;
using SpiixBot.Youtube.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Victoria;

namespace SpiixBot.Util
{
    public class LyricsNotFoundException : Exception
    {

    }

    public static class Extensions
    {
        private static readonly Regex ParanReg
            = new Regex(@"(\(.*?\))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ArtistReg
            = new Regex(@"\w+.\w+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static async Task<string> FetchGeniusLyricsUrl(this LavaTrack track)
        {
            return await FetchGeniusLyricsUrl(track.Title);
        }

        public static async Task<string> FetchGeniusLyricsUrl(this Video video)
        {
            return await FetchGeniusLyricsUrl(video.Title);
        }

        public static async Task<string> FetchGeniusLyricsUrl(this Song song)
        {
            return await FetchGeniusLyricsUrl(song.Artist, song.Name);
        }

        public static void Shuffle<T>(this Random rng, T[] array)
        {
            int n = array.Length;
            while (n > 1)
            {
                int k = rng.Next(n--);
                T temp = array[n];
                array[n] = array[k];
                array[k] = temp;
            }
        }

        public static async Task<string> FetchGeniusLyricsUrl(string input)
        {
            try
            {
                var (artist, title) = GetArtistAndTitleBetter(input);
                return await FetchGeniusLyricsUrl(artist, title);
            }
            catch
            {
                throw new LyricsNotFoundException();
            }
        }

        public static async Task<string> FetchGeniusLyricsUrl(string artist, string title)
        {
            RestClient client = new RestClient("https://genius.com");
            RestRequest request = new RestRequest("/api/search/multi", Method.Get);

            request.AddQueryParameter("q", $"{artist}+{title}".Replace(' ', '+'));

            RestResponse response = await client.ExecuteAsync(request);
            JObject data = JObject.Parse(response.Content);

            string path = "";

            foreach (JToken token in data.SelectToken("response.sections"))
            {
                if ((string)token.SelectToken("type") == "song")
                {
                    JArray array = (JArray)token.SelectToken("hits");
                    if (array.Count == 0) throw new LyricsNotFoundException();

                    path = (string)token.SelectToken("hits[0].result.path");
                    break;
                }
            }

            if (path == "") throw new LyricsNotFoundException();

            return $"https://genius.com{path}";
        }

        internal static (string Artist, string Title) GetArtistAndTitleBetter(string input)
        {
            Console.WriteLine(input);
            string[] split = input.Split(" - ", 2);
            Console.WriteLine(split.Length);

            if (split.Length != 2) throw new Exception("First");

            string t = split[1];

            int idx1 = t.ToLower().IndexOf("ft.");
            if (idx1 != -1) t = t.Substring(0, idx1);

            int idx2 = t.ToLower().IndexOf("feat.");
            if (idx2 != -1) t = t.Substring(0, idx2);

            int idx3 = t.ToLower().IndexOf("ft ");
            if (idx3 != -1) t = t.Substring(0, idx3);

            int idx4 = t.ToLower().IndexOf("feat");
            if (idx4 != -1) t = t.Substring(0, idx4);

            string text = t;
            char[] chars = "()[]{}".ToCharArray();
            foreach (char l in t)
            {
                if (chars.Contains(l))
                {
                    text = t.Substring(0, t.IndexOf(l));
                    break;
                }
            }

            return (split[0].Trim(), text.Trim());
        }

        internal static (string Artist, string Title) GetArtistAndTitle(LavaTrack lavaTrack)
        {
            var title = ParanReg.Replace(lavaTrack.Title, string.Empty);
            title = title.Replace("&", "and");
            var titleSplit = title.Split('-');

            var artistSplit = lavaTrack.Author.Split('-');

            if (titleSplit.Length == 1 && artistSplit.Length > 1)
            {
                return (artistSplit[0].Trim(), title.Trim());
            }

            var artist = ArtistReg.Match(titleSplit[0]).Value;
            if (artist.Equals(titleSplit[0], StringComparison.OrdinalIgnoreCase) ||
                artist.Equals(lavaTrack.Author, StringComparison.OrdinalIgnoreCase))
            {
                return (titleSplit[0].Trim(), titleSplit[1].Trim());
            }

            return (artist, titleSplit[1].Trim());
        }
    }
}
