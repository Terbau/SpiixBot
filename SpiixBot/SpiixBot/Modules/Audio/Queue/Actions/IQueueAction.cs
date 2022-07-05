using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public interface IQueueAction
    {
        public DateTime PerformedAt { get; set; }
        public SocketGuildUser Author { get; set; }
        public int PerformedAtIndex { get; set; }
        public bool IsLinkedToPrevious { get; set; }

        public void PerformAction(ItemQueue queue);
        public void UndoAction(ItemQueue queue);
    }
}
