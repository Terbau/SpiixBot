using Discord;
using Discord.Audio;
using Discord.WebSocket;
using SpiixBot.Modules.Audio;
using SpiixBot.Modules.Audio.Player;
using SpiixBot.Modules.Audio.Queue;
using SpiixBot.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;
using Victoria.Filters;

namespace SpiixBot.Services
{
    public class AudioService
    {
        public float Volume { get; set; } = 1f;
        public StatsEventArgs LastReceivedLavaplayerStats { get; private set; }
        public List<IFilter> ActiveFilters { get; } = new List<IFilter>();

        private readonly DiscordSocketClient _client;
        private readonly ConcurrentDictionary<ulong, Player> _guildSessions = new ConcurrentDictionary<ulong, Player>();
        private YoutubeService _youtubeService;
        private LavaNode _lavaNode;

        public AudioService(DiscordSocketClient client, YoutubeService youtubeService, LavaNode lavaNode)
        {
            _client = client;
            _youtubeService = youtubeService;
            _lavaNode = lavaNode;

            _lavaNode.OnStatsReceived += OnStatsReceived;

            _client.UserVoiceStateUpdated += (SocketUser user, SocketVoiceState beforeVoiceState, SocketVoiceState afterVoiceState) =>
            {
                SocketGuildUser guildUser = user as SocketGuildUser;
                if (guildUser != null)
                {
                    if (guildUser.Id == guildUser.Guild.CurrentUser.Id)
                    {
                        if (beforeVoiceState.VoiceChannel != null && afterVoiceState.VoiceChannel == null)
                        {
                            Task.Run(async () => await SoftStopPlayer(guildUser.Guild, beforeVoiceState.VoiceChannel));
                        }
                    }
                }

                return Task.CompletedTask;
            };
        }

        public Task OnStatsReceived(StatsEventArgs stats)
        {
            LastReceivedLavaplayerStats = stats;

            return Task.CompletedTask;
        }

        public async Task<bool> ApplyFilterAsync(LavaPlayer player, IFilter filter, double volume = 1.0, params EqualizerBand[] equalizerBands)
        {
            bool overridden = false;

            for (int i = 0; i < ActiveFilters.Count; i++)
            {
                if (ActiveFilters[i].GetType() == filter.GetType())
                {
                    ActiveFilters.RemoveAt(i);
                    break;
                }
            }

            ActiveFilters.Add(filter);
            await player.ApplyFilterAsync(filter, volume, equalizerBands);

            return overridden;
        }

        public void CleanupAndRemovePlayer(ulong guildId)
        {
            if (_guildSessions.TryGetValue(guildId, out Player player))
            {
                player.CancelDisconnect();
                player.Clear();

                _guildSessions.Remove(guildId, out _);
            }
        }

        public async Task SoftStopPlayer(SocketGuild guild, IVoiceChannel voiceChannel, string reason = "Forceful disconnect")
        {
            Player player = GetOrCreatePlayer(guild);
            player.CancelDisconnect();
            await _lavaNode.LeaveAsync(voiceChannel);

            ISocketMessageChannel textChannel = player.TextChannel;
            CleanupAndRemovePlayer(guild.Id);

            Console.WriteLine($"[Guild Audio Shutdown] Guild ID: {guild.Id} | Reason: {reason}");

            if (textChannel != null) await textChannel.SendMessageAsync(embed: MessageHelper.GetEmbed(description: "Queue was cleared and playback stopped because the bot disconnected."));
        }

        public Player GetOrCreatePlayer(SocketGuild guild, ISocketMessageChannel textChannel = null)
        {
            if (!_guildSessions.TryGetValue(guild.Id, out Player collection))
            {
                collection = new Player(this, _youtubeService, _lavaNode)
                {
                    Guild = guild,
                    Queue = new ItemQueue(),
                    TextChannel = textChannel,
                };
                _guildSessions.TryAdd(guild.Id, collection);
            }

            if (collection.TextChannel == null) collection.TextChannel = textChannel;

            return collection;
        }

        public void RemoveGuildCollection(SocketGuild guild)
        {
            _guildSessions.TryRemove(guild.Id, out Player collection);
        }

        public bool IsCurrentlyBeingUsed(SocketGuild guild)
        {
            return guild.IsConnected;
        }

        public async Task<IAudioClient> ConnectToChannelAsync(IVoiceChannel channel)
        {
            return await channel.ConnectAsync();
        }

        public async Task DisconnectFromChannelAsync(IVoiceChannel channel)
        {
            await channel.DisconnectAsync();
        }

        public async Task<IAudioClient> JoinChannel(SocketVoiceChannel channel)
        {
            if (channel != channel.Guild.CurrentUser.VoiceChannel || channel.Guild.AudioClient == null) return await channel.ConnectAsync();
            return channel.Guild.AudioClient;
        }
    }
}
