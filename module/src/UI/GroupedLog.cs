using System;
using System.Collections.Generic;

namespace Echopunks.UI
{
    /// <summary>
    /// The shared core of the editor's run logs (the TEST LOG and the EXECUTION LOG): text
    /// entries grouped under keys in arrival order, UNCAPPED by design (user rule 2026-08-25 —
    /// the graph side materializes only a window of groups around the focused one, so the store
    /// can afford to keep the whole run; realistic runs are a few MB). The entry cap is pure
    /// INSURANCE against the one unbounded case — a non-terminating solution left
    /// fast-forwarding unattended for hours (~7k entries/s) — set so high it never fires in
    /// real use ("never crash the game"): past it the OLDEST groups drop whole, silently, in
    /// chunks with one index rebuild each (amortized O(1) per add). BCL-pure; unit-tested
    /// through both wrappers.
    /// </summary>
    internal sealed class GroupedLog<TKey> where TKey : IEquatable<TKey>
    {
        private readonly int _maxEntries;

        public GroupedLog(int maxEntries)
        {
            _maxEntries = maxEntries > 0 ? maxEntries : 1;
        }

        private readonly List<TKey> _order = new List<TKey>();
        private readonly Dictionary<TKey, List<string>> _entries = new Dictionary<TKey, List<string>>();
        private readonly Dictionary<TKey, int> _pos = new Dictionary<TKey, int>(); // key -> index in _order
        private int _count;

        /// <summary>Groups holding entries, oldest first.</summary>
        public IReadOnlyList<TKey> Groups => _order;

        public bool IsEmpty => _order.Count == 0;

        /// <summary>Total entries across all groups.</summary>
        public int EntryCount => _count;

        /// <summary>Groups the insurance cap shed, oldest-first (realistically always 0; kept
        /// for the dev log line and the unit tests).</summary>
        public int DroppedGroups { get; private set; }

        public void Clear()
        {
            _order.Clear();
            _entries.Clear();
            _pos.Clear();
            _count = 0;
            DroppedGroups = 0;
        }

        public void Add(TKey key, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            List<string> list;
            if (!_entries.TryGetValue(key, out list))
            {
                list = new List<string>();
                _entries[key] = list;
                _pos[key] = _order.Count;
                _order.Add(key);
            }
            list.Add(text);
            _count++;
            if (_count > _maxEntries) DropOldest();
        }

        // Runaway backstop: shed the oldest groups whole until 5% under the cap, so the drop
        // and its index rebuild run rarely.
        private void DropOldest()
        {
            int target = _maxEntries - _maxEntries / 20;
            int k = 0;
            while (k < _order.Count - 1 && _count > target)
            {
                TKey key = _order[k];
                _count -= _entries[key].Count;
                _entries.Remove(key);
                _pos.Remove(key);
                k++;
                DroppedGroups++;
            }
            _order.RemoveRange(0, k);
            for (int i = 0; i < _order.Count; i++) _pos[_order[i]] = i;
        }

        public IReadOnlyList<string> Entries(TKey key)
        {
            List<string> list;
            return _entries.TryGetValue(key, out list) ? list : (IReadOnlyList<string>)new string[0];
        }

        /// <summary>The group's index in <see cref="Groups"/>, or -1 when absent (never added,
        /// cleared, or shed by the backstop).</summary>
        public int IndexOf(TKey key)
        {
            int i;
            return _pos.TryGetValue(key, out i) ? i : -1;
        }
    }
}
