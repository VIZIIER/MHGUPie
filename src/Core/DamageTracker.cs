using System;
using System.Collections.Generic;
using System.Linq;

namespace MHGUPie.Core
{
    /// <summary>
    /// Damage meter. MHGU doesn't keep per-hunter damage, so this counts every HP point the large monsters lose
    /// (your hits, palico, poison, blast, bombs...) and shows it per monster, total, and as DPS over time.
    /// A session starts at the first damage and ends once no large monster has been around for a while.
    /// </summary>
    public sealed class DamageTracker
    {
        private readonly object _lock = new object();
        private DateTime _start, _last;
        private bool _running;
        private DateTime _lastMonsterSeen = DateTime.MinValue;
        private readonly Dictionary<string, double> _perMonster = new Dictionary<string, double>();
        private readonly List<string> _order = new List<string>();
        private readonly List<KeyValuePair<double, double>> _history = new List<KeyValuePair<double, double>>();
        private double _total;
        private int _lastSecond = -1;

        public double SessionEndSeconds = 20;

        public void Reset()
        {
            lock (_lock)
            {
                _running = false;
                _total = 0;
                _perMonster.Clear();
                _order.Clear();
                _history.Clear();
                _lastSecond = -1;
            }
        }

        public void AddDamage(string monster, double amount, DateTime now)
        {
            if (amount <= 0) return;
            lock (_lock)
            {
                if (!_running)
                {
                    if (_total > 0) { _total = 0; _perMonster.Clear(); _order.Clear(); _history.Clear(); _lastSecond = -1; }
                    _running = true;
                    _start = now;
                }
                _last = now;
                _total += amount;
                double d;
                if (!_perMonster.TryGetValue(monster, out d)) { d = 0; _order.Add(monster); }
                _perMonster[monster] = d + amount;
            }
        }

        /// <summary>Called every scanner tick.</summary>
        public void Tick(bool anyLargeMonster, DateTime now)
        {
            lock (_lock)
            {
                if (anyLargeMonster) _lastMonsterSeen = now;
                if (!_running) return;
                if (!anyLargeMonster && (now - _lastMonsterSeen).TotalSeconds > SessionEndSeconds)
                {
                    _running = false; // keep the numbers on screen until the next hunt starts
                    return;
                }
                int sec = (int)(now - _start).TotalSeconds;
                if (sec != _lastSecond)
                {
                    _lastSecond = sec;
                    double el = Math.Max(1, (now - _start).TotalSeconds);
                    _history.Add(new KeyValuePair<double, double>(el, _total / el));
                    if (_history.Count > 3600) _history.RemoveAt(0);
                }
            }
        }

        public DamageSnapshot Snapshot(DateTime now)
        {
            lock (_lock)
            {
                var s = new DamageSnapshot();
                s.Running = _running;
                if (_total <= 0) return s;
                DateTime end = _running ? now : _last;
                s.Elapsed = end - _start;
                s.Total = _total;
                s.Dps = _total / Math.Max(1, s.Elapsed.TotalSeconds);
                foreach (string k in _order)
                    s.Rows.Add(new DamageRow { Name = k, Damage = _perMonster[k], Share = _perMonster[k] / _total });
                s.Rows = s.Rows.OrderByDescending(r => r.Damage).ToList();
                s.History = new List<KeyValuePair<double, double>>(_history);
                return s;
            }
        }
    }
}
