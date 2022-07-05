using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util
{
    internal class MessageHelper
    {
        private const int _defaultColor = 0xFFFFFF;
        private const int _defaultSuccessColor = 0x2BFC51;
        private const int _defaultErrorColor = 0xFF2B2B;

        private const string _successImageUrl = "https://cdn.discordapp.com/attachments/627164432296050688/852956155663351848/success.png";
        private const string _errorImageUrl = "https://cdn.discordapp.com/attachments/627164432296050688/852955294573002842/error.png";

        internal static Embed GetEmbed(string description = null, string title = null, Color? color = null, string footer = null, string authorName = null, string authorIconUrl = null)
        {
            var embed = new EmbedBuilder()
                .WithColor(color ?? new Color(_defaultColor));

            if (title != null) embed.WithTitle(title);
            if (description != null) embed.WithDescription(description);
            if (footer != null) embed.WithFooter(embedFooter => embedFooter.Text = footer);

            if (authorName != null && authorIconUrl != null)
            {
                var author = new EmbedAuthorBuilder()
                    .WithName(authorName)
                    .WithIconUrl(authorIconUrl);

                embed.WithAuthor(author);
            }

            return embed.Build();
        }

        internal static Embed GetSuccessEmbed(string description = null, string footer = null)
        {
            return GetEmbed(description: description, color: new Color(_defaultSuccessColor), footer: footer, authorName: "Success", authorIconUrl: _successImageUrl);
        }

        internal static Embed GetErrorEmbed(string description = null, string footer = null)
        {
            return GetEmbed(description: description, color: new Color(_defaultErrorColor), footer: footer, authorName: "Error", authorIconUrl: _errorImageUrl);
        }
    }
}
