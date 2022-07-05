using Discord;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util.Paginator
{
    public interface IPaginatorPage
    {
        public Embed ConstructPage();
    }
}
