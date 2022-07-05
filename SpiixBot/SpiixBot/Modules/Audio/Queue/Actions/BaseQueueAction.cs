using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue.Actions
{
    public abstract class BaseQueueAction : IQueueAction
    {
        public DateTime PerformedAt { get; set; }
        public SocketGuildUser Author { get; set; }
        public int PerformedAtIndex { get; set; }
        public bool IsLinkedToPrevious { get; set; } = false;

        public abstract void PerformAction(ItemQueue queue);
        public abstract void UndoAction(ItemQueue queue);
    }
}
