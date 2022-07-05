using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    class AddItemAction : BaseQueueAction
    {
        public QueueItem Item { get; set; }
        private int _index;
        
        public override void PerformAction(ItemQueue queue)
        {
            queue.AddSingleItem(Item);
            _index = queue.Length - 1;
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > _index) throw new IllegalUndoAction();

            queue.RemoveSingleItemAt(_index - totalProgress);
        }
    }
}
