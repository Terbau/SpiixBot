using Newtonsoft.Json;
using RestSharp;
using SpiixBot.Spotify.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace SpiixBot.Spotify
{
    public class SpotifyClient
    {
        private readonly string _clientId;
        private readonly string _secret;

        private readonly RestClient _accountClient = new RestClient("https://accounts.spotify.com/api");
        private readonly RestClient _apiClient = new RestClient("https://api.spotify.com/v1");

        private string _accessToken;
        private DateTime _authExpiresAt = DateTime.MinValue;

        public SpotifyClient(string clientId, string secret)
        {
            _clientId = clientId;
            _secret = secret;
        }

        private async Task PerformClientCredentialsAsync()
        {
            Console.WriteLine(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_secret}")));
            var request = new RestRequest("/token", Method.Post)
                .AddHeader("Content-Type", "application/x-www-form-urlencoded")
                .AddHeader("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_clientId}:{_secret}")))
                .AddParameter("grant_type", "client_credentials");

            RestResponse response = await _accountClient.ExecuteAsync(request);
            var data = JObject.Parse(response.Content);

            _accessToken = (string)data.SelectToken("access_token");
            _authExpiresAt = DateTime.UtcNow.AddSeconds((int)data.SelectToken("expires_in"));
        }

        private async Task<RestResponse> PerformRequest(RestRequest request, bool isRecursive = false)
        {
            if (DateTime.UtcNow > _authExpiresAt) await PerformClientCredentialsAsync();

            request.AddHeader("Authorization", $"Bearer {_accessToken}");

            RestResponse response = await _apiClient.ExecuteAsync(request);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            {
                if (isRecursive) throw new Exception("Could not authenticate spotify");
                return await PerformRequest(request, isRecursive: true);
            }

            return response;
        }

        public async Task<List<Song>> SearchSongsAsync(string query, int limit = -1)
        {
            var request = new RestRequest("/search")
                .AddQueryParameter("q", query)
                .AddQueryParameter("type", "track")
                .AddQueryParameter("limit", Math.Min(50, limit == -1 ? 50 : limit).ToString());
            RestResponse response = await PerformRequest(request);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine("Not found");
            }
            else if ((int)response.StatusCode == 429)
            {
                Console.WriteLine("Was rate limited");
            }

            var data = JObject.Parse(response.Content);

            List<Song> songs = new List<Song>();
            foreach (JToken songData in data.SelectToken("tracks.items"))
            {
                var song = new Song
                {
                    Name = (string)songData.SelectToken("name"),
                    Id = (string)songData.SelectToken("id"),
                    Artist = string.Join(", ", songData.SelectToken("artists").Select(artistToken => (string)artistToken.SelectToken("name"))),
                    ThumbnailUrl = (string)songData.SelectToken("album.images[0].url"),
                    DurationMs = (int)songData.SelectToken("duration_ms"),
                    Explicit = (bool)songData.SelectToken("explicit"),
                };
                songs.Add(song);
            }

            if (songs.Count == 0) throw new NotFoundException();

            return songs;
        }

        private Song CreateSongObject(JToken token)
        {
            return new Song
            {
                Name = (string)token.SelectToken("name"),
                Id = (string)token.SelectToken("id"),
                Artist = string.Join(", ", token.SelectToken("artists").Select(artistToken => (string)artistToken.SelectToken("name"))),
                ThumbnailUrl = (string)token.SelectToken("album.images[0].url"),
                DurationMs = (int)token.SelectToken("duration_ms"),
                Explicit = (bool)token.SelectToken("explicit"),
            };
        }

        public async Task<List<Song>> GetSongsAsync(IEnumerable<string> songIds)
        {
            var request = new RestRequest("/tracks")
                .AddQueryParameter("ids", string.Join(",", songIds));
            RestResponse response = await PerformRequest(request);

            var data = JObject.Parse(response.Content);

            List<Song> songs = new List<Song>();
            foreach (JToken songData in data.SelectToken("tracks"))
            {
                if (songData == null || string.IsNullOrEmpty(songData.ToString())) continue;

                var song = CreateSongObject(songData);
                songs.Add(song);
            }

            return songs;
        }

        public async Task<Song> GetSongAsync(string songId)
        {
            List<Song> songs = await GetSongsAsync(new string[1] { songId });
            if (songs.Count == 0) throw new NotFoundException();

            return songs[0];
        }

        private async Task<IEnumerable<Song>> GetPlaylistPartAsync(string playlistId, int offset = 0)
        {
            var request = new RestRequest($"/playlists/{playlistId}/tracks")
                .AddQueryParameter("fields", "items.track(name, explicit, id, duration_ms, artists.name, album(images))")
                .AddQueryParameter("limit", "100")
                .AddQueryParameter("offset", offset.ToString());
            RestResponse response = await PerformRequest(request);

            var data = JObject.Parse(response.Content);
            return data.SelectToken("items").Select(token => CreateSongObject(token.SelectToken("track")));
        }

        public async Task<Playlist> GetPlaylistAsync(string playlistId)
        {
            var request = new RestRequest($"/playlists/{playlistId}")
                .AddQueryParameter("fields", "name, images, tracks(total)");
            RestResponse response = await PerformRequest(request);

            var data = JObject.Parse(response.Content);

            int total = (int)data.SelectToken("tracks.total");
            var songs = new List<Song>();

            if (total != 0)
            {
                int totalRequests = (int)Math.Ceiling(total / 100f);
                Console.WriteLine(totalRequests);

                Task[] tasks = new Task<IEnumerable<Song>>[totalRequests];
                for (int i = 0; i < totalRequests; i++)
                {
                    int offset = i * 100;
                    Task task = GetPlaylistPartAsync(playlistId, offset: offset);
                    tasks[i] = task;
                }

                await Task.WhenAll(tasks);
                foreach (Task<IEnumerable<Song>> task in tasks)
                {
                    IEnumerable<Song> enumerableSongs = task.Result;
                    songs.AddRange(enumerableSongs);
                }
            }
           
            return new Playlist
            {
                Name = (string)data.SelectToken("name"),
                Id = playlistId,
                ThumbnailUrl = (string)data.SelectToken("images[0].url"),
                Songs = songs,
            };
        }
    }
}
