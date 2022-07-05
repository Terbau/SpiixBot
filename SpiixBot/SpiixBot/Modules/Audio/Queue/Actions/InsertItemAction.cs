using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class InsertItemAction : BaseQueueAction
    {
        public QueueItem Item { get; set; }
        public int Index { get; set; }

        public override void PerformAction(ItemQueue queue)
        {
            queue.InsertSingleItem(Index, Item);
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > Index) throw new IllegalUndoAction();

            queue.RemoveSingleItemAt(Index - totalProgress);
        }
    }
}
