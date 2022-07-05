using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue.Actions
{
    public class ClearQueueAction : BaseQueueAction
    {
        public QueueItem[] ClearedItems { get; private set; }
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }

        public override void PerformAction(ItemQueue queue)
        {
            ClearedItems = queue.RemoveRange(StartIndex, EndIndex);
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > EndIndex) throw new IllegalUndoAction();

            queue.InsertItems(Math.Max(StartIndex - totalProgress, 0), ClearedItems.Skip(totalProgress));
        }
    }
}
