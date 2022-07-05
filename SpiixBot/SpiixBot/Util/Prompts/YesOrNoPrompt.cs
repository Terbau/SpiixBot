using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SpiixBot.Util.Prompts
{
    public class YesOrNoPrompt : IAsyncDisposable
    {
        public readonly SocketCommandContext Context;
        public string Message;
        public readonly int Timeout;
        public bool DeleteOriginal;

        private readonly Emote _yesReaction = Emote.Parse("<:yes:887445437919936633>");
        private readonly Emote _noReaction = Emote.Parse("<:no:887445417342693386>");

        private RestUserMessage _base;
        private bool _hasRegistered = false;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private TaskCompletionSource<bool> _taskcs;
        private bool _hasBeenDisposed = false;

        public YesOrNoPrompt(SocketCommandContext context, string message = "Are you sure you want to do this?", int seconds = 60, bool deleteOriginal = false)
        {
            Context = context;
            Message = message;
            Timeout = seconds;
            DeleteOriginal = deleteOriginal;
        }

        public async Task ReactionAddedHandler(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> channel, SocketReaction reaction)
        {
            if (reaction.UserId == Context.Client.CurrentUser.Id) return;
            if (reaction.MessageId != _base.Id) return;

            if (reaction.Emote.ToString() == _yesReaction.ToString())
            {
                _taskcs.SetResult(true);
            }
            else if (reaction.Emote.ToString() == _noReaction.ToString())
            {
                _taskcs.SetResult(false);
            }
            else return;

            _cts.Cancel();
            await DisposeAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_hasBeenDisposed) return;
            _hasBeenDisposed = true;

            if (_hasRegistered)
            {
                Context.Client.ReactionAdded -= ReactionAddedHandler;
            }

            var textChannel = Context.Channel as SocketTextChannel;
            var messages = new List<IMessage>() { _base };

            if (DeleteOriginal) messages.Add(Context.Message);

            await textChannel.DeleteMessagesAsync(messages);
        }

        public async Task StartTimeout()
        {
            await Task.Delay(Timeout * 1000, _cts.Token);

            if (!_cts.IsCancellationRequested) _taskcs.SetException(new TimeoutException());
        }

        public async Task<bool> Run()
        {
            _taskcs = new TaskCompletionSource<bool>();

            var _ = Task.Run(StartTimeout);
            Context.Client.ReactionAdded += ReactionAddedHandler;

            _base = await Context.Channel.SendMessageAsync(embed: MessageHelper.GetEmbed(Message));
            try
            {
                await _base.AddReactionsAsync(new IEmote[2] { _yesReaction, _noReaction });
            }
            catch { }

            bool res = await _taskcs.Task;
            return res;
        }
    }
}
