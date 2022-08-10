using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using RestSharp.Serializers;
using SpiixBot.Youtube;
using SpiixBot.Youtube.Constants;
using SpiixBot.Youtube.Models;
using SpiixBot.Youtube.SignatureScrambler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SpiixBot.Youtube
{
    public class YoutubeClient
    {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.101 Safari/537.36";

        private readonly string _personalApiKey;
        private readonly RestClient _youtubeRestClient = new RestClient("https://www.youtube.com/");
        private readonly RestClient _googleApisRestClient = new RestClient("https://www.googleapis.com/");
        private readonly Regex _streamUrlJsonPattern = new Regex(@"ytInitialPlayerResponse = (\{.*\})\;(?:(?:var meta)|(?:<\/script>))", RegexOptions.Compiled);
        private Unscrambler _signatureUnscrambler = null;
        private readonly Dictionary<string, int> qualityWeights = new Dictionary<string, int>()
        {
            { "AUDIO_QUALITY_LOW", 3 },
            { "AUDIO_QUALITY_MEDIUM", 2 },
            { "AUDIO_QUALITY_HIGH", 1 }
        };

        public YoutubeClient(string apiKey)
        {
            _personalApiKey = apiKey;
        }

        private async Task CreateSignatureUnscramblerAsync(string playerUrlPart)
        {
            var request = new RestRequest(playerUrlPart, Method.Get);

            RestResponse response = await _youtubeRestClient.ExecuteAsync(request);

            _signatureUnscrambler = new Unscrambler(response.Content);
        }

        public string UnscrambleSignature(string sig)
        {
            return _signatureUnscrambler.Unscramble(sig);
        }

        public async Task<string> GetStreamUrlAsync(string videoId)
        {
            var request = new RestRequest("watch", Method.Get);

            request.AddQueryParameter("v", videoId);
            request.AddQueryParameter("bpctr", "9999999999");
            request.AddQueryParameter("has_verified", "1");

            RestResponse response = await _youtubeRestClient.ExecuteAsync(request);

            JObject obj = JObject.Parse(_streamUrlJsonPattern.Match(response.Content).Groups[1].Value);

            List<JToken> validTokens = new List<JToken>();

            foreach (JToken token in obj.SelectToken("streamingData.adaptiveFormats"))
            {
                string mimeType = (string)token.SelectToken("mimeType");
                if (!mimeType.Contains("audio")) continue;

                JToken signatureCipher = token.SelectToken("signatureCipher");
                if (signatureCipher == null) continue;

                validTokens.Add(token);
            }

            validTokens.OrderBy(o => qualityWeights[(string)o.SelectToken("audioQuality")]).ToList();
            if (!validTokens.Any()) throw new NotFoundException();

            string rawCipher = Uri.UnescapeDataString((string)validTokens.First().SelectToken("signatureCipher"));

            string[] split = rawCipher.Split(new string[] { "&url=" }, StringSplitOptions.None);
            string url = split[1];

            string[] split2 = split[0].Split(new string[] { "&sp=" }, StringSplitOptions.None);
            string paramKey = split2[1];

            if (_signatureUnscrambler == null)
            {
                string playerUrlPart = Regex.Match(response.Content, @".(?:PLAYER_JS_URL|jsUrl).\s*:\s*.([^""]+).").Groups[1].Value;
                await CreateSignatureUnscramblerAsync(playerUrlPart);
            }

            string scrambledSignature = split2[0].Substring(2);
            string decodedSig = _signatureUnscrambler.Unscramble(scrambledSignature);

            return $"{url}&{paramKey}={decodedSig}";
        }

        public async Task<List<Video>> SearchVideosAsync(string query, int limit = -1)
        {
            var request = new RestRequest("youtubei/v1/search", Method.Post);

            request.AddQueryParameter("key", SearchConstants.Key);
            request.AddHeader("User-Agent", UserAgent);

            string payload = SearchConstants.Payload.Replace("{0}", query.Replace("\"", "\\\""));
            request.AddStringBody(payload, ContentType.Json);

            RestResponse response = await _youtubeRestClient.ExecuteAsync(request);
            JObject obj = JObject.Parse(response.Content);

            string maybeErrorMessage = (string)obj.SelectToken("contents.twoColumnSearchResultsRenderer.primaryContents.sectionListRenderer.contents[0].itemSectionRenderer.contents[0].backgroundPromoRenderer.title.runs[0].text");
            if (maybeErrorMessage == "No results found") throw new NotFoundException();

            JToken contents = obj.SelectToken("contents.twoColumnSearchResultsRenderer.primaryContents.sectionListRenderer.contents[0].itemSectionRenderer.contents");

            var videos = new List<Video>();

            foreach (JToken element in contents)
            {
                JToken vc = element["videoRenderer"];
                if (vc == null) continue;

                string title = (string)vc.SelectToken("title.runs[0].text");
                string id = (string)vc.SelectToken("videoId");
                string thumbnailUrl = (string)vc.SelectToken("channelThumbnailSupportedRenderers.channelThumbnailWithLinkRenderer.thumbnail.thumbnails[0].url");
                string duration = (string)vc.SelectToken("lengthText.simpleText");

                var video = new Video(title, id, thumbnailUrl, duration);
                videos.Add(video);

                if (videos.Count == limit) break;
            }

            if (videos.Count == 0) throw new NotFoundException();

            return videos;
        }

        private async Task<RestResponse> GetPlaylistPartByIdAsync2(string playlistId, string continuationToken = null)
        {
            var request = new RestRequest("youtubei/v1/browse", Method.Post);

            request.AddQueryParameter("key", BrowseConstants.Key);
            request.AddHeader("User-Agent", UserAgent);

            string payload = BrowseConstants.Payload.Replace("{0}", continuationToken == null ? $"\"browseId\": \"VL{playlistId}\"" : $"\"continuation\": \"{continuationToken}\"");
            request.AddStringBody(payload, ContentType.Json);

            RestResponse response = await _youtubeRestClient.ExecuteAsync(request);
            return response;
        }

        private async Task<List<Video>> GetPlaylistVideosRecursiveAsync(string playlistId, JToken previousToken = null)
        {
            var videos = new List<Video>();

            if (previousToken != null)
            {
                foreach(JToken token in previousToken)
                {
                    JToken vr = token.SelectToken("playlistVideoRenderer");

                    if (vr == null)
                    {
                        string newContinuationToken = (string)token.SelectToken("continuationItemRenderer.continuationEndpoint.continuationCommand.token");
                        RestResponse response = await GetPlaylistPartByIdAsync2(playlistId, newContinuationToken);

                        JToken newToken = JObject.Parse(response.Content).SelectToken("onResponseReceivedActions[0].appendContinuationItemsAction.continuationItems");
                        List<Video> newVideos = await GetPlaylistVideosRecursiveAsync(playlistId, newToken);

                        videos.AddRange(newVideos);
                        continue;
                    }

                    string title = (string)vr.SelectToken("title.runs[0].text");
                    string videoId = (string)vr.SelectToken("videoId");
                    string thumbnailUrl = (string)vr.SelectToken("thumbnail.thumbnails").Last.SelectToken("url");
                    string duration = (string)vr.SelectToken("lengthText.simpleText");

                    var video = new Video(title, videoId, thumbnailUrl, duration);
                    videos.Add(video);
                }
            }

            return videos;
        }

        public async Task<Playlist> GetPlaylistByIdAsync2(string playlistId)
        {
            RestResponse response = await GetPlaylistPartByIdAsync2(playlistId);

            JObject obj = JObject.Parse(response.Content);

            string maybeErrorMessage = (string)obj.SelectToken("alerts[0].alertRenderer.text.runs[0].text");
            if (maybeErrorMessage == "The playlist does not exist.") throw new NotFoundException();

            JToken videoItems = obj.SelectToken("contents.twoColumnBrowseResultsRenderer.tabs[0].tabRenderer.content.sectionListRenderer.contents[0].itemSectionRenderer.contents[0].playlistVideoListRenderer.contents");

            string playlistTitle = (string)obj.SelectToken("metadata.playlistMetadataRenderer.title");
            List<Video> videos = await GetPlaylistVideosRecursiveAsync(playlistId, previousToken: videoItems);
            var playlist = new Playlist(videos)
            {
                Id = playlistId,
                Title = playlistTitle,
            };

            return playlist;
        }

        private async Task<RestResponse> GetVideosPartByIdAsync(string[] videoIds, string pageToken = null)
        {
            var request = new RestRequest("youtube/v3/videos", Method.Get);

            request.AddHeader("Accept", "application/json");
            request.AddQueryParameter("key", _personalApiKey);

            request.AddQueryParameter("part", "snippet,contentDetails");
            request.AddQueryParameter("id", string.Join(",", videoIds));
            request.AddQueryParameter("maxResults", "50");
            if (pageToken != null) request.AddQueryParameter("pageToken", pageToken);

            RestResponse response = await _googleApisRestClient.ExecuteAsync(request);
            return response;
        }

        public async Task<List<Video>> GetVideosByIdsAsync(params string[] videoIds)
        {
            string[][] videoIdChunks = videoIds
                                .Select((s, i) => new { Value = s, Index = i })
                                .GroupBy(x => x.Index / 50)
                                .Select(grp => grp.Select(x => x.Value).ToArray())
                                .ToArray();

            List<Task<RestResponse>> tasks = new List<Task<RestResponse>>();
            foreach (string[] chunk in videoIdChunks)
            {
                Task<RestResponse> task = GetVideosPartByIdAsync(chunk);
                tasks.Add(task);
            }

            RestResponse[] responses = await Task.WhenAll(tasks);

            List<Video> videos = new List<Video>();
            foreach (RestResponse response in responses)
            {
                JObject obj = JObject.Parse(response.Content);
                foreach (JToken token in obj.SelectToken("items"))
                {
                    string title = (string)token.SelectToken("snippet.title");
                    string id = (string)token.SelectToken("id");
                    string thumbnailUrl = (string)token.SelectToken("snippet.thumbnails.high.url");
                    string duration = (string)token.SelectToken("contentDetails.duration");
                    string parsedDuration = Video.ParseDuration(duration);

                    Video video = new Video(title, id, thumbnailUrl, parsedDuration);
                    videos.Add(video);
                }
            }

            return videos;
        }

        private async Task<RestResponse> GetPlaylistPartByIdAsync(string id, int limit = -1, string pageToken = null)
        {
            var request = new RestRequest("youtube/v3/playlistItems", Method.Get);

            request.AddHeader("Accept", "application/json");
            request.AddQueryParameter("key", _personalApiKey);

            request.AddQueryParameter("part", "snippet");
            request.AddQueryParameter("playlistId", id);
            request.AddQueryParameter("maxResults", (limit > 0 ? Math.Min(limit, 50) : 50).ToString());
            if (pageToken != null) request.AddQueryParameter("pageToken", pageToken);

            RestResponse response = await _googleApisRestClient.ExecuteAsync(request);
            return response;
        }

        public async Task<Playlist> GetPlaylistByIdAsync(string id, int limit = -1)
        {
            List<JObject> responses = new List<JObject>();

            RestResponse firstPart = await GetPlaylistPartByIdAsync(id, limit: limit);
            JObject obj = JObject.Parse(firstPart.Content);
            responses.Add(obj);

            int totalItems = (int)obj.SelectToken("pageInfo.totalResults");

            if ((limit > 50 || limit < 0) && totalItems > 50)
            {
                for (int i = 0; i < (int)Math.Ceiling((double)(totalItems - 50) / 50); i++)
                {
                    string pageToken = (string)obj.SelectToken("nextPageToken");
                    int newLimit = limit - (50 * (i + 1));

                    RestResponse partResponse = await GetPlaylistPartByIdAsync(id, limit: newLimit, pageToken: pageToken);
                    obj = JObject.Parse(partResponse.Content);
                    responses.Add(obj);
                }
            }

            List<string> videoIds = new List<string>();

            foreach (JObject response in responses)
            {
                foreach (JToken token in response.SelectToken("items"))
                {
                    videoIds.Add((string)token.SelectToken("snippet.resourceId.videoId"));
                }
            }

            List<Video> videos = await GetVideosByIdsAsync(videoIds.ToArray());

            return new Playlist(videos)
            {
                Id = id,
            };
        }
    }
}
