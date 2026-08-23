using System.Collections.Generic;

namespace ExaAccess.UI
{
    /// <summary>
    /// The per-run TEST LOG behind the editor's "Test log" stop: sim events (EXA errors, goal
    /// flips) grouped by the 0-based test run they landed on, in arrival order. BCL-pure so it
    /// is unit-tested. A free run sweeps up to 100 auto-advancing tests and a fan-out solution
    /// can die eight ways per test, so the store is capped: the newest <see cref="MaxTests"/>
    /// tests are kept (older ones drop WHOLE — a failing run's last test is the one that
    /// matters) and each test keeps its first <see cref="MaxPerTest"/> entries plus a count of
    /// what overflowed.
    /// </summary>
    internal sealed class TestLog
    {
        public const int MaxTests = 24;
        public const int MaxPerTest = 40;

        private readonly List<int> _order = new List<int>();
        private readonly Dictionary<int, List<string>> _entries = new Dictionary<int, List<string>>();
        private readonly Dictionary<int, int> _overflow = new Dictionary<int, int>();

        /// <summary>Test indexes holding entries, oldest first.</summary>
        public IReadOnlyList<int> Tests => _order;

        public bool IsEmpty => _order.Count == 0;

        public void Clear()
        {
            _order.Clear();
            _entries.Clear();
            _overflow.Clear();
        }

        public void Add(int test, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (test < 0) test = 0;
            List<string> list;
            if (!_entries.TryGetValue(test, out list))
            {
                list = new List<string>();
                _entries[test] = list;
                _overflow[test] = 0;
                _order.Add(test);
                while (_order.Count > MaxTests)
                {
                    int oldest = _order[0];
                    _order.RemoveAt(0);
                    _entries.Remove(oldest);
                    _overflow.Remove(oldest);
                }
            }
            if (list.Count < MaxPerTest) list.Add(text);
            else _overflow[test]++;
        }

        public IReadOnlyList<string> Entries(int test)
        {
            List<string> list;
            return _entries.TryGetValue(test, out list) ? list : (IReadOnlyList<string>)new string[0];
        }

        /// <summary>Entries beyond <see cref="MaxPerTest"/> that were counted, not kept.</summary>
        public int Overflow(int test)
        {
            int n;
            return _overflow.TryGetValue(test, out n) ? n : 0;
        }
    }
}
