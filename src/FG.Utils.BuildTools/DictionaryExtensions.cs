using System.Collections.Generic;
using System.Linq;

namespace FG.Utils.BuildTools
{
    public static class DictionaryExtensions
    {
        public enum DictionaryDiffType
        {
            None = 0,
            Added = 1,
            Removed = 2,
            Changed = 3,
        }

        public class DictionaryDiff<TKey, TValue>
        {
            public DictionaryDiffType Type { get; set; }
            public TKey Key { get; set; }

            public TValue OldValue { get; set; }
            public TValue NewValue { get; set; }
        }

        public static bool AreEquivalent<TKey, TValue>(
            this IDictionary<TKey, TValue> a, IDictionary<TKey, TValue> b)
        {
            if (a == null) return (b == null);
            if (a.Count != b?.Count) return false;

            return !a.GetDiff(b).Any();
        }

        public static IEnumerable<DictionaryDiff<TKey, TValue>> GetDiff<TKey, TValue>(this IDictionary<TKey, TValue> a, IDictionary<TKey, TValue> b)
        {
            var addedItems = new SortedList<TKey, TValue>(b);
            var addedKeys = b.Keys.ToArray();
            var removedItems = new SortedList<TKey, TValue>(a);
            var removedKeys = a.Keys.ToList();
            var changedItems = new List<DictionaryDiff<TKey, TValue>>();

            foreach (var addedKey in addedKeys)
            {
                if (!removedKeys.Contains(addedKey))
                {
                    removedItems.Remove(addedKey);
                }
                else
                {
                    var itemA = removedItems[addedKey];
                    var itemB = addedItems[addedKey];

                    if (!itemA.Equals(itemB))
                    {
                        changedItems.Add(new DictionaryDiff<TKey, TValue>()
                        {
                            Key = addedKey,
                            NewValue = itemB,
                            OldValue = itemA
                        });
                    }
                }
            }

            changedItems.AddRange(addedItems.Select(kv => new DictionaryDiff<TKey, TValue>() { Key = kv.Key, NewValue = kv.Value }));
            changedItems.AddRange(removedItems.Select(kv => new DictionaryDiff<TKey, TValue>() { Key = kv.Key, OldValue = kv.Value }));

            return changedItems;
        }

        public static TValue Get<TKey, TValue>(this IDictionary<TKey, TValue> that, TKey key)
        {
            return that.ContainsKey(key) ? that[key] : default(TValue);
        }
    }
}