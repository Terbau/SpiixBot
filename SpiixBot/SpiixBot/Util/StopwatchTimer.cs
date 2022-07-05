using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SpiixBot.Util
{
    public class StopwatchTimer : IDisposable
    {
        private Stopwatch _watch = new Stopwatch();
        private int _id = -1;

        public StopwatchTimer()
        {
            Start();
        }

        public StopwatchTimer(int id)
        {
            if (id == -1) throw new ArgumentException("Id cannot be -1");
            _id = id;

            Start();
        }

        private void Start()
        {
            _watch.Start();
        }

        public void Dispose()
        {
            _watch.Stop();

            float seconds = _watch.ElapsedMilliseconds / 1000f;
            string idField = _id == -1 ? "" : $" #{_id}";

            Console.WriteLine($"[StopWatch{idField}] Finished in {seconds}s");
        }
    }
}
