using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using SpiixBot.Attributes;
using SpiixBot.Modules.Audio;
using SpiixBot.Modules.Audio.Player;
using SpiixBot.Modules.Audio.Queue;
using SpiixBot.Modules.Audio.Queue.Actions;
using SpiixBot.Services;
using SpiixBot.Spotify.Models;
using SpiixBot.Util;
using SpiixBot.Util.Paginator;
using SpiixBot.Util.Prompts;
using SpiixBot.Youtube;
using SpiixBot.Youtube.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Filters;

namespace SpiixBot.Modules
{
    public class AudioModule : ModuleBase<SocketCommandContext>
    {
        private readonly Regex _youtubeHostRegex = new Regex(@"^(?:www\.)?(?:(?:youtube\.[a-zA-Z]{2,3})|(?:youtu.be))$", RegexOptions.Compiled);
        private readonly Regex _spotifyHostRegex = new Regex(@"^(?:www\.)?open\.(?:spotify\.[a-zA-Z]{2,3})$", RegexOptions.Compiled);

        private DiscordSocketClient _client;
        private AudioService _audioService;
        private LavaNode _lavaNode;
        private YoutubeService _youtubeService;
        private SpotifyService _spotifyService;
        private YoutubeClient _youtubeClient => _youtubeService.Client;

        public AudioModule(DiscordSocketClient client, AudioService audioService, LavaNode lavaNode, YoutubeService youtubeService, SpotifyService spotifyService)
        {
            _client = client;
            _audioService = audioService;
            _lavaNode = lavaNode;
            _youtubeService = youtubeService;
            _spotifyService = spotifyService;
        }

        public string GetAsMegabyte(ulong raw)
        {
            ulong asMega = raw / (ulong)Math.Pow(2d, 20d);
            return $"{asMega}MB";
        }

        [Command("play", RunMode = RunMode.Async)]
        [Alias("p")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches YouTube and queues the best result.")]
        public async Task PlayCommand([Remainder][Summary("If left empty the command will attempt to unpause the playback.")] string query = "")
        {
            if (query == "")
            {
                await Unpause();
                return;
            }

            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                await ProcessPlay(query, guildUser);
            }
        }

        [Command("spotifyplay", RunMode = RunMode.Async)]
        [Alias("sp", "splay")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches Spotify and queues the best result.")]
        public async Task SpotifyPlayCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                await ProcessPlaySpotify(query, guildUser);
            }
        }

        [Command("playtop", RunMode = RunMode.Async)]
        [Alias("pt", "ptop", "playt")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches YouTube and adds the best result to the top of the queue.")]
        public async Task PlayTopCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new MoveItemsAction
                {
                    Author = guildUser,
                    InsertAtIndex = 0,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlay(query, guildUser, followedByAction: action);
            }
        }

        [Command("spotifyplaytop", RunMode = RunMode.Async)]
        [Alias("splaytop", "spt", "sptop", "splayt")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches Spotify and adds the best result to the top of the queue.")]
        public async Task SpotifyPlayTopCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new MoveItemsAction
                {
                    Author = guildUser,
                    InsertAtIndex = 0,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlaySpotify(query, guildUser, followedByAction: action);
            }
        }


