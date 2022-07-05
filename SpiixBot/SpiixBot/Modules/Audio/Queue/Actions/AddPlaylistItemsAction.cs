using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class AddPlaylistItemsAction : BaseQueueAction
    {
        public IEnumerable<QueueItem> Items { get; set; }

        private int _startIndex;
        private int _endIndex;

        public override void PerformAction(ItemQueue queue)
        {
            _startIndex = queue.Length;
            queue.AddPlaylistItems(Items);
            _endIndex = queue.Length - 1;
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > _endIndex) throw new IllegalUndoAction();

            int count = _endIndex - totalProgress;

            IQueueAction action = queue.UndoneActions.Last();
            if (action.IsLinkedToPrevious && action is MoveItemsAction) count += totalProgress;

            queue.RemovePlaylistItems(Math.Max(_startIndex - totalProgress, 0), count);
        }

    }
}
