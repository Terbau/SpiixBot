using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections.Generic;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class InsertPlaylistItemsAction : BaseQueueAction
    {
        public QueueItem[] Items { get; set; }
        public int StartIndex { get; set; }

        private int _endIndex;


        public override void PerformAction(ItemQueue queue)
        {
            queue.InsertPlaylistItems(StartIndex, Items);
            _endIndex = StartIndex + Items.Length - 1;
        }

        public override void UndoAction(ItemQueue queue)
        {
            int totalProgress = queue.CurrentIndex - PerformedAtIndex;

            if (totalProgress > _endIndex) throw new IllegalUndoAction();

            queue.RemovePlaylistItems(Math.Min(StartIndex - totalProgress, 0), _endIndex - totalProgress);
        }
    }
}
