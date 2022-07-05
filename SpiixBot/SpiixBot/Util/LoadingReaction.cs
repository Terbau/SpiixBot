using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Util
{
    public class LoadingReaction : IDisposable
    {
        SocketCommandContext Context { get; }

        private bool _hasAdded = false;

        public LoadingReaction(SocketCommandContext context)
        {
            Context = context;
            Task.Run(StartLoading);
        }

        private async Task StartLoading()
        {
            await Context.Message.AddReactionAsync(Emote.Parse("<a:loading:886722801162154014>"));
            _hasAdded = true;
        }

        private async Task StopLoading()
        {
            await Context.Message.RemoveReactionAsync(Emote.Parse("<a:loading:886722801162154014>"), Context.Client.CurrentUser);
        }

        public void Dispose()
        {
            if (_hasAdded) Task.Run(StopLoading);
        }
    }
}
