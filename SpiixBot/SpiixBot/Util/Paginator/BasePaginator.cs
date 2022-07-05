using Discord;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiixBot.Util.Paginator
{
    public abstract class BasePaginator : IPaginator
    {
        public List<IPaginatorPage> Pages { get; }

        private ISocketMessageChannel _channel;
        private TimeSpan _timeout;
        private ulong? _limitControlToUserId;

        private Emoji[] _emojies = new Emoji[]
        {
            new Emoji("\U000025c0"),  // left
            new Emoji("\U000025b6"),  // right
            new Emoji("\U000026d4"),  // stop
        };
        private RestUserMessage _baseMessage;
        private int _currentPage = 0;
        private Task _runTask;
        private DiscordSocketClient _client;

        private CancellationTokenSource _scheduleDisposeSource;
        private Task _scheduleDisposeTask;

        public BasePaginator(ISocketMessageChannel channel, TimeSpan timeout, ulong? limitControlToUserId = null)
        {
            _channel = channel;
            _timeout = timeout;
            _limitControlToUserId = limitControlToUserId;

            Pages = new List<IPaginatorPage>();
        }

        public void AddPage(IPaginatorPage page)
        {
            Pages.Add(page);
        }

        public void RemovePage(IPaginatorPage page)
        {
            Pages.Remove(page);
        }

        public void RemovePageAt(int index)
        {
            Pages.RemoveAt(index);
        }

        public async Task EditToPage(int index)
        {
            IPaginatorPage page = Pages[index];
            Embed embed = page.ConstructPage();

            await _baseMessage.ModifyAsync(x => { x.Embed = embed; });
        }

        public async Task OnReactionChange(Cacheable<IUserMessage, ulong> cachedMessage, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.UserId == _client.CurrentUser.Id) return;
            if (reaction.MessageId != _baseMessage.Id) return;
            if (_limitControlToUserId != null && _limitControlToUserId != reaction.UserId) return;
            if (!_emojies.Contains(reaction.Emote)) return;

            ResetScheduleDispose();

            int prev = _currentPage;

            switch ((PaginatorControls)Array.FindIndex(_emojies, emoji => emoji.ToString() == reaction.Emote.ToString()))
            {
                case PaginatorControls.Left:
                    _currentPage = _currentPage - 1 >= 0 ? _currentPage - 1 : Pages.Count - 1; 
                    break;
                case PaginatorControls.Right:
                    _currentPage = _currentPage + 1 <= Pages.Count - 1 ? _currentPage + 1 : 0;
                    break;
                case PaginatorControls.Stop:
                    Dispose();
                    return;
            }

            if (_currentPage != prev) await EditToPage(_currentPage);
        }

        private async Task DisposeAsyncAfter(int milliseconds, CancellationToken token)
        {
            await Task.Delay(milliseconds, token);

            if (token.IsCancellationRequested) return;

            Dispose();
        }

        private void ResetScheduleDispose()
        {
            if (_scheduleDisposeTask != null)
            {
                _scheduleDisposeSource.Cancel();
            }

            var cts = new CancellationTokenSource();
            _scheduleDisposeSource = cts;
            _scheduleDisposeTask = DisposeAsyncAfter((int)_timeout.TotalMilliseconds, cts.Token);
        }

        private async Task ActualRun()
        {
            if (Pages.Count == 1)
            {
                _baseMessage = await _channel.SendMessageAsync(embed: Pages[0].ConstructPage());
                return;
            }

            ResetScheduleDispose();

            // Set up event listeners
            _client.ReactionAdded += OnReactionChange;
            _client.ReactionRemoved += OnReactionChange;

            _baseMessage = await _channel.SendMessageAsync(embed: Pages[0].ConstructPage());
            await _baseMessage.AddReactionsAsync(_emojies);
        }

        public void Run(DiscordSocketClient client)
        {
            if (Pages.Count == 0) throw new ArgumentException("No pages registered to the current paginator.");

            _client = client;
            _runTask = ActualRun();
        }

        public void Dispose()
        {
            _client.ReactionAdded -= OnReactionChange;
            _client.ReactionRemoved -= OnReactionChange;

            _scheduleDisposeSource.Cancel();

            Task.Run(async () =>
            {
                await _baseMessage.RemoveAllReactionsAsync();
                //await _channel.SendMessageAsync(text: "Paginator stopped.");
            });
        }
    }
}