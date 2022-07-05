using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Util.Paginator
{
    public abstract class BasePaginatorPage : IPaginatorPage
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Footer { get; set; }

        public Embed ConstructPage()
        {
            var builder = new EmbedBuilder()
                .WithDescription(Description);

            if (Title != null) builder.WithTitle(Title);
            if (Footer != null)
            {
                var fBuilder = new EmbedFooterBuilder()
                    .WithText(Footer);

                builder.WithFooter(fBuilder);
            }

            return builder.Build();
        }
    }
}
