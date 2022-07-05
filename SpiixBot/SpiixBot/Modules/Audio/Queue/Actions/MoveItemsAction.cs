using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue.Actions
{
    public class MoveItemsAction : BaseQueueAction
    {
        public int StartIndex { get; set; } = -1;
        public int EndIndex { get; set; }
        public int InsertAtIndex { get; set; }

        public Func<ItemQueue, int> ResolveStartIndex { get; set; }

        private int _actInsertAtIndex;

        public override void PerformAction(ItemQueue queue)
        {
            if (ResolveStartIndex != null && StartIndex == -1) StartIndex = ResolveStartIndex(queue);

            QueueItem[] items = queue.RemoveRange(StartIndex, EndIndex);

            int actInsertAtIndex = InsertAtIndex <= StartIndex ? InsertAtIndex : InsertAtIndex - items.Length + 1;
            _actInsertAtIndex = actInsertAtIndex;
            queue.InsertItems(Math.Max(actInsertAtIndex, 0), items);
        }

        public override void UndoAction(ItemQueue queue)
        {
            // Test this more
            // - Select (BUT NOT DELETE!) from history when progressed?

            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            //int justTest = StartIndex - totalProgress < 0 ? 

            //int toAdd = InsertAtIndex > StartIndex ? EndIndex - StartIndex - 
            int count = Math.Max(EndIndex - StartIndex + 1 + Math.Min(_actInsertAtIndex - totalProgress, 0), 0);

            int actInsertAtIndex = InsertAtIndex <= StartIndex ? InsertAtIndex : InsertAtIndex - count + 1;

            QueueItem[] items = queue.UnsafeSelectRangeCount(queue.CurrentIndex + actInsertAtIndex - totalProgress, EndIndex - StartIndex + 1);  // Currently here

            queue.RemoveRangeCountNoReturn(Math.Max(actInsertAtIndex - totalProgress, 0), count);
            queue.InsertItems(Math.Max(StartIndex - totalProgress + (EndIndex - StartIndex - count) + 1, 0), items);
        }
    }
}
