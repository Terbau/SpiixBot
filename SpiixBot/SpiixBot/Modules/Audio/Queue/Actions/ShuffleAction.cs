using Discord.WebSocket;
using SpiixBot.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue.Actions
{
    public class ShuffleAction : BaseQueueAction
    {
        public int[] Seeds { get; set; }
        public Dictionary<int, int> PreviousSeeds { get; set; }

        public override void PerformAction(ItemQueue queue)
        {
            if (Seeds == null)
            {
                Seeds = Enumerable.Range(0, queue.Length).ToArray();
                var rand = new Random();
                rand.Shuffle(Seeds);
            }

            PreviousSeeds = queue.MoveItemsBySeeds(Seeds);
        }

        public override void UndoAction(ItemQueue queue)
        {
            queue.MoveItemsBySeeds(PreviousSeeds);
        }
    }
}
