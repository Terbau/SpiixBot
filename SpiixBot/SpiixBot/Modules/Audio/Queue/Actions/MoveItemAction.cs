using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class MoveItemAction : BaseQueueAction
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public Func<ItemQueue, int> ResolveStartIndex { get; set; }

        private QueueItem _item;

        public override void PerformAction(ItemQueue queue)
        {
            if (ResolveStartIndex != null) StartIndex = ResolveStartIndex(queue);

            _item = queue.GetItemAt(StartIndex, remove: false);

            bool removeFirst = StartIndex > EndIndex;
            if (removeFirst) queue.RemoveSingleItemAt(StartIndex);

            queue.InsertSingleItem(EndIndex, _item);

            if (!removeFirst) queue.RemoveSingleItemAt(StartIndex);
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > Math.Min(StartIndex, EndIndex)) throw new IllegalUndoAction();

            bool removeFirst = StartIndex > EndIndex;
            if (removeFirst) queue.UnsafeRemoveSingleItemAt(totalProgress + EndIndex);

            queue.UnsafeInsertSingleItem(totalProgress + StartIndex, _item);

            if (!removeFirst) queue.UnsafeRemoveSingleItemAt(totalProgress + EndIndex);
        }
    }
}
