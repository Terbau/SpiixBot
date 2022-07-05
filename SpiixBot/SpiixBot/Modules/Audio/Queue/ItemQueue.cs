using Discord.WebSocket;
using SpiixBot.Modules.Audio.Queue.Actions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiixBot.Modules.Audio.Queue
{
    public class ItemQueue : IEnumerable<QueueItem>
    {
        private readonly object _modifyLock = new object();
        private readonly List<QueueItem> _items = new List<QueueItem>();
        private int _currentIndex = 0;
        public int CurrentIndex => _currentIndex;

        public List<IQueueAction> PreviousActions { get; } = new List<IQueueAction>();
        public List<IQueueAction> UndoneActions { get; } = new List<IQueueAction>();
        public int Length => _items.Count - _currentIndex;
        public int HistoryLength => _currentIndex;
        public bool Empty => Length == 0;

        public IEnumerator<QueueItem> GetEnumerator()
        {
            return _items.Skip(_currentIndex).GetEnumerator();
        }

        public IEnumerator GetEnumerator1()
        {
            return _items.Skip(_currentIndex).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator1();
        }

        public void IncrementCurrentIndex(int incBy)
        {
            _currentIndex += incBy;
        }

        public void DecrementCurrentIndex(int decBy)
        {
            _currentIndex -= decBy;
        }

        public int GetTotalDuration()
        {
            return Enumerable.Sum(_items.Skip(_currentIndex).Select(item => item.VideoInfo != null ? item.VideoInfo.GetDurationInSeconds() : item.SongInfo.Duration));
        }

        public IEnumerable<QueueItem> GetHistory()
        {
            return _items.Take(_currentIndex).Reverse();
        }

        public bool IsReapeating()
        {
            if (PreviousActions.Count <= 0) return false;

            foreach (IQueueAction action in PreviousActions.Reverse<IQueueAction>())
            {
                if (action is RepeatAction) return true;
                if (action is StopRepeatAction) return false;
            }

            return false;
        }

        public void PerformAction(IQueueAction action)
        {
            action.PerformedAt = DateTime.UtcNow;
            action.PerformedAtIndex = _currentIndex;

            action.PerformAction(this);
            PreviousActions.Add(action);
        }

        public void UndoAction(IQueueAction action, bool undoLinked = true)
        {
            action.UndoAction(this);
            PreviousActions.Remove(action);
            UndoneActions.Add(action);

            if (undoLinked && action.IsLinkedToPrevious) UndoAction(PreviousActions.Last());
        }

        public void UnsafeClearAll()
        {
            _items.Clear();
        }

        public void ResetCurrentIndex()
        {
            _currentIndex = 0;
        }

        public void AddSingleItem(QueueItem item)
        {
            lock (_modifyLock)
            {
                _items.Add(item);
            }
        }

        public void UnsafeAddSingleItem(QueueItem item) => AddSingleItem(item);

        public void InsertSingleItem(int index, QueueItem item)
        {
            lock (_modifyLock)
            {
                _items.Insert(_currentIndex + index, item);
            }
        }

        public void UnsafeInsertSingleItem(int index, QueueItem item)
        {
            lock (_modifyLock)
            {
                _items.Insert(index, item);
            }
        }

        public void AddPlaylistItems(IEnumerable<QueueItem> items)
        {
            lock (_modifyLock)
            {
                foreach(QueueItem item in items)
                {
                    _items.Add(item);
                }
            }
        }

        public void UnsafeAddPlaylistItems(IEnumerable<QueueItem> items) => AddPlaylistItems(items);

        public void InsertPlaylistItems(int startIndex, QueueItem[] items)
        {
            lock (_modifyLock)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    _items.Insert(_currentIndex + i + startIndex, items[i]);
                }
            }
        }

        public void UnsafeInsertPlaylistItems(int startIndex, QueueItem[] items)
        {
            lock (_modifyLock)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    _items.Insert(i + startIndex, items[i]);
                }
            }
        }

        public void RemoveSingleItem(QueueItem item)
        {
            lock (_modifyLock)
            {
                _items.Remove(item);
            }
        }

        public void UnsafeRemoveSingleItem(QueueItem item) => RemoveSingleItem(item);

        public void RemoveSingleItemAt(int index)
        {
            lock (_modifyLock)
            {
                _items.RemoveAt(_currentIndex + index);
            }
        }

        public void UnsafeRemoveSingleItemAt(int index)
        {
            lock (_modifyLock)
            {
                _items.RemoveAt(index);
            }
        }

        public void RemovePlaylistItems(int startIndex, int endIndex = -1)
        {
            lock (_modifyLock)
            {
                int actualEndIndex = endIndex == -1 ? _items.Count : _currentIndex + endIndex;
                for (int i = actualEndIndex; i >= _currentIndex + startIndex; i--)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        public void UnsafeRemovePlaylistItems(int startIndex, int endIndex = -1)
        {
            lock (_modifyLock)
            {
                int actualEndIndex = endIndex == -1 ? _items.Count : endIndex;
                for (int i = actualEndIndex; i >= startIndex; i--)
                {
                    _items.RemoveAt(i);
                }
            }
        }

        public QueueItem[] RemoveRange(int startIndex = 0, int endIndex = -1)
        {
            int takeCount = (endIndex != -1 ? endIndex : Length - 1) - startIndex + 1;

            Console.WriteLine(startIndex.ToString() + " " + endIndex.ToString() + " " + takeCount.ToString());

            QueueItem[] items = _items.Skip(_currentIndex + startIndex).Take(takeCount).ToArray();
            _items.RemoveRange(_currentIndex + startIndex, takeCount);

            return items;
        }

        public QueueItem[] RemoveRangeCount(int startIndex, int count)
        {
            Console.WriteLine(startIndex.ToString() + " " + count.ToString());
            QueueItem[] items = _items.Skip(_currentIndex + startIndex).Take(count).ToArray();
            _items.RemoveRange(_currentIndex + startIndex, count);

            return items;
        }

        public QueueItem[] SelectRange(int startIndex, int endIndex)
        {
            int takeCount = (endIndex != -1 ? endIndex : Length - 1) - startIndex + 1;
            QueueItem[] items = _items.Skip(_currentIndex + startIndex).Take(takeCount).ToArray();
            return items;
        }

        public QueueItem[] SelectRangeCount(int startIndex, int count)
        {
            QueueItem[] items = _items.Skip(_currentIndex + startIndex).Take(count).ToArray();
            return items;
        }

        public QueueItem[] UnsafeSelectRangeCount(int startIndex, int count)
        {
            QueueItem[] items = _items.Skip(startIndex).Take(count).ToArray();
            return items;
        }

        public QueueItem SelectItem(int index)
        {
            return _items[_currentIndex + index];
        }

        public void RemoveRangeNoReturn(int startIndex = 0, int endIndex = -1)
        {
            int takeCount = (endIndex != -1 ? endIndex : Length - 1) - startIndex + 1;
            _items.RemoveRange(_currentIndex + startIndex, takeCount);
        }

        public void RemoveRangeCountNoReturn(int startIndex, int count)
        {
            _items.RemoveRange(_currentIndex + startIndex, count);
        }

        public void InsertItems(int index, IEnumerable<QueueItem> items)
        {
            _items.InsertRange(_currentIndex + index, items);
        }

        public QueueItem GetItemAt(int index, bool remove = true)
        {
            QueueItem item = _items[_currentIndex + index];
            if (remove) _items.RemoveAt(_currentIndex + index);

            return item;
        }

        public QueueItem GetNextItem(bool remove = true)
        {
            QueueItem item = _items[_currentIndex];

            if (remove) _currentIndex++;

            return item;
        }

        public Dictionary<int, int> MoveItemsBySeeds(int[] seeds)
        {
            if (seeds.Length != Length) throw new ArgumentException("Seeds must be the same length as the queue.");

            var copied = _items.Skip(_currentIndex).ToArray();
            var resDict = new Dictionary<int, int>();

            int i = 0;
            foreach (QueueItem item in copied)
            {
                int seed = seeds[i];
                _items[_currentIndex + seed] = item;
                resDict[seed] = i;

                i++;
            }

            return resDict;
        }

        public void MoveItemsBySeeds(int[] fromSeeds, int[] toSeeds)
        {
            if (fromSeeds.Length != toSeeds.Length) throw new ArgumentException("from and to seeds must have the same length");
            if (fromSeeds.Length != Length) throw new ArgumentException("Seeds must be the same length as the queue.");

            var adjusted = new QueueItem[Length];
            int k = 0;
            foreach (QueueItem item in _items.Skip(_currentIndex))
            {
                adjusted[fromSeeds[k]] = item;
                k++;
            }

            int i = 0;
            foreach (QueueItem item in adjusted)
            {
                _items[_currentIndex + toSeeds[i]] = item;
                i++;
            }
        }

        public void MoveItemsBySeeds(Dictionary<int, int> seedMap)
        {
            int offset = seedMap.Count - Length;
            var copied = _items.Skip(_items.Count - seedMap.Count).ToArray();

            foreach (KeyValuePair<int, int> pair in seedMap)
            {
                if (_currentIndex + pair.Value - offset < 0) continue;

                var temp = copied[pair.Key];
                _items[_currentIndex + pair.Value - offset] = temp;
            }
        }
    }
}
