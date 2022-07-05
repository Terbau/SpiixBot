using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace SpiixBot.Modules.Audio
{
    public class AudioController
    {
        public MemoryStream Stream { get; } = new MemoryStream();
        public DateTime StartedAt { get; set; }
        public double ElapsedSeconds => (DateTime.UtcNow - StartedAt).TotalSeconds + startSeekSeconds;

        internal CancellationTokenSource cts;
        internal AudioControllerAction action;
        internal int? seekVariable = null;
        internal int startSeekSeconds = 0;

        public void Forward(int seconds)
        {
            action = AudioControllerAction.Forward;
            seekVariable = seconds;

            cts.Cancel();
        }

        public void Backwards(int seconds)
        {
            action = AudioControllerAction.Backwards;
            seekVariable = seconds;

            cts.Cancel();
        }

        public void Restart()
        {
            action = AudioControllerAction.Restart;
            seekVariable = null;  // Pretty much useless but its just here for clarity.

            cts.Cancel();
        }
    }
}
