using Discord.WebSocket;
using SpiixBot.Util.Paginator.Pages;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util.Paginator
{
    public class DescriptionPaginator : BasePaginator
    {
        public string Title { get; set; }
        public string Footer { get; set; }
        private List<string> _descriptionParts = new List<string>();

        public DescriptionPaginator(ISocketMessageChannel channel, TimeSpan timeout, ulong? limitControlToUserId = null) : base(channel, timeout, limitControlToUserId: limitControlToUserId) 
        {
            
        }

        public DescriptionPaginator WithTitle(string title)
        {
            Title = title;
            return this;
        }

        public DescriptionPaginator WithFooter(string footer)
        {
            Footer = footer;
            return this;
        }

        public void AddString(string part)
        {
            _descriptionParts.Add(part);
        }

        public void BuildPages(int maxPerPage = -1)
        {
            int MAX_LENGTH = 0x800;

            var parts = new List<string>();
            var builder = new StringBuilder();

            int i = 0;
            foreach (string part in _descriptionParts)
            {
                if (part.Length > MAX_LENGTH) throw new ArgumentException("paginator string is too long");

                if (part.Length + builder.Length > MAX_LENGTH || i == maxPerPage)
                {
                    parts.Add(builder.ToString());
                    builder.Clear();
                    i = 0;
                }

                builder.Append(part);
                i++;
            }

            if (builder.Length > 0) parts.Add(builder.ToString());

            foreach (string desc in parts)
            {
                var page = new RegularPage
                {
                    Title = Title,
                    Description = desc,
                    Footer = Footer,
                };

                AddPage(page);
            }
        }
    }
}