        [RequireContext(ContextType.Guild)]
        [Command("playshuffle", RunMode = RunMode.Async)]
        [Alias("pshuffle", "playshuff", "pshuff")]
        [Summary("Searches YouTube and queues the best result. Then shuffles the queue.")]
        public async Task PlayShuffleCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new ShuffleAction
                {
                    Author = Context.User as SocketGuildUser,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlay(query, guildUser, followedByAction: action);
            }
        }

        [RequireContext(ContextType.Guild)]
        [Command("spotifyplayshuffle", RunMode = RunMode.Async)]
        [Alias("spshuffle", "splayshuff", "spshuff", "spotifyplayshuff")]
        [Summary("Searches Spotify and queues the best result. Then shuffles the queue.")]
        public async Task PlaySpotifyShuffleCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new ShuffleAction
                {
                    Author = Context.User as SocketGuildUser,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlaySpotify(query, guildUser, followedByAction: action);
            }
        }

        [Command("playskip", RunMode = RunMode.Async)]
        [Alias("ps", "pskip", "plays")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches YouTube and instantly plays the best result.")]
        public async Task PlaySkipCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new MoveItemsAction
                {
                    Author = guildUser,
                    InsertAtIndex = 0,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlay(query, guildUser, followedByAction: action, followedBySkip: true);
            }
        }

        [Command("spotifyplayskip", RunMode = RunMode.Async)]
        [Alias("sps", "spskip", "splays", "splayskip")]
        [RequireContext(ContextType.Guild)]
        [Summary("Searches Spotify and instantly plays the best result.")]
        public async Task SpotifyPlaySkipCommand([Remainder] string query)
        {
            if (!await RunVoiceChannelCheck()) return;

            using (var loading = new LoadingReaction(Context))
            {
                var guildUser = (SocketGuildUser)Context.User;

                IQueueAction action = new MoveItemsAction
                {
                    Author = guildUser,
                    InsertAtIndex = 0,
                    IsLinkedToPrevious = true,
                };

                await ProcessPlaySpotify(query, guildUser, followedByAction: action, followedBySkip: true);
            }
        }

        public async Task ProcessPlay(string input, SocketGuildUser guildUser = null, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            if (guildUser == null) guildUser = Context.User as SocketGuildUser;

            Uri uriResult;
            bool result = Uri.TryCreate(input, UriKind.Absolute, out uriResult);

            if (result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                var queryParams = HttpUtility.ParseQueryString(uriResult.Query);

                if (_youtubeHostRegex.Match(uriResult.Host).Success)
                {
                    string listParam = queryParams.Get("list");
                    if (listParam != null)
                    {   
                        bool res = await SearchPlaylistAndPlay(listParam, guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                        if (res) return;
                    }

                    string idParam = queryParams.Get("v");
                    if (idParam != null)
                    {
                        await LookupAndPlay(idParam, guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                        return;
                    }
                }
                else if (_spotifyHostRegex.Match(uriResult.Host).Success)
                {
                    string[] split = uriResult.AbsolutePath.Substring(1).Split('/');
                    if (split.Length == 2)
                    {
                        switch (split[0])
                        {
                            case "playlist":
                                await SearchSpotifyPlaylistAndPlayAsync(split[1], guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                                return;
                            case "track":
                                await LookupSpotifyTrackAndPlayAsync(split[1], guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                                return;
                        }
                    }
                }
            }

            await SearchAndPlay(input, guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
        }

        public async Task ProcessPlaySpotify(string input, SocketGuildUser guildUser = null, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            if (guildUser == null) guildUser = Context.User as SocketGuildUser;

            bool result = Uri.TryCreate(input, UriKind.Absolute, out Uri uriResult);

            if (result && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps))
            {
                if (_spotifyHostRegex.Match(uriResult.Host).Success)
                {
                    string[] split = uriResult.AbsolutePath.Substring(1).Split('/');
                    if (split.Length == 2)
                    { 
                        switch (split[0])
                        {
                            case "playlist":
                                await SearchSpotifyPlaylistAndPlayAsync(split[1], guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                                return;
                            case "track":
                                await LookupSpotifyTrackAndPlayAsync(split[1], guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
                                return;
                        }
                    }
                }
            }

            await SpotifySearchAndPlayAsync(input, guildUser, followedByAction: followedByAction, followedBySkip: followedBySkip);
        }

        public async Task<bool> RunVoiceChannelCheck()
        {
            var guildUser = Context.User as SocketGuildUser;
            if (guildUser.VoiceChannel is null)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("You must be connected to a voice channel to use this command."));
                return false;
            }

            return true;
        }

        public async Task SendConnectedAndBoundTo(IVoiceChannel voiceChannel)
        {
            if (Context.Guild.CurrentUser.VoiceChannel != null) return;

            await ReplyAsync(embed: MessageHelper.GetEmbed(description: $"Connecting to voice channel `{voiceChannel.Name}` and bound to text channel <#{Context.Channel.Id}>"));
        }

        public async Task RunPlayerAsync(SocketVoiceChannel voiceChannel, bool shouldSkip = false)
        {
            SocketGuild guild = voiceChannel.Guild;
            if (voiceChannel is null)
            {
                Embed embed = MessageHelper.GetErrorEmbed("An error occured because you are not connected to a voice channel.");
                await ReplyAsync(embed: embed);
            }

            Player player = _audioService.GetOrCreatePlayer(guild, Context.Channel);

            if (shouldSkip && _lavaNode.HasPlayer(Context.Guild) && player.IsCurrentlyPlaying)
            {
                var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);

                if (lavaPlayer.PlayerState == PlayerState.Paused)
                {
                    await lavaPlayer.SeekAsync(TimeSpan.FromMilliseconds(0));
                    await Task.Delay(1000);
                }

                await player.PlayNextTrack(lavaPlayer);
            }
            else if (!player.IsCurrentlyPlaying) await player.RunLavaLink(voiceChannel);
            //else throw new Exception("Error while running player!");
        }

        public string GetIconUrlByProvider(ItemProvider provider)
        {
            switch (provider)
            {
                case ItemProvider.Youtube:
                    return "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";

                case ItemProvider.Twitch:
                    return "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";

                case ItemProvider.Spotify:
                    return "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
            }

            return "Unknown";
        }

        public Embed GetQueuedEmbedSingle(Player player, QueueItem item, int timeUntilPlaying = -1, int position = -1, bool isSkip = false)
        {
            string iconUrl = "";
            string externalUrl = "";
            string title = "";
            int duration = 0;
            string thumbnailUrl = "";
            switch (item.Provider)
            {
                case ItemProvider.Youtube:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";
                    externalUrl = item.VideoInfo.Url;
                    title = item.VideoInfo.Title;
                    duration = item.VideoInfo.GetDurationInSeconds();
                    thumbnailUrl = item.VideoInfo.ThumbnailUrl;
                    break;

                case ItemProvider.Spotify:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
                    externalUrl = item.SongInfo.ExternalUrl;
                    title = item.SongInfo.Name;
                    duration = item.SongInfo.Duration;
                    thumbnailUrl = item.SongInfo.ThumbnailUrl;
                    break;

                case ItemProvider.Twitch:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";
                    break;
            }

            var authorBuilder = new EmbedAuthorBuilder()
                .WithIconUrl(iconUrl)
                .WithName((player.Queue.Length == 1 && !player.IsCurrentlyPlaying) || isSkip ? "Now Playing" : "Added To Queue");

            var builder = new EmbedBuilder()
                .WithAuthor(authorBuilder)
                .WithThumbnailUrl(thumbnailUrl);

            var titleFieldBuilder = new EmbedFieldBuilder()
                .WithName("Title")
                .WithValue($"[{title}]({externalUrl})")
                .WithIsInline(true);
            builder.AddField(titleFieldBuilder);

            var durationFieldBuilder = new EmbedFieldBuilder()
                .WithName("Duration")
                .WithValue($"`{TimeUtil.ToReadableTime(duration)}`")
                .WithIsInline(true);
            builder.AddField(durationFieldBuilder);

            if (timeUntilPlaying != -1)
            {
                var timeUntilPlayingDurationFieldBuilder = new EmbedFieldBuilder()
                    .WithName("Time Until Playing")
                    .WithValue($"`{TimeUtil.ToReadableTime(timeUntilPlaying)}`")
                    .WithIsInline(true);
                builder.AddField(timeUntilPlayingDurationFieldBuilder);
            }

            if (position != -1)
            {
                var positionFieldBuilder = new EmbedFieldBuilder()
                    .WithName("Position In Queue")
                    .WithValue($"`{position}`")
                    .WithIsInline(true);
                builder.AddField(positionFieldBuilder);
            }

            return builder.Build();
        }

        public Embed GetQueuedEmbedPlaylist(Player player, ItemProvider provider, int totalDuration, string name, string url, string thumbnailUrl, int timeUntilPlaying = -1, int position = -1, bool isSkip = false)
        {
            string iconUrl = "";
            switch (provider)
            {
                case ItemProvider.Youtube:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";
                    break;

                case ItemProvider.Twitch:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";
                    break;

                case ItemProvider.Spotify:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
                    break;
            }

            var authorBuilder = new EmbedAuthorBuilder()
                .WithIconUrl(iconUrl)
                .WithName((player.Queue.Length == 1 && !player.IsCurrentlyPlaying) || isSkip ? "Now Playing Playlist" : "Added Playlist To Queue");

            var builder = new EmbedBuilder()
                .WithAuthor(authorBuilder)
                .WithThumbnailUrl(thumbnailUrl);

            var titleFieldBuilder = new EmbedFieldBuilder()
                .WithName("Playlist Title")
                .WithValue($"[{name}]({url})")
                .WithIsInline(true);
            builder.AddField(titleFieldBuilder);

            var durationFieldBuilder = new EmbedFieldBuilder()
                .WithName("Total Duration")
                .WithValue($"`{TimeUtil.ToReadableTime(totalDuration)}`")
                .WithIsInline(true);
            builder.AddField(durationFieldBuilder);

            if (timeUntilPlaying != -1)
            {
                var timeUntilPlayingDurationFieldBuilder = new EmbedFieldBuilder()
                    .WithName("Time Until Playing")
                    .WithValue($"`{TimeUtil.ToReadableTime(timeUntilPlaying)}`")
                    .WithIsInline(true);
                builder.AddField(timeUntilPlayingDurationFieldBuilder);
            }

            if (position != -1)
            {
                var positionFieldBuilder = new EmbedFieldBuilder()
                    .WithName("Position In Queue")
                    .WithValue($"`{position}`")
                    .WithIsInline(true);
                builder.AddField(positionFieldBuilder);
            }

            return builder.Build();
        }

        public async Task<bool> CheckPausedPrompt()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild, Context.Channel);
            if (_lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                if (lavaPlayer.PlayerState == PlayerState.Paused)
                {
                    await using (var prompt = new YesOrNoPrompt(Context, message: "The player is currently paused. Do you want to clear the queue and instantly skip to this track?"))
                    {
                        bool result;
                        try
                        {
                            result = await prompt.Run();
                        }
                        catch (TimeoutException)
                        {
                            await ReplyAsync(embed: MessageHelper.GetErrorEmbed("You took too long. Cancelled the operation."));
                            throw;
                        }

                        if (result)
                        {
                            if (player.Queue.Length > 0) await Clear(1, -1);

                            await ReplyAsync(embed: MessageHelper.GetEmbed("Cleared queue and skipping..."));
                            return true;
                        }
                        else
                        {
                            await ReplyAsync(embed: MessageHelper.GetEmbed("Okay. Queueing normally..."));
                        }
                    }
                }
            }

            return false;
        }

        public async Task SearchAndPlay(string query, SocketGuildUser guildUser, ItemProvider provider = ItemProvider.Youtube, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            List<Video> videos;
            try
            {
                videos = await _youtubeClient.SearchVideosAsync(query);
            }
            catch (NotFoundException)
            {
                Embed embed = MessageHelper.GetErrorEmbed("The search yielded no results.");
                await ReplyAsync(embed: embed);
                return;
            }

            Video video = videos[0];

            Player player = _audioService.GetOrCreatePlayer(Context.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return;
            }

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }

            QueueItem item = new QueueItem
            {
                User = guildUser,
                VideoInfo = video,
                Provider = provider,
            };
            player.Queue.PerformAction(new AddItemAction
            {
                Author = Context.User as SocketGuildUser,
                Item = item,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = player.Queue.Length - 1;
                    action.EndIndex = player.Queue.Length - 1;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added \"{videos[0].Title}\" to the queue.");
            Embed addedEmbed = GetQueuedEmbedSingle(player, item, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);
        }

        public async Task LookupAndPlay(string videoId, SocketGuildUser guildUser, ItemProvider provider = ItemProvider.Youtube, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            List<Video> videos = await _youtubeClient.GetVideosByIdsAsync(videoId);
            if (videos.Count == 0)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The search yielded no results."));
                return;
            }

            Video video = videos[0];

            Player player = _audioService.GetOrCreatePlayer(Context.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return;
            }

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }

            QueueItem item = new QueueItem
            {
                User = guildUser,
                VideoInfo = video,
                Provider = provider,
            };
            player.Queue.PerformAction(new AddItemAction
            {
                Author = Context.User as SocketGuildUser,
                Item = item,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = player.Queue.Length - 1;
                    action.EndIndex = player.Queue.Length - 1;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added \"{videos[0].Title}\" to the queue.");
            Embed addedEmbed = GetQueuedEmbedSingle(player, item, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);
        }

        public async Task<bool> SearchPlaylistAndPlay(string playlistId, SocketGuildUser guildUser, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            Youtube.Models.Playlist playlist;
            try
            {
                playlist = await _youtubeClient.GetPlaylistByIdAsync2(playlistId);
            }
            catch (NotFoundException)
            {
                Embed embed = MessageHelper.GetErrorEmbed("The playlist specified was not found.");
                await ReplyAsync(embed: embed);
                return true;
            }

            if (playlist.Videos.Count < 1) return false;

            IEnumerable<QueueItem> queueItems = playlist.Videos.Select(item =>
            {
                return new QueueItem
                {
                    User = guildUser,
                    VideoInfo = item,
                    IsPlaylistItem = true,
                    Provider = ItemProvider.Youtube,
                };
            });

            Player player = _audioService.GetOrCreatePlayer(guildUser.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return true;
            }

            int preLength = player.Queue.Length;

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }


            player.Queue.PerformAction(new AddPlaylistItemsAction
            {
                Author = Context.User as SocketGuildUser,
                Items = queueItems,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = preLength - 1;
                    action.EndIndex = preLength + playlist.Videos.Count - 2;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added **{playlist.Videos.Count}** videos to the queue.");
            int totalDuration = playlist.GetCombinedDuration();

            Embed addedEmbed = GetQueuedEmbedPlaylist(player, ItemProvider.Youtube, totalDuration, playlist.Title, playlist.Url, playlist.Videos.First().ThumbnailUrl, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);

            return true;
        }

        public async Task SpotifySearchAndPlayAsync(string query, SocketGuildUser guildUser, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            List<Song> songs;
            try
            {
                songs = await _spotifyService.Client.SearchSongsAsync(query, limit: 1);
            }
            catch (Spotify.NotFoundException)
            {
                Embed embed = MessageHelper.GetErrorEmbed("The search yielded no results.");
                await ReplyAsync(embed: embed);
                return;
            }

            Song song = songs[0];
            string youtubeQuery = $"{song.Artist} - {song.Name} Audio";

            List<Video> videos;
            try
            {
                videos = await _youtubeClient.SearchVideosAsync(youtubeQuery, limit: 1);
            }
            catch (NotFoundException)
            {
                Embed embed = MessageHelper.GetErrorEmbed("The search yielded no results.");
                await ReplyAsync(embed: embed);
                return;
            }

            Video video = videos[0];

            Player player = _audioService.GetOrCreatePlayer(Context.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return;
            }

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }

            QueueItem item = new QueueItem
            {
                User = guildUser,
                VideoInfo = video,
                SongInfo = song,
                Provider = ItemProvider.Spotify,
            };

            player.Queue.PerformAction(new AddItemAction
            {
                Author = Context.User as SocketGuildUser,
                Item = item,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = player.Queue.Length - 1;  // -1 because _currentIndex will be incremented before this is performed
                    action.EndIndex = player.Queue.Length - 1;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added \"{song.Name}\" by \"{song.Artist}\" to the queue.");
            Embed addedEmbed = GetQueuedEmbedSingle(player, item, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);
        }

        public async Task SearchSpotifyPlaylistAndPlayAsync(string playlistId, SocketGuildUser guildUser, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            Spotify.Models.Playlist playlist;
            try
            {
                playlist = await _spotifyService.Client.GetPlaylistAsync(playlistId);
            }
            catch (Spotify.NotFoundException)
            {
                Embed embed = MessageHelper.GetErrorEmbed("The playlist specified was not found.");
                await ReplyAsync(embed: embed);
                return;
            }

            IEnumerable<QueueItem> queueItems = playlist.Songs.Select(item =>
            {
                return new QueueItem
                {
                    User = guildUser,
                    VideoInfo = new Video(item.Name, item.Id, item.ThumbnailUrl, TimeUtil.ToReadableTime(item.Duration)),
                    SongInfo = item,
                    IsPlaylistItem = true,
                    Provider = ItemProvider.Spotify,
                    HasDummyVideoInfo = true,
                };
            });

            Player player = _audioService.GetOrCreatePlayer(guildUser.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return;
            }

            int preLength = player.Queue.Length;

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }

            player.Queue.PerformAction(new AddPlaylistItemsAction
            {
                Author = Context.User as SocketGuildUser,
                Items = queueItems,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = preLength;
                    action.EndIndex = preLength + playlist.Songs.Count - 1;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            int totalDuration = playlist.GetCombinedDuration();

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added **{playlist.Songs.Count}** songs to the queue.");
            Embed addedEmbed = GetQueuedEmbedPlaylist(player, ItemProvider.Spotify, totalDuration, playlist.Name, playlist.Url, playlist.Songs.First().ThumbnailUrl, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);
        }

        public async Task LookupSpotifyTrackAndPlayAsync(string trackId, SocketGuildUser guildUser, IQueueAction followedByAction = null, bool followedBySkip = false)
        {
            Song song;
            try
            {
                song = await _spotifyService.Client.GetSongAsync(trackId);
            }
            catch (Spotify.NotFoundException)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Could not find the specified spotify song."));
                return;
            }

            string youtubeQuery = $"{song.Artist} - {song.Name} Audio";

            List<Video> videos;
            try
            {
                videos = await _youtubeClient.SearchVideosAsync(youtubeQuery, limit: 1);
            }
            catch (NotFoundException)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The search yielded no results."));
                return;
            }

            Video video = videos[0];

            Player player = _audioService.GetOrCreatePlayer(guildUser.Guild, Context.Channel);

            try
            {
                bool result = await CheckPausedPrompt();
                if (result) followedBySkip = true;
            }
            catch (TimeoutException)
            {
                return;
            }

            bool isSkip = false;
            int timeUntilPlaying = player.IsCurrentlyPlaying ? 0 : -1;
            int position = player.IsCurrentlyPlaying ? 1 : -1;
            if (!followedBySkip && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
                timeUntilPlaying += (int)(player.GetTrackDurationLeft(lavaPlayer) / 1000);
            }

            if (followedBySkip)
            {
                timeUntilPlaying = -1;
                position = -1;
                isSkip = true;
            }

            if ((followedByAction == null || !(followedByAction is MoveItemsAction)) && player.IsCurrentlyPlaying && _lavaNode.HasPlayer(Context.Guild))
            {
                timeUntilPlaying += player.Queue.GetTotalDuration();
                position = player.Queue.Length + 1;
            }

            QueueItem item = new QueueItem
            {
                User = guildUser,
                VideoInfo = video,
                SongInfo = song,
                Provider = ItemProvider.Spotify,
            };

            player.Queue.PerformAction(new AddItemAction
            {
                Author = Context.User as SocketGuildUser,
                Item = item,
            });

            if (followedByAction != null)
            {
                if (followedByAction is MoveItemsAction action)
                {
                    action.StartIndex = player.Queue.Length - 1;  // -1 because _currentIndex will be incremented before this is performed
                    action.EndIndex = player.Queue.Length - 1;

                    player.Queue.PerformAction(action);
                }
                else player.Queue.PerformAction(followedByAction);
            }

            await SendConnectedAndBoundTo(guildUser.VoiceChannel);
            //Embed addedToQueueEmbed = MessageHelper.GetSuccessEmbed($"Added \"{song.Name}\" by \"{song.Artist}\" to the queue.");
            Embed addedEmbed = GetQueuedEmbedSingle(player, item, timeUntilPlaying: timeUntilPlaying, position: position, isSkip: isSkip);
            await ReplyAsync(embed: addedEmbed);

            await RunPlayerAsync(guildUser.VoiceChannel, shouldSkip: followedBySkip);
        }

        [Command("disconnect")]
        [Alias("dc")]
        [RequireContext(ContextType.Guild)]
        [Summary("Disconnects the bot from its current voice channel.")]
        public async Task DisconnectCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }

            _audioService.CleanupAndRemovePlayer(Context.Guild.Id);

            await _lavaNode.LeaveAsync(lavaPlayer.VoiceChannel);

            await ReplyAsync(embed: MessageHelper.GetSuccessEmbed($"The queue has been cleared and the bot has been disconnected."));
        }

        [Command("pause")]
        [Alias("stop")]
        [RequireContext(ContextType.Guild)]
        [Summary("Pauses the current track.")]
        public async Task PauseCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState != PlayerState.Playing)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            player.ScheduleDisconnect(60 * 60 * 1000);
            await lavaPlayer.PauseAsync();

            await ReplyAsync(embed: MessageHelper.GetEmbed("Paused playback. _Disconnect timer started (1hr)_"));
        }

        public async Task Unpause()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState != PlayerState.Paused)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Cannot unpause as the playback is not paused."));
                return;
            }

            await lavaPlayer.ResumeAsync();

            await ReplyAsync(embed: MessageHelper.GetEmbed("Unpaused playback."));
        }

        [Command("unpause")]
        [Alias("re", "res", "resume", "continue")]
        [RequireContext(ContextType.Guild)]
        [Summary("Unpauses the current track.")]
        public async Task UnpauseCommand()
        {
            await Unpause();
        }

        [Command("forceskip")]
        [Alias("skip", "fs")]
        [RequireContext(ContextType.Guild)]
        [Summary("Skips the current track.")]
        public async Task ForceSkipCommand(int amountToSkip = 1)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            { 
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            if (player.Queue.IsReapeating())
            {
                await player.PlayNextTrack(lavaPlayer);
                await ReplyAsync(embed: MessageHelper.GetEmbed("Force skipped current song.\n\n_Repeat enabled. Turn off with `!repeat`_"));
            }
            else
            {
                if (!player.Queue.Empty)
                {
                    if (amountToSkip < 1 || amountToSkip > player.Queue.Length)
                    {
                        await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`amountToSkip` must be larger than 0 and less than or equal to the queue size."));
                        return;
                    }

                    if (amountToSkip > 1) player.Queue.IncrementCurrentIndex(amountToSkip - 1);
                    await player.PlayNextTrack(lavaPlayer);
                }
                else
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("There are no more items in the queue."));
                    return;
                }

                if (amountToSkip == 1) await ReplyAsync(embed: MessageHelper.GetEmbed("Force skipped current song."));
                else await ReplyAsync(embed: MessageHelper.GetEmbed($"Force skipped current song and the upcoming {amountToSkip - 1} songs"));
            }
        }

        [RequireContext(ContextType.Guild)]
        [Command("goback")]
        [Alias("previous", "prev")]
        [Summary("Instantly plays the previous track.")]
        public async Task PreviousCommand(int goTo = 1)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            if (player.Queue.CurrentIndex - 1 <= 0)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"No history exists yet."));
                return;
            }

            if (goTo < 1 || goTo > player.Queue.CurrentIndex - 1)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"goTo must be more than or equal to 1 and less than history size."));
                return;
            }

            player.Queue.DecrementCurrentIndex(goTo + 1);

            await ReplyAsync(embed: GetQueuedEmbedSingle(player, player.Queue.GetNextItem(false), isSkip: true));

            LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            await player.PlayNextTrack(lavaPlayer);
        }

        public async Task<int> ConvertToSeconds(string input)
        {
            int totalSeconds = 0;

            if (input.Contains(':'))
            {
                string[] split = input.Split(':');
                if (split.Length != 2 || !Int32.TryParse(split[0], out int minutes) || !Int32.TryParse(split[1], out int seconds))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Format must be like this `<minutes>:<seconds>`."));
                    return -1;
                }

                totalSeconds += minutes * 60;
                totalSeconds += seconds;
            }
            else
            {
                if (!Int32.TryParse(input, out int seconds))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Format must be plain seconds or like this `<minutes>:<seconds>`."));
                    return -1;
                }

                totalSeconds += seconds;
            }

            return Math.Max(totalSeconds, 0);
        }

        [RequireContext(ContextType.Guild)]
        [Command("seek")]
        [Summary("Jumps to a certain time in a track.")]
        public async Task SeekCommand([Summary("Accepts both seconds and this format: `<minutes>:<seconds>`")] string secondsOrFmt)
        {
            int seconds = await ConvertToSeconds(secondsOrFmt);
            if (seconds == -1) return;

            if (seconds <= 0)
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"Seconds must be bigger than 0");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            if (seconds > lavaPlayer.Track.Duration.TotalSeconds)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Seconds may not exceed the length of the current song."));
                return;
            }

            await lavaPlayer.SeekAsync(TimeSpan.FromSeconds(seconds));

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Seeked to `{seconds}` seconds in the current song."));
        }

        [Command("forward")]
        [RequireContext(ContextType.Guild)]
        [Summary("Jumps forward a certain time in a track.")]
        public async Task ForwardCommand([Summary("Accepts both seconds and this format: `<minutes>:<seconds>`")] string secondsOrFmt)
        {
            int seconds = await ConvertToSeconds(secondsOrFmt);
            if (seconds == -1) return;

            if (seconds <= 0)
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"Seconds must be bigger than 0");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            double actualPosition = player.GetActualTrackPosition(lavaPlayer);

            TimeSpan duration = lavaPlayer.Track.Duration;
            if (actualPosition + seconds * 1000 > duration.TotalMilliseconds)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The song is not long enough."));
                return;
            }

            await lavaPlayer.SeekAsync(TimeSpan.FromMilliseconds(actualPosition + seconds * 1000));

            Embed embed = MessageHelper.GetEmbed($"Forwarded {seconds} seconds.");
            await ReplyAsync(embed: embed);
        }

        [Command("backwards")]
        [Alias("rewind")]
        [RequireContext(ContextType.Guild)]
        [Summary("Jumps backwards a certain time in a track.")]
        public async Task BackwardsCommand([Summary("Accepts both seconds and this format: `<minutes>:<seconds>`")] string secondsOrFmt)
        {
            int seconds = await ConvertToSeconds(secondsOrFmt);
            if (seconds == -1) return;

            if (seconds <= 0)
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"Seconds must be bigger than 0");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            double actualPosition = player.GetActualTrackPosition(lavaPlayer);

            await lavaPlayer.SeekAsync(TimeSpan.FromMilliseconds(Math.Max(actualPosition - seconds * 1000, 0)));

            Embed embed = MessageHelper.GetEmbed($"Rewinded {seconds} seconds.");
            await ReplyAsync(embed: embed);
        }

        [RequireContext(ContextType.Guild)]
        [Command("restart")]
        [Alias("replay")]
        [Summary("Restarts the current track.")]
        public async Task RestartCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            await lavaPlayer.SeekAsync(TimeSpan.FromMilliseconds(0));

            Embed embed = MessageHelper.GetEmbed($"Restarted the current song.");
            await ReplyAsync(embed: embed);
        }

        [RequireContext(ContextType.Guild)]
        [Command("volume")]
        [Summary("Sets or outputs the current volume.")]
        public async Task VolumeCommand(int volume = -1)
        {
            if (volume != -1 && (volume < 0 || volume > 1000))
            {
                Embed errorEmbed = MessageHelper.GetErrorEmbed($"Volume must be greater than 1 and less than 1000. Default is 100.");
                await ReplyAsync(embed: errorEmbed);
                return;
            }

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            LavaPlayer lavaPlayer = _lavaNode.GetPlayer(Context.Guild);

            if (volume == -1)
            {
                int printableVolume = lavaPlayer.Volume == 0 ? 100 : lavaPlayer.Volume;
                await ReplyAsync(embed: MessageHelper.GetEmbed($"Current volume: `{printableVolume}%`."));
                return;
            }

            await lavaPlayer.UpdateVolumeAsync((ushort)volume);

            Embed embed = MessageHelper.GetEmbed($"Successfully adjusted volume to `{volume}`.");
            await ReplyAsync(embed: embed);
        }

        private const string _eqSummary = "A space separated list of this format `<band 1-15>:<gain -25-100>`. If set to \"clear\" it will clear the current equalizer bands.";

        [RequireContext(ContextType.Guild)]
        [Command("equalizer")]
        [Alias("eq")]
        [Summary("Applies or outputs equalizer bands.")]
        public async Task EqualizerCommand([Remainder][Summary(_eqSummary)]string bands = "")
        {
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);

            if (bands == "")
            {
                string[] hzF = new string[]
                {
                    "25 Hz", "40 Hz", "63 Hz", "100 Hz", "160 Hz", "250 Hz", "400 Hz", "630 Hz", "1 kHz", "1.6 kHz", "2.5 kHz", "4 kHz", "6.3 kHz", "10 kHz", "16 kHz"
                };

                List<string> parts = new List<string>();
                if (lavaPlayer.Equalizer != null)
                {
                    foreach (EqualizerBand band in lavaPlayer.Equalizer)
                    {
                        string gain = (band.Gain * 100).ToString().Replace(',', '.');
                        if (band.Gain != 0) parts.Add($"Band: {band.Band + 1} ({hzF[band.Band]}) | Gain: {gain}");
                    }
                }

                string fmt = parts.Count != 0 ? string.Join("\n", parts) : "None";

                await ReplyAsync(embed: MessageHelper.GetEmbed($"Current equalizer settings:\n```\n{fmt}```"));
                return;
            }

            if (bands.ToLower() == "clear")
            {
                await ClearEqualizer();
                return;
            }

            string[] split = bands.Split(' ');
            int[] actualBands = new int[split.Length];
            double[] actualGains = new double[split.Length];

            for (int i = 0; i < split.Length; i++)
            {
                string part = split[i];

                string[] innerSplit = part.Split(':');
                if (innerSplit.Length != 2)
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Bands must be a space separated list of this format `<band 1-15>:<gain -25-100>`\nExample: `!equalizer 3:40 9:-20`"));
                    return;
                }

                if (!Int32.TryParse(innerSplit[0], out int band)) 
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Bands must be a space separated list of this format `<band 1-15>:<gain -25-100>`\nExample: `!equalizer 3:40 9:-20`"));
                    return;
                }

                double gain;
                if (!Double.TryParse(innerSplit[1], out gain) && !Double.TryParse(innerSplit[1].Replace('.', ','), out gain))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Bands must be a space separated list of this format `<band 1-15>:<gain -25-100>`\nExample: `!equalizer 3:40 9:-20`"));
                    return;
                }

                if (band < 1 || band > 15)
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Bands must be levels between 1 and 15."));
                    return;
                }

                if (gain < -25d || gain > 100d)
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Gain must be a level between -25 and 100."));
                    return;
                }

                if (actualBands.Contains(band))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Duplicate band specified: `{band}`"));
                    return;
                }

                actualBands[i] = band;
                actualGains[i] = gain;
            }

            EqualizerBand[] eqBands = new EqualizerBand[split.Length];
            string[] fmtBands = new string[split.Length];

            string[] hz = new string[]
            {
                "25 Hz", "40 Hz", "63 Hz", "100 Hz", "160 Hz", "250 Hz", "400 Hz", "630 Hz", "1 kHz", "1.6 kHz", "2.5 kHz", "4 kHz", "6.3 kHz", "10 kHz", "16 kHz"
            };

            for (int k = 0; k < split.Length; k++)
            {
                eqBands[k] = new EqualizerBand(actualBands[k] - 1, actualGains[k] / 100d);
                fmtBands[k] = $"Band: {actualBands[k]} ({hz[actualBands[k] - 1]}) | Gain: {actualGains[k].ToString().Replace(',', '.')}";
            }

            await lavaPlayer.EqualizerAsync(eqBands);

            Embed embed = MessageHelper.GetEmbed($"Changed equalizer bands changed to:\n```\n{string.Join("\n", fmtBands)}```\n_This might take a few seconds to apply..._");
            await ReplyAsync(embed: embed);
        }

        [RequireContext(ContextType.Guild)]
        [Command("bassboost")]
        [Alias("bb")]
        [Summary("Bass boosts the playback.")]
        public async Task BassBoostCommand([Summary("Currently only accepts levels ranging 1-3.")]string level = "1")
        {
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }

            if (level.ToLower() == "clear")
            {
                await ClearEqualizer();
                return;
            }

            if (!Int32.TryParse(level, out int outLevel))
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Level must be a number."));
                return;
            }

            EqualizerBand[][] bands = new EqualizerBand[][]
            {
                new EqualizerBand[]
                {
                    new EqualizerBand(0, -0.025d),
                    new EqualizerBand(1, 0.03d),
                    new EqualizerBand(2, 0.08d),
                    new EqualizerBand(3, 0.3d),
                    new EqualizerBand(4, -0.06d),
                    new EqualizerBand(5, 0.07d),
                },
                new EqualizerBand[]
                {
                    new EqualizerBand(0, -0.05d),
                    new EqualizerBand(1, 0.06d),
                    new EqualizerBand(2, 0.16d),
                    new EqualizerBand(3, 0.3d),
                    new EqualizerBand(4, -0.12d),
                    new EqualizerBand(5, 0.11d),
                },
                new EqualizerBand[]
                {
                    new EqualizerBand(0, -0.1d),
                    new EqualizerBand(1, 0.14d),
                    new EqualizerBand(2, 0.32d),
                    new EqualizerBand(3, 0.6d),
                    new EqualizerBand(4, -0.25d),
                    new EqualizerBand(5, 0.22d),
                },
            };

            if (outLevel < 1 || outLevel > bands.Length)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Level must be between 1 and {bands.Length}."));
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            await lavaPlayer.EqualizerAsync(bands[outLevel - 1]);

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Activated bass boost equalizer.\n\n_This might take a few seconds to apply..._"));
        }

        public async Task ClearEqualizer()
        {
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            EqualizerBand[] bands = new EqualizerBand[15];

            for (int i = 0; i < 15; i++)
            {
                bands[i] = new EqualizerBand(i, 0d);
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            await lavaPlayer.EqualizerAsync(bands);

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Cleared equalizer.\n\n_This might take a few seconds to apply..._"));
        }

        [RequireContext(ContextType.Guild)]
        [Command("clearequalizer")]
        [Alias("ceq", "cleareq", "cequalizer")]
        [Summary("Clears the equalizer bands.")]
        public async Task ClearEqualizerCommand()
        {
            await ClearEqualizer();
        }

        //[RequireContext(ContextType.Guild)]
        //[Command("filter")]
        //public async Task FilterCommand(double hz)
        //{
        //    if (!_lavaNode.HasPlayer(Context.Guild))
        //    {
        //        Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
        //        await ReplyAsync(embed: errEmbed);
        //        return;
        //    }

        //    var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
        //    await lavaPlayer.ApplyFilterAsync(new RotationFilter()
        //    {
        //        Hertz = hz,
        //    });

        //    await ReplyAsync("Applied filter");
        //}

        [Command("nowplaying")]
        [Alias("np")]
        [RequireContext(ContextType.Guild)]
        [Summary("Displays info about the current track.")]
        public async Task NowPlayingCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            QueueItem item = player.CurrentItem;
            Video video = item.VideoInfo;

            string extraLink = "";
            string iconUrl = "";
            switch (item.Provider)
            {
                case ItemProvider.Youtube:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";
                    break;

                case ItemProvider.Twitch:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";
                    break;

                case ItemProvider.Spotify:
                    iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
                    extraLink = $"[[Spotify]({item.SongInfo.ExternalUrl})] ";
                    break;
            }

            EmbedAuthorBuilder authorBuilder = new EmbedAuthorBuilder()
                .WithIconUrl(iconUrl)
                .WithName("Now playing");

            EmbedBuilder builder = new EmbedBuilder()
                .WithAuthor(authorBuilder)
                .WithThumbnailUrl(video.ThumbnailUrl);

            var titleFieldBuilder = new EmbedFieldBuilder()
                .WithName("Title")
                .WithValue($"{extraLink}[{video.Title}]({video.Url})")
                .WithIsInline(true);
            builder.AddField(titleFieldBuilder);

            var durationFieldBuilder = new EmbedFieldBuilder()
                .WithName("Duration")
                .WithValue($"``{video.Duration}``")
                .WithIsInline(true);
            builder.AddField(durationFieldBuilder);

            if (player.Queue.IsReapeating())
            {
                var repeatFieldBuilder = new EmbedFieldBuilder()
                    .WithName("Is Repeating")
                    .WithValue("Yes")
                    .WithIsInline(true);
                builder.AddField(repeatFieldBuilder);
            }

            int duration = video.GetDurationInSeconds();
            int elapsed = (int)(player.GetActualTrackPosition(lavaPlayer) / 1000);

            int percentage = elapsed * 100 / duration;
            int currentlyAt = percentage * 18 / 100;

            string part = "";
            for (int i = 0; i < 18; i++)
            {
                if (i == currentlyAt) part += "⚪";
                else part += "▬";
            }

            string paused = lavaPlayer.PlayerState == PlayerState.Paused ? " (**Paused**)\n" : "";
            string bar = $"{paused}``{TimeUtil.ToReadableTime(elapsed)}`` **[**{part}**]** ``{TimeUtil.ToReadableTime(video.GetDurationInSeconds() - elapsed)}``";

            var progressBarFieldBuilder = new EmbedFieldBuilder()
                .WithName("Progress")
                .WithValue(bar)
                .WithIsInline(false);
            builder.AddField(progressBarFieldBuilder);

            Embed embed = builder.Build();
            await ReplyAsync(embed: embed);
        }

        [Command("queue")]
        [Alias("q")]
        [RequireContext(ContextType.Guild)]
        [Summary("Displays the queue.")]
        public async Task QueueCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            var paginator = new DescriptionPaginator(Context.Channel, TimeSpan.FromSeconds(2 * 60), limitControlToUserId: Context.User.Id)
                .WithTitle("Audio Queue");

            if (player.IsCurrentlyPlaying)
            {
                if (!_lavaNode.HasPlayer(Context.Guild))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                    return;
                }

                var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);

                QueueItem item = player.CurrentItem;
                string emoji = "N/A";
                string title = "N/A";
                string url = "N/A";
                int duration = 0;
                switch (item.Provider)
                {
                    case ItemProvider.Youtube:
                        emoji = "<:youtubetest:883407960297050112>";
                        title = item.VideoInfo.Title;
                        url = item.VideoInfo.Url;
                        duration = item.VideoInfo.GetDurationInSeconds();
                        break;

                    case ItemProvider.Spotify:
                        emoji = "<:spotifytest:884731886100971520>";
                        title = item.SongInfo.Name;
                        url = item.SongInfo.ExternalUrl;
                        duration = item.SongInfo.DurationMs / 1000;
                        break;

                    case ItemProvider.Twitch:
                        emoji = "<:twitchtest:883407935802339349>";
                        break;
                }

                int elapsed = (int)(player.GetActualTrackPosition(lavaPlayer) / 1000);
                int percentage = elapsed * 100 / duration;

                string loopPart = player.Queue.IsReapeating() ? " **[Repeating]**" : "";
                string paused = lavaPlayer.PlayerState == PlayerState.Paused ? " (**Paused**)" : "";

                paginator.AddString($"**Now Playing**{loopPart}\n[{emoji}] [{title}]({url}) - `{TimeUtil.ToReadableTime(duration)}` ({percentage}%){paused}\n──────────────────────────\n\n");
            }
            
            if (player.Queue.Empty && !player.IsCurrentlyPlaying) paginator.AddString("Queue Empty :(");
            else
            {

                int totalSeconds = 0;
                int i = 0;
                foreach (QueueItem item in player.Queue)
                {
                    i++;

#pragma warning disable CS0219 // Variable is assigned but its value is never used
                    string iconUrl;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
                    string emoji = "N/A";
                    string title = "N/A";
                    string url = "N/A";
                    int seconds = 0;
                    switch (item.Provider)
                    {
                        case ItemProvider.Youtube:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";
                            emoji = "<:youtubetest:883407960297050112>";
                            title = item.VideoInfo.Title;
                            url = item.VideoInfo.Url;
                            seconds = item.VideoInfo.GetDurationInSeconds();
                            break;

                        case ItemProvider.Spotify:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
                            emoji = "<:spotifytest:884731886100971520>";
                            title = item.SongInfo.Name;
                            url = item.SongInfo.ExternalUrl;
                            seconds = item.SongInfo.DurationMs / 1000;
                            break;

                        case ItemProvider.Twitch:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";
                            emoji = "<:twitchtest:883407935802339349>";
                            break;
                    }

                    paginator.AddString($"[{emoji}] `{i}` - [{title}]({url}) - `{TimeUtil.ToReadableTime(seconds)}`\n\n");
                    totalSeconds += seconds;
                }

                paginator.WithFooter($"{player.Queue.Length} Songs  ●  {TimeUtil.ToSpotifyReadableTime(totalSeconds)}");
            }

            paginator.BuildPages(maxPerPage: 15);
            paginator.Run(Context.Client);
        }

        [RequireContext(ContextType.Guild)]
        [Command("history")]
        [Alias("h", "songhistory", "trackhistory")]
        [Summary("Displays the track history.")]
        public Task HistoryCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            var paginator = new DescriptionPaginator(Context.Channel, TimeSpan.FromSeconds(2 * 60), limitControlToUserId: Context.User.Id)
                .WithTitle("Audio History");

            QueueItem[] history = player.Queue.GetHistory().ToArray();

            if (history.Length <= 1) paginator.AddString("No history.");
            else
            {
                int totalSeconds = 0;
                int i = 0;
                foreach (QueueItem item in history.Skip(1))
                {
                    i++;

#pragma warning disable CS0219 // Variable is assigned but its value is never used
                    string iconUrl;
#pragma warning restore CS0219 // Variable is assigned but its value is never used
                    string emoji = "N/A";
                    string title = "N/A";
                    string url = "N/A";
                    int seconds = 0;
                    switch (item.Provider)
                    {
                        case ItemProvider.Youtube:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881741245939089448/youtube.png";
                            emoji = "<:youtubetest:883407960297050112>";
                            title = item.VideoInfo.Title;
                            url = item.VideoInfo.Url;
                            seconds = item.VideoInfo.GetDurationInSeconds();
                            break;

                        case ItemProvider.Spotify:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881927332082884689/spotify.png";
                            emoji = "<:spotifytest:884731886100971520>";
                            title = item.SongInfo.Name;
                            url = item.SongInfo.ExternalUrl;
                            seconds = item.SongInfo.DurationMs / 1000;
                            break;

                        case ItemProvider.Twitch:
                            iconUrl = "https://cdn.discordapp.com/attachments/627164432296050688/881925994418700338/twitch.png";
                            emoji = "<:twitchtest:883407935802339349>";
                            break;
                    }

                    paginator.AddString($"[{emoji}] `{i}` - [{title}]({url}) - `{TimeUtil.ToReadableTime(seconds)}`\n\n");
                    totalSeconds += seconds;
                }

                paginator.WithFooter($"{player.Queue.Length} Songs  ●  {TimeUtil.ToSpotifyReadableTime(totalSeconds)}");
            }

            paginator.BuildPages(maxPerPage: 15);
            paginator.Run(Context.Client);

            return Task.CompletedTask;

        }

        private const string _undoSummary = "If set to true, actions directly linked to the last action will also be undone. Check with the actions audit log command.";

        [RequireContext(ContextType.Guild)]
        [Command("undo")]
        [Summary("Undoes the previous queue action.")]
        public async Task UndoCommand([Summary(_undoSummary)]bool undoLinked = true)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);
            var prevActions = player.Queue.PreviousActions;

            if (prevActions.Count == 0)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Nothing to undo."));
                return;
            }

            bool takeAnyways = true;
            bool Pred(IQueueAction action)
            {
                try
                {
                    return (action.IsLinkedToPrevious || takeAnyways) && takeAnyways;
                }
                finally
                {
                    if (!action.IsLinkedToPrevious || !undoLinked) takeAnyways = false;
                }
            }

            IQueueAction[] actions = prevActions.Reverse<IQueueAction>().TakeWhile(Pred).ToArray();

            try
            {
                foreach (IQueueAction action in actions)
                {
                    try
                    {
                        player.Queue.UndoAction(action, undoLinked: false);
                    }
                    catch (IllegalUndoAction)
                    {
                        await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"Could not undo this action (`{action.ToString().Split('.').Last()}`) as the queue has progressed too far."));
                        return;
                    }
                }
            }
            finally
            {
                //prevActions.RemoveRange(prevActions.Count - actions.Length, actions.Length);
            }

            if (actions.Length == 1) await ReplyAsync(embed: MessageHelper.GetSuccessEmbed($"Undid action (`{actions[0].ToString().Split('.').Last()}`)"));
            else await ReplyAsync(embed: MessageHelper.GetSuccessEmbed($"Undid {actions.Length} actions ({string.Join(", ", actions.Select(a => $"`{a.ToString().Split('.').Last()}`"))})"));
        }

        [RequireContext(ContextType.Guild)]
        [Command("move")]
        [Summary("Moves a track from one position in the queue to another.")]
        public async Task MoveCommand(int from, int to)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            int queueLength = player.Queue.Length;
            if (from > queueLength || to > queueLength)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("``from`` and ``to`` must both be smaller than the current queue size."));
                return;
            }

            if (from == to)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("``from`` and ``to`` cannot be the same number."));
                return;
            }

            player.Queue.PerformAction(new MoveItemAction
            {
                Author = Context.User as SocketGuildUser,
                StartIndex = from - 1,
                EndIndex = to - 1,
            });

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Item moved from ``{from}`` to ``{to}``."));
        }

        [RequireContext(ContextType.Guild)]
        [Command("remove")]
        [Summary("Removes a track from the queue.")]
        public async Task RemoveCommand(int index)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);
            int queueLength = player.Queue.Length;

            if (index > queueLength)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("``index`` must be smaller than the queue size."));
                return;
            }

            player.Queue.PerformAction(new RemoveItemAction
            {
                Author = Context.User as SocketGuildUser,
                Index = index - 1,
            });

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Removed item at ``{index}``."));
        }

        [RequireContext(ContextType.Guild)]
        [Command("shuffle")]
        [Summary("Shuffles the queue.")]
        public async Task ShuffleCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (player.Queue.Length <= 1)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The queue must have more than one item to be shuffled."));
                return;
            }

            player.Queue.PerformAction(new ShuffleAction
            {
                Author = Context.User as SocketGuildUser,
            });

            await ReplyAsync(embed: MessageHelper.GetEmbed($"Successfully shuffled the queue."));
        }

        [RequireContext(ContextType.Guild)]
        [Command("actions")]
        [Alias("actionhistory", "auditlog")]
        [Summary("Displays action audit log.")]
        public Task ActionsCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            var paginator = new DescriptionPaginator(Context.Channel, TimeSpan.FromSeconds(2 * 60), limitControlToUserId: Context.User.Id)
                .WithTitle("Actions History");

            if (player.Queue.PreviousActions.Count == 0) paginator.AddString("No actions available");
            else
            {
                DateTime dt = DateTime.UtcNow;
                int i = 0;
                bool wasLinked = false;
                foreach (IQueueAction action in player.Queue.PreviousActions.Reverse<IQueueAction>())
                {
                    string icon;
                    if (wasLinked && action.IsLinkedToPrevious) icon = "┣";
                    else if (wasLinked) icon = "┗";
                    else if (action.IsLinkedToPrevious) icon = "┏";
                    else icon = " ឵឵ ឵឵ ●";

                    i++;
                    TimeSpan timeSpan = dt - action.PerformedAt;

                    string maybeLine = action.IsLinkedToPrevious == true ? "┃" : "";
                    paginator.AddString($"{icon} `{action.ToString().Split('.').Last()}` - {TimeUtil.ToReadableAgoTime((int)timeSpan.TotalSeconds)} ({action.Author.Mention})\n{maybeLine}\n");

                    wasLinked = action.IsLinkedToPrevious;
                }

                paginator.WithFooter($"{player.Queue.PreviousActions.Count} Actions");
            }
            
            paginator.BuildPages(maxPerPage: 15);
            paginator.Run(Context.Client);

            return Task.CompletedTask;
        }

        public async Task Clear(int startIndex, int endIndex)
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (endIndex == -1) endIndex = player.Queue.Length;

            if (startIndex == 1 && endIndex == 0)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The queue is already empty"));
                return;
            }

            if (startIndex < 1)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`startIndex` cannot be smaller than 1."));
                return;
            }

            if (startIndex > endIndex)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`startIndex` cannot be bigger than `endIndex`"));
                return;
            }

            if (endIndex - 1 > player.Queue.Length - 1)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`endIndex` cannot be bigger than the queue size."));
                return;
            }

            int prevLength = player.Queue.Length;

            player.Queue.PerformAction(new ClearQueueAction
            {
                Author = Context.User as SocketGuildUser,
                StartIndex = startIndex - 1,
                EndIndex = endIndex - 1,
            });

            if (startIndex - 1 == 0 && endIndex == prevLength) await ReplyAsync(embed: MessageHelper.GetSuccessEmbed("Successfully cleared the queue."));
            else if (startIndex - 1 != 0 && endIndex == prevLength) await ReplyAsync(embed: MessageHelper.GetSuccessEmbed($"Successfully cleared the queue from position `{startIndex}`."));
            else await ReplyAsync(embed: MessageHelper.GetSuccessEmbed($"Successfully cleared the queue from position {startIndex} to {endIndex}"));
        }

        [RequireContext(ContextType.Guild)]
        [Command("clear")]
        [Alias("c", "qc", "qclear", "queuec", "queueclear", "clearqueue", "clearq")]
        [Summary("Clears the queue.")]
        public async Task ClearCommand(int startIndex = 1, int endIndex = -1)
        {
            await Clear(startIndex, endIndex);
        }

        [RequireContext(ContextType.Guild)]
        [Command("repeat")]
        [Alias("loop")]
        [Summary("Toggles looping of the current track.")]
        public async Task RepeatCommand()
        {
            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (!player.Queue.IsReapeating())
            {
                player.Queue.PerformAction(new RepeatAction
                {
                    Author = Context.User as SocketGuildUser,
                });

                await ReplyAsync(embed: MessageHelper.GetEmbed("Playback will now repeat."));
            }
            else
            {
                player.Queue.PerformAction(new StopRepeatAction
                {
                    Author = Context.User as SocketGuildUser,
                });

                await ReplyAsync(embed: MessageHelper.GetEmbed("Playback will no longer repeat."));
            }
        }

        [RequireContext(ContextType.Guild)]
        [Command("lyrics")]
        [Summary("Sends lyrics to a track.")]
        public async Task LyricsCommand([Summary("The queue position of the track to get lyrics for.")]int position = 0)
        {
            if (!_lavaNode.HasPlayer(Context.Guild))
            {
                Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                await ReplyAsync(embed: errEmbed);
                return;
            }

            var lavaPlayer = _lavaNode.GetPlayer(Context.Guild);
            if (lavaPlayer.PlayerState == PlayerState.None)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed($"The bot is not connected."));
                return;
            }
            else if (lavaPlayer.PlayerState == PlayerState.Stopped)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("The bot is currently not playing anything."));
                return;
            }

            Player player = _audioService.GetOrCreatePlayer(Context.Guild);

            if (position < 0 || position > player.Queue.Length)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Position must be equal or larger than 0 and less than the queue size."));
                return;
            }

            if (position == 0 && player.CurrentItem == null)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("No track is currently playing."));
                return;
            }

            QueueItem item = position == 0 ? player.CurrentItem : player.Queue.SelectItem(position - 1);

            //LavaTrack track = lavaPlayer.Track;
            //if (track == null)
            //{
            //    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("No track found."));
            //    return;
            //}

            string url;
            try
            {
                url = item.Provider switch
                {
                    ItemProvider.Youtube => await item.VideoInfo.FetchGeniusLyricsUrl(),
                    ItemProvider.Spotify => await item.SongInfo.FetchGeniusLyricsUrl(),
                    _ => "N/A",
                };
            }
            catch (LyricsNotFoundException)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("Couldn't find any genius lyrics for the current track."));
                return;
            }

            string part = item.Provider == ItemProvider.Youtube ? item.VideoInfo.Title : item.SongInfo.Name;
            await ReplyAsync(embed: MessageHelper.GetEmbed($"Lyrics for {part}: {url}"));
        }

        //[Hidden]
        [RequireContext(ContextType.Guild)]
        [Group("filter")]
        [Alias("filters")]
        [Summary("Audio filter related commands.")]
        public class FilterModule1 : ModuleBase<SocketCommandContext>
        {
            private LavaNode __lavaNode;
            private AudioService __audioService;

            public FilterModule1(LavaNode lavaNode, AudioService audioService)
            {
                __lavaNode = lavaNode;
                __audioService = audioService;
            }

            [Command]
            [Summary("Displays currently active audio filters.")]
            public async Task DefaultCommand()
            {
                await CurrentCommand();
            }

            [Command("help")]
            [Summary("Displays filter help.")]
            public async Task HelpCommand()
            {
                await ReplyAsync(@"```
!filter clear
- Clears the currently actived filters.

!filter current|active|info
- Displays currently active filters.

------------------------------------------------

!filter rotation [rotationHz = 0.2]
- Activates rotation filter.

!filter channelmix [leftToLeft = 1.0] [leftToRight = 0.0] [rightToLeft = 0.0] [rightToRight = 1.0]
- Activates channel mix filter.

!filter mono
- Sets audio channels to mono.

!filter stereo
- Sets audio channels to stereo.

!filter karaoke [level = 1.0] [monoLevel = 1.0] [filterBand = 220.0] [filterWidth = 100.0]
- Activates karaoke filter.

!filter timescale [speed = 1.0] [pitch = 1.0] [rate = 1.0]
- Activates timescale filter.

!filter distortion [sinOffset = 0.0] [sinScale = 1.0] [cosOffset = 0.0] [cosScale = 1.0] [tanOffset = 0.0] [tanScale = 1.0] [offset = 0.0] [scale = 1.0]
- Activates distortion filter.

!filter lowpass [smoothing = 20.0]
- Activates lowpass filter.

!filter vibrato [frequency = 2.0 (0 < x <= 14)] [depth = 0.5 (0 < x <= 1)]
- Activates vibrato filter.

!filter tremolo [frequency = 2.0 (0 < x)] [depth = 0.5 (0 < x <= 1)]
- Activates tremolo filter.```");
            }

            [Command("clear")]
            [Alias("c")]
            [Summary("Clears the audio filters.")]
            public async Task ClearCommand()
            {
                if (!__lavaNode.HasPlayer(Context.Guild))
                {
                    Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                    await ReplyAsync(embed: errEmbed);
                    return;
                }

                var lavaPlayer = __lavaNode.GetPlayer(Context.Guild);

                IFilter[] emptyFilters = new IFilter[0];
                __audioService.ActiveFilters.Clear();
                await lavaPlayer.ApplyFiltersAsync(emptyFilters);

                await ReplyAsync(embed: MessageHelper.GetEmbed($"Cleared filters. _This might take a few seconds to apply..._"));
                return;
            }

            [Command("current")]
            [Alias("active", "info")]
            [Summary("Displays currently active audio filters.")]
            public async Task CurrentCommand()
            {
                string snakeCase(string input)
                {
                    return input[0].ToString().ToLower() + input[1..];
                }

                if (!__lavaNode.HasPlayer(Context.Guild))
                {
                    Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                    await ReplyAsync(embed: errEmbed);
                    return;
                }

                var lavaPlayer = __lavaNode.GetPlayer(Context.Guild);

                List<string> activeFilters = new List<string>();
                foreach (IFilter filter in __audioService.ActiveFilters)
                {
                    string joined = string.Join(", ", filter.GetType().GetProperties().Select(prop => $"{snakeCase(prop.Name)}: {prop.GetValue(filter)}"));
                    activeFilters.Add($"{filter.GetType().Name}({joined})");
                }

                string activeText = "None";
                if (activeFilters.Count > 0)
                    activeText = string.Join("\n", activeFilters);

                await ReplyAsync(embed: MessageHelper.GetEmbed($"**Currently active filters:\n**```\n{activeText}```"));
            }

            [Command("rotation")]
            [Summary("Activates rotation filter (8d audio).")]
            public async Task RotationCommand([Summary("`0.2` is roughly the same as 8d videos on YouTube.")]double rotationHz = 0.2)
            {
                await ApplyFilter(new RotationFilter
                {
                    Hertz = rotationHz
                });
            }

            [Command("channelmix")]
            [Alias("stereo")]
            [Summary("Activates the channel mix filter (audio channels).")]
            public async Task ChannelMixCommand(double leftToLeft = 1.0, double leftToRight = 0.0, double rightToLeft = 0.0, double rightToRight = 1.0)
            {
                await ApplyFilter(new ChannelMixFilter
                {
                    LeftToLeft = leftToLeft,
                    LeftToRight = leftToRight,
                    RightToLeft = rightToLeft,
                    RightToRight = rightToRight,
                });
            }

            [Command("mono")]
            [Summary("Sets audio to use a single channel.")]
            public async Task MonoChannelMixCommand()
            {
                double leftToLeft = 0.5;
                double leftToRight = 0.5;
                double rightToLeft = 0.5;
                double rightToRight = 0.5;

                await ApplyFilter(new ChannelMixFilter
                {
                    LeftToLeft = leftToLeft,
                    LeftToRight = leftToRight,
                    RightToLeft = rightToLeft,
                    RightToRight = rightToRight,
                });
            }

            [Command("karaoke")]
            [Summary("Activates karaoke filter (scuffed).")]
            public async Task KaraokeCommand(double level = 1.0, double monoLevel = 1.0, double filterBand = 220.0, double filterWidth = 100.0)
            {
                await ApplyFilter(new KarokeFilter
                {
                    Level = level,
                    MonoLevel = monoLevel,
                    FilterBand = filterBand,
                    FilterWidth = filterWidth,
                });
            }

            [Command("timescale")]
            [Summary("Activates timescale filter (Speed, Pitch).")]
            public async Task TimescaleCommand(double speed = 1.0, double pitch = 1.0, double rate = 1.0)
            {
                await ApplyFilter(new TimescaleFilter
                {
                    Speed = speed,
                    Pitch = pitch,
                    Rate = rate,
                });
            }

            [Command("distortion")]
            [Summary("Activates distortion filter.")]
            public async Task DistortionCommand(int sinOffset = 0, int sinScale = 1, int cosOffset = 0, int cosScale = 1, int tanOffset = 0, int tanScale = 1, int offset = 0, int scale = 1)
            {
                await ApplyFilter(new DistortionFilter
                {
                    SinOffset = sinOffset,
                    SinScale = sinScale,
                    CosOffset = cosOffset,
                    CosScale = cosScale,
                    TanOffset = tanOffset,
                    TanScale = tanScale,
                    Offset = offset,
                    Scale = scale,
                });
            }

            [Command("lowpass")]
            [Summary("Activates lowpass filter.")]
            public async Task LowPassCommand(double smoothing = 20.0)
            {
                await ApplyFilter(new LowPassFilter
                {
                    Smoothing = smoothing,
                });
            }

            [Command("vibrato")]
            [Summary("Activates vibrato filter.")]
            public async Task VibratoCommand([Summary("Must be 0 < x <= 14")] double frequency = 2.0, [Summary("Must be 0 < x <= 1")]double depth = 0.5)
            {
                if (!(0 < frequency && frequency <= 14))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`frequency` must be 0 < x <= 14"));
                    return;
                }

                if (!(0 < depth && depth <= 14))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`depth` must be 0 < x <= 1"));
                    return;
                }

                await ApplyFilter(new VibratoFilter
                {
                    Frequency = frequency,
                    Depth = depth,
                });
            }

            [Command("tremolo")]
            [Summary("Activates tremolo filter.")]
            public async Task TremoloCommand([Summary("Must be 0 < x")]double frequency = 2.0, [Summary("Must be 0 < x <= 1")]double depth = 0.5)
            {
                if (!(0 < frequency))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`frequency` must be 0 < x"));
                    return;
                }

                if (!(0 < depth && depth <= 14))
                {
                    await ReplyAsync(embed: MessageHelper.GetErrorEmbed("`depth` must be 0 < x <= 1"));
                    return;
                }

                await ApplyFilter(new TremoloFilter
                {
                    Frequency = frequency,
                    Depth = depth,
                });
            }

            public async Task ApplyFilter(IFilter filter)
            {
                if (!__lavaNode.HasPlayer(Context.Guild))
                {
                    Embed errEmbed = MessageHelper.GetErrorEmbed($"The bot is not connected.");
                    await ReplyAsync(embed: errEmbed);
                    return;
                }

                var lavaPlayer = __lavaNode.GetPlayer(Context.Guild);

                bool overridden = await __audioService.ApplyFilterAsync(lavaPlayer, filter);
                string extra = overridden ? "Replaced" : "Applied";
                await ReplyAsync(embed: MessageHelper.GetEmbed($"{extra} {filter.GetType().Name}. _This might take a few seconds to apply..._"));
            }
        }

        [Command("vpsstats")]
        [Alias("vps")]
        [Summary("Displays statistics about the vps.")]
        public async Task LavaStatsCommand()
        {
            StatsEventArgs args = _audioService.LastReceivedLavaplayerStats;
            if (args == null)
            {
                await ReplyAsync(embed: MessageHelper.GetErrorEmbed("No stats available"));
                return;
            }

            var builder = new EmbedBuilder()
                .WithTitle("Lava Stats");

            var cpuCoresFieldBuilder = new EmbedFieldBuilder()
                .WithName("CPU Cores")
                .WithValue(args.Cpu.Cores.ToString())
                .WithIsInline(true);
            builder.AddField(cpuCoresFieldBuilder);

            var cpuLoadFieldBuilder = new EmbedFieldBuilder()
                .WithName("CPU Load")
                .WithValue(args.Cpu.SystemLoad.ToString("0.00") + "%")
                .WithIsInline(true);
            builder.AddField(cpuLoadFieldBuilder);

            var cpuLavaLinkLoadFieldBuilder = new EmbedFieldBuilder()
                .WithName("CPU LavaLink Load")
                .WithValue(args.Cpu.LavalinkLoad.ToString("0.00") + "%")
                .WithIsInline(true);
            builder.AddField(cpuLavaLinkLoadFieldBuilder);

            var alloMemoryFieldBuilder = new EmbedFieldBuilder()
                .WithName("LavaLink Allocated Memory")
                .WithValue(GetAsMegabyte(args.Memory.Allocated))
                .WithIsInline(true);
            builder.AddField(alloMemoryFieldBuilder);

            var usedMemoryFieldBuilder = new EmbedFieldBuilder()
                .WithName("LavaLink Used Memory")
                .WithValue(GetAsMegabyte(args.Memory.Used))
                .WithIsInline(true);
            builder.AddField(usedMemoryFieldBuilder);

            var uptimeFieldBuilder = new EmbedFieldBuilder()
                .WithName("LavaLink Uptime")
                .WithValue(TimeUtil.ToFullReadableTime((int)args.Uptime.TotalSeconds).ToString())
                .WithIsInline(true);
            builder.AddField(uptimeFieldBuilder);

            await ReplyAsync(embed: builder.Build());
        }
    }
}
