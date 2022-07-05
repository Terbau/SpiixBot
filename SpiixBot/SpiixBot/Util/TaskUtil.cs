using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SpiixBot.Util
{
    public static class TaskUtil
    {
        public static async Task<T> RunWithTimeout<T>(Task<T> task, int millisecondsTimeout)
        {
            if (await Task.WhenAny(task, Task.Delay(millisecondsTimeout)) == task)
            {
                await task;  // re-await to raise potential errors

                return task.Result;
            }

            throw new TimeoutException();
        }

        public static async Task RunWithTimeout(Task task, int millisecondsTimeout)
        {
            if (await Task.WhenAny(task, Task.Delay(millisecondsTimeout)) == task)
            {
                await task;  // re-await to raise potential errors

                return;
            }

            throw new TimeoutException();
        }
    }
}
