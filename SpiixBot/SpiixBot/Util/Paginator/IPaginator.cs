using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Util.Paginator
{
    public interface IPaginator : IDisposable
    {
        List<IPaginatorPage> Pages { get; }

        public void AddPage(IPaginatorPage page);
        public void RemovePage(IPaginatorPage page);
        public void RemovePageAt(int index);
    }
}
