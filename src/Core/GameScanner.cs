using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace MHGUPie.Core
{
    /// <summary>
    /// Background poller (HunterPie's scan loop, for MHGU): finds MHGURecomp, reads the large monsters every
    /// tick and publishes an immutable GameSnapshot that the overlay picks up.
    /// </summary>
    public sealed class GameScanner : IDisposable
    {
        private sealed class Tracked
        {
            public int LastHp = int.MinValue;
            public int[] MaxFlinch, MaxBreak, FirstBreak;
            public bool[] Hit;
            public uint[] MaxTimer;
            public bool[] Built;
            public DateTime? DeadAt;
            public double Damage;
            public DateTime LastSeen;
        }

        private readonly AddressMap _map;
        private readonly GameMemory _mem;
        private readonly MonsterNames _names = new MonsterNames();
        private readonly PartNames _parts = new PartNames();
        private readonly Dictionary<string, Tracked> _tracked = new Dictionary<string, Tracked>();
        private readonly string _configDir;
        private Thread _thread;
        private volatile bool _stop;
        private volatile GameSnapshot _latest = new GameSnapshot();
        private DateTime _nextAttach = DateTime.MinValue;
        private DateTime _nextNameTable = DateTime.MinValue;
        private string _lastError = "";

        public readonly DamageTracker Damage = new DamageTracker();
        public int PollIntervalMs = 100;
        public string GameDataPathOverride;
        public GameSnapshot Latest { get { return _latest; } }
        public GameMemory Memory { get { return _mem; } }
        public AddressMap Map { get { return _map; } }

        public GameScanner(AddressMap map, string configDir)
        {
            _map = map;
            _configDir = configDir;
            _mem = new GameMemory(map);
            ReloadNameFiles();
        }

        public void ReloadNameFiles()
        {
            _names.LoadOverrides(Path.Combine(_configDir, "names.ini"));
            _parts.Load(Path.Combine(_configDir, "parts.ini"));
        }

        public void Start()
        {
            _thread = new Thread(Loop) { IsBackground = true, Name = "MHGUPie scanner" };
            _thread.Start();
        }

        private void Loop()
        {
            while (!_stop)
            {
                var sw = Stopwatch.StartNew();
                try { Tick(); }
                catch (Exception ex) { _lastError = ex.Message; }
                int wait = PollIntervalMs - (int)sw.ElapsedMilliseconds;
                Thread.Sleep(Math.Max(10, wait));
            }
        }

        private void Tick()
        {
            DateTime now = DateTime.Now;
            var snap = new GameSnapshot();

            if (_mem.IsAttached && !_mem.IsAlive())
            {
                _mem.Detach();
                _tracked.Clear();
            }

            if (!_mem.IsAttached)
            {
                snap.Status = "Waiting for MHGURecomp (mhgurecomp.exe)..." + (_lastError.Length > 0 ? "  " + _lastError : "");
                if (now >= _nextAttach)
                {
                    _nextAttach = now.AddSeconds(2);
                    TryAttach();
                }
                Damage.Tick(false, now);
                snap.Damage = Damage.Snapshot(now);
                _latest = snap;
                return;
            }

            snap.Attached = true;
            snap.ProcessInfo = _mem.ProcessName + " (pid " + _mem.ProcessId + "), guest memory at 0x" + _mem.GuestBase.ToString("X");
            snap.GameFocused = IsGameFocused();

            if (!_names.HasTable && now >= _nextNameTable)
            {
                _nextNameTable = now.AddSeconds(5);
                _names.ReadTables(_mem, _map);
            }

            ReadMonsters(snap, now);
            snap.Status = snap.Monsters.Count > 0
                ? "Connected - " + snap.Monsters.Count + " large monster(s)"
                : "Connected - no large monster right now (in town or loading)";

            Damage.Tick(snap.Monsters.Any(m => !m.Dead), now);
            snap.Damage = Damage.Snapshot(now);
            _latest = snap;
        }

        private void TryAttach()
        {
            foreach (string name in _map.ProcessNames)
            {
                Process[] ps;
                try { ps = Process.GetProcessesByName(name); }
                catch (Exception) { continue; }
                foreach (Process p in ps)
                {
                    string err;
                    if (_mem.TryAttach(p, out err))
                    {
                        _lastError = "";
                        _tracked.Clear();
                        LoadGameNames();
                        _nextNameTable = DateTime.MinValue;
                        return;
                    }
                    _lastError = err;
                }
            }
        }

        private void LoadGameNames()
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(GameDataPathOverride))
            {
                candidates.Add(GameDataPathOverride);
                candidates.Add(Path.Combine(GameDataPathOverride, "monsterName_eng.gmd"));
                candidates.Add(Path.Combine(GameDataPathOverride, _map.NamesFile));
            }
            if (Path.IsPathRooted(_map.NamesFile)) candidates.Add(_map.NamesFile);
            else if (_mem.ExecutablePath != null)
                candidates.Add(Path.Combine(Path.GetDirectoryName(_mem.ExecutablePath), _map.NamesFile));
            foreach (string c in candidates)
                if (File.Exists(c) && _names.LoadGmd(c)) return;
        }

        private bool IsGameFocused()
        {
            int pid;
            Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out pid);
            return pid == _mem.ProcessId || pid == Process.GetCurrentProcess().Id;
        }

        private static short S16(byte[] b, int o) { return BitConverter.ToInt16(b, o); }
        private static ushort U16(byte[] b, int o) { return BitConverter.ToUInt16(b, o); }

        private void ReadMonsters(GameSnapshot snap, DateTime now)
        {
            uint list = _mem.ReadU32(_map.ListPointer);
            if (!_mem.IsGuestPtr(list)) return;

            var seen = new HashSet<string>();
            byte[] block = new byte[_map.StatusBlockSize];
            for (int slot = 0; slot < _map.SlotCount; slot++)
            {
                uint ent = _mem.ReadU32(list + (uint)(_map.FirstSlot + slot * 4));
                if (!_mem.IsGuestPtr(ent)) continue;
                uint st = _mem.ReadU32(ent + (uint)_map.EntityToStatus);
                if (!_mem.IsGuestPtr(st)) continue;
                if (!_mem.Read(st, block, block.Length)) continue;

                int type = U16(block, _map.TypeId);
                if (type == 0 || type >= _map.LargeIdLimit) continue;
                int hp = BitConverter.ToInt32(block, _map.Hp);
                int maxHp = BitConverter.ToInt32(block, _map.MaxHp);
                if (maxHp <= 0 || maxHp > 2000000 || hp > maxHp * 4 || hp < -maxHp) continue;

                string key = st.ToString("X8") + ":" + type;
                seen.Add(key);
                Tracked t;
                if (!_tracked.TryGetValue(key, out t))
                {
                    t = new Tracked();
                    t.MaxFlinch = new int[_map.PartCount];
                    t.MaxBreak = new int[_map.PartCount];
                    t.FirstBreak = new int[_map.PartCount];
                    t.Hit = new bool[_map.PartCount];
                    for (int i = 0; i < _map.PartCount; i++) t.FirstBreak[i] = int.MinValue;
                    t.MaxTimer = new uint[_map.Ailments.Count];
                    t.Built = new bool[_map.Ailments.Count];
                    _tracked[key] = t;
                }
                t.LastSeen = now;

                var m = new MonsterState();
                m.Slot = slot;
                m.Entity = ent;
                m.Status = st;
                m.TypeId = type;
                m.Name = _names.Get(type);
                m.Hp = Math.Max(0, hp);
                m.MaxHp = maxHp;
                m.Dead = hp <= 0;

                // damage = HP the monster lost since the last tick
                if (t.LastHp != int.MinValue && hp < t.LastHp)
                {
                    double dmg = t.LastHp - Math.Max(0, hp);
                    if (dmg > 0 && dmg <= maxHp) { t.Damage += dmg; Damage.AddDamage(DamageName(m, snap), dmg, now); }
                }
                t.LastHp = hp;
                m.DamageTaken = t.Damage;
                if (m.Dead && t.DeadAt == null) t.DeadAt = now;
                if (!m.Dead) t.DeadAt = null;

                for (int i = 0; i < _map.PartCount; i++)
                {
                    int o = _map.PartBase + i * _map.PartStride;
                    var p = new PartState();
                    p.Index = i;
                    p.Name = _parts.Get(m.Name, i);
                    p.FlinchCount = block[o + _map.PartFlinchCount];
                    p.BreakCount = block[o + _map.PartBreakCount];
                    p.FlinchHp = U16(block, o + _map.PartFlinchHp);
                    p.BreakHp = S16(block, o + _map.PartBreakHp);
                    if (p.FlinchHp > t.MaxFlinch[i]) t.MaxFlinch[i] = p.FlinchHp;
                    if (t.FirstBreak[i] == int.MinValue) t.FirstBreak[i] = p.BreakHp;
                    if (p.BreakHp > t.MaxBreak[i]) t.MaxBreak[i] = p.BreakHp;
                    p.MaxFlinchHp = t.MaxFlinch[i];
                    p.MaxBreakHp = t.MaxBreak[i];
                    // same rule as the MHXX overlay: a part with more than 2 break HP at the start can break
                    p.Breakable = t.FirstBreak[i] > 2 || t.MaxBreak[i] > 2;
                    p.Broken = p.Breakable && (p.BreakHp <= 0 || p.BreakCount > 0);
                    if (p.FlinchHp < p.MaxFlinchHp || p.FlinchCount > 0 || p.BreakHp < p.MaxBreakHp) t.Hit[i] = true;
                    p.EverHit = t.Hit[i];
                    if (p.MaxFlinchHp > 0 || p.Breakable) m.Parts.Add(p);
                }

                for (int i = 0; i < _map.Ailments.Count; i++)
                {
                    AilmentDef d = _map.Ailments[i];
                    var a = new AilmentState();
                    a.Def = d;
                    a.Buildup = U16(block, d.Buildup);
                    a.Threshold = U16(block, d.Threshold);
                    a.Count = d.Count > 0 ? block[d.Count] : 0;
                    a.Timer = d.Timer > 0 ? BitConverter.ToUInt32(block, d.Timer) : 0;
                    if (a.Timer > 100000) a.Timer = 0; // not a timer value: ignore
                    if (a.Timer > t.MaxTimer[i]) t.MaxTimer[i] = a.Timer;
                    if (a.Timer == 0) t.MaxTimer[i] = 0;
                    a.MaxTimer = t.MaxTimer[i];
                    if (a.Buildup > 0 || a.Count > 0) t.Built[i] = true;
                    a.EverBuilt = t.Built[i];
                    if (a.Threshold > 0 && a.Threshold < 30000 && a.Buildup < 30000) m.Ailments.Add(a);
                }

                // dead/captured monsters stay on screen for a few seconds, like HunterPie
                if (m.Dead && t.DeadAt.HasValue && (now - t.DeadAt.Value).TotalSeconds > 8) continue;
                snap.Monsters.Add(m);
            }

            foreach (string k in _tracked.Keys.Where(k => !seen.Contains(k) && (now - _tracked[k].LastSeen).TotalSeconds > 30).ToList())
                _tracked.Remove(k);
        }

        private static string DamageName(MonsterState m, GameSnapshot snap)
        {
            bool dup = snap.Monsters.Any(o => o.Name == m.Name && o.Slot != m.Slot);
            return dup ? m.Name + " (" + (m.Slot + 1) + ")" : m.Name;
        }

        public void Dispose()
        {
            _stop = true;
            if (_thread != null) _thread.Join(500);
            _mem.Dispose();
        }
    }
}
