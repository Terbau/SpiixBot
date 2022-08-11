using Discord;
using Discord.Audio;
using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue;
using SpiixBot.Services;
using SpiixBot.Util;
using SpiixBot.Util.Encoder;
using SpiixBot.Youtube;
using SpiixBot.Youtube.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace SpiixBot.Modules.Audio.Player
{
    public class Player
    {
        public SocketGuild Guild { get; set; }
        public ItemQueue Queue { get; set; }
        public QueueItem CurrentItem { get; set; }
        public bool IsCurrentlyPlaying { get; set; }
        public AudioController Controller { get; set; }
        public bool WasDisconnected { get; set; } = false;
        public ISocketMessageChannel TextChannel { get; set; }

        private Task _nextItemFetchingTask;
        private Task _disconnectTask = null;
        private CancellationTokenSource _disconnectCts = null;
        private CancellationTokenSource _singleItemCts;
        private CancellationTokenSource _sessionCts;
        private AudioService _audioService;
        private YoutubeService _youtubeService;
        private LavaNode _lavaNode;
        private int _retryingLoadCounter = 0;

        public Player(AudioService audioService, YoutubeService youtubeService, LavaNode lavaNode)
        {
            _audioService = audioService;
            _youtubeService = youtubeService;
            _lavaNode = lavaNode;

            _lavaNode.OnTrackEnded += OnTrackEnded;
            _lavaNode.OnTrackException += OnTrackException;
            _lavaNode.OnTrackStuck += OnTrackStuck;
        }

        public async Task PlayNextTrack(LavaPlayer player)
        {
            Console.WriteLine("Play next track requested.");

            if (Queue.IsReapeating() && Queue.HistoryLength > 0) Queue.DecrementCurrentIndex(1);
            QueueItem item = Queue.GetNextItem();

            await PlayTrack(player, item);
        }

        public void CancelDisconnect()
        {
            if (_disconnectCts != null && !_disconnectCts.IsCancellationRequested)
            {
                _disconnectCts.Cancel();
                _disconnectTask = null;
                _disconnectCts = null;
            }
        }

        public async Task PlayTrack(LavaPlayer player, QueueItem item, bool isRetry = false)
        {
            CurrentItem = item;
            if (isRetry)
            {
                _retryingLoadCounter++;
            }
            else
            {
                _retryingLoadCounter = 0;
            }

            CancelDisconnect();

            if (_nextItemFetchingTask != null && !_nextItemFetchingTask.IsCompleted) await _nextItemFetchingTask;
            //if (item.HasDummyVideoInfo || item.VideoInfo.StreamUrl == null) await ProcessItem(item);
            if (item.HasDummyVideoInfo) await ProcessItem(item);

            // Do this to decrease waiting time between songs
            if (!Queue.Empty)
            {
                QueueItem nextItem = Queue.GetNextItem(false);
                if (nextItem.HasDummyVideoInfo) _nextItemFetchingTask = ProcessItem(nextItem);
            }

            Video video = item.VideoInfo;
            string lavaHash = TrackEncoder.Encode(
                video.Title,
                "",
                video.GetDurationInSeconds() * 1000,
                video.Id,
                false,
                video.Url,
                position: item.SeekPosition * 1000
            );

            LavaTrack track = new LavaTrack(lavaHash, "", "", "", "", TimeSpan.Zero, 0, true, false, default);

            //if (player.PlayerState == PlayerState.Playing || player.PlayerState == PlayerState.Paused) await player.StopAsync();

            await player.PlayAsync(track);
            Console.WriteLine($"[Player] Playing next track {video.Title}");
        }

        public void ScheduleDisconnect(int milliseconds)
        {
            CancelDisconnect();
            _disconnectTask = Task.Run(async () =>
            {
                _disconnectCts = new CancellationTokenSource();
                await Task.Delay(milliseconds, _disconnectCts.Token);

                if (!_disconnectCts.IsCancellationRequested)
                {
                    var lavaPlayer = _lavaNode.GetPlayer(Guild);
                    if (lavaPlayer.PlayerState != PlayerState.None)
                    {
                        Console.WriteLine("Disconnect reason: Timeout");
                        Clear();
                        await _lavaNode.LeaveAsync(lavaPlayer.VoiceChannel);
                    }
                }
            });
        }

        public async Task PlayNextTrackIfExists(LavaPlayer player)
        {
            if (!Queue.Empty) await PlayNextTrack(player);
            else
            {
                IsCurrentlyPlaying = false;

                if (TextChannel != null)
                {
                    Task task = Task.Run(async () => await TextChannel.SendMessageAsync(embed: MessageHelper.GetEmbed(description: $"Queue finished. _Disconnect timer started (15m)_")));
                }

                Console.WriteLine("Scheduling disconnect");
                ScheduleDisconnect(15 * 60 * 1000);
            }
        }

        public async Task OnTrackEnded(TrackEndedEventArgs args)
        {
            Console.WriteLine($"[Track Ended] {args.Track.Title} (ID: {args.Track.Id}) | Reason: {args.Reason}");
            if (args.Reason == TrackEndReason.LoadFailed)  
            {
                if (_retryingLoadCounter != 1)  // Ignore if retrying has already been attempted.
                {
                    await PlayTrack(args.Player, CurrentItem, isRetry: true);
                    return;
                }

                _retryingLoadCounter = 0;
            }

            if (!(args.Reason == TrackEndReason.Finished || args.Reason == TrackEndReason.Replaced))
            {
                IsCurrentlyPlaying = false;
                return;
            }

            if (args.Reason == TrackEndReason.Finished)
            {
                await PlayNextTrackIfExists(args.Player);
            }
        }

        public async Task OnTrackException(TrackExceptionEventArgs args)
        {
            Console.WriteLine($"[Track Exception] {args.Track.Title} (ID: {args.Track.Id}) | Exception message: {args.Exception.Message}");

            if (TextChannel != null)
            {
                string part = Queue.Length > 0 ? " Playing next track..." : "";
                Task task = Task.Run(async () => await TextChannel.SendMessageAsync(embed: MessageHelper.GetErrorEmbed($"Track ({args.Track.Title}) could not be played because of an error.{part}")));
            }

            await PlayNextTrackIfExists(args.Player);
        }

        public async Task OnTrackStuck(TrackStuckEventArgs args)
        {
            Console.WriteLine($"[Track Stuck] {args.Track.Title} (ID: {args.Track.Id})");
            
            if (TextChannel != null)
            {
                Task task = Task.Run(async () => await TextChannel.SendMessageAsync(embed: MessageHelper.GetEmbed(description: $"Track ({args.Track.Title}) got stuck. Restarting the playback now...")));
            }
            
            await PlayTrack(args.Player, CurrentItem);
        }

        public double GetActualTrackPosition(LavaPlayer player)
        {
            DateTime datetime = player.LastUpdate.UtcDateTime;
            TimeSpan diff = DateTime.UtcNow - datetime;
            double totalMilliseconds = (long)diff.TotalMilliseconds;

            TimeSpan position = player.Track.Position;
            double actualPosition = totalMilliseconds + position.TotalMilliseconds;

            return actualPosition;
        }

        public double GetTrackDurationLeft(LavaPlayer player)
        {
            return player.Track.Duration.TotalMilliseconds - GetActualTrackPosition(player);
        }

        public void SkipSingleItem()
        {
            if (_singleItemCts != null && !_singleItemCts.IsCancellationRequested) _singleItemCts.Cancel();
        }

        public void Stop()
        {
            if (_sessionCts != null && !_sessionCts.IsCancellationRequested) _sessionCts.Cancel();
            if (_singleItemCts != null && !_singleItemCts.IsCancellationRequested) _singleItemCts.Cancel();
        }

        private async Task FixVideoInfo(QueueItem item)
        {
            if (item.Provider == ItemProvider.Spotify)
            {
                string query = $"{item.SongInfo.Artist} - {item.SongInfo.Name} Audio";

                List<Video> videos;
                try
                {
                    videos = await _youtubeService.Client.SearchVideosAsync(query, limit: 1);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    item.IsBroken = true;
                    return;
                }

                Video video = videos[0];
                item.VideoInfo = video;

                item.HasDummyVideoInfo = false;
            }
        }

        private async Task ProcessItem(QueueItem item)
        {
            await FixVideoInfo(item);
            //await item.VideoInfo.GetStreamUrlAsync(_youtubeService.Client);
        }

        public async Task RunLavaLink(IVoiceChannel voiceChannel)
        {
            IsCurrentlyPlaying = true;

            LavaPlayer player;
            if (!_lavaNode.HasPlayer(Guild))
            {
                player = await _lavaNode.JoinAsync(voiceChannel);
            }
            else player = _lavaNode.GetPlayer(Guild);

            WasDisconnected = false;

            await PlayNextTrack(player);
        }

        public void Clear()
        {
            TextChannel = null;
            IsCurrentlyPlaying = false;
            WasDisconnected = true;
            Queue.UnsafeClearAll();
            Queue.PreviousActions.Clear();
            Queue.UndoneActions.Clear();
            Queue.ResetCurrentIndex();
        }
    }
}
