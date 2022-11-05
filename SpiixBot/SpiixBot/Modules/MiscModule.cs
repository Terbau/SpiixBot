using Discord.Commands;
using SpiixBot.Util;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Modules.Audio
{
    public class MiscModule : ModuleBase<SocketCommandContext>
    {
        [Command("invite")]
        [Summary("Sends a link that can be used to invite the bot to other servers.")]
        public async Task InviteCommand()
        {
            //await ReplyAsync(embed: MessageHelper.GetEmbed($"https://discord.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=37088320&scope=bot"));
            await ReplyAsync(embed: MessageHelper.GetEmbed($"https://discord.com/oauth2/authorize?client_id={Context.Client.CurrentUser.Id}&permissions=8&scope=bot"));
        }
    }
}
