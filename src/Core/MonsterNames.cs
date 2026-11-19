using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MHGUPie.Core
{
    /// <summary>
    /// Monster names straight from the game's own text (monsterName_eng.gmd, MT Framework GMD v2), looked up the
    /// way the game does it: type id -> index (TypeTable) -> message index (MessageTable) -> GMD line.
    /// User overrides in config\names.ini win.
    /// </summary>
    public sealed class MonsterNames
    {
        private string[] _gmd = new string[0];
        private readonly Dictionary<int, int> _typeToMessage = new Dictionary<int, int>();
        private readonly Dictionary<int, string> _overrides = new Dictionary<int, string>();
        public string LoadedFrom { get; private set; }

        public void LoadOverrides(string path)
        {
            _overrides.Clear();
            Ini ini = Ini.Load(path);
            foreach (var kv in ini.Keys("Names"))
            {
                long id;
                if (Ini.TryParseNum(kv.Key, out id) && kv.Value.Length > 0) _overrides[(int)id] = kv.Value;
            }
        }

        public bool LoadGmd(string path)
        {
            try
            {
                if (path == null || !File.Exists(path)) return false;
                _gmd = ParseGmd(File.ReadAllBytes(path));
                LoadedFrom = path;
                return _gmd.Length > 0;
            }
            catch (Exception) { return false; }
        }

        public static string[] ParseGmd(byte[] d)
        {
            if (d.Length < 0x28 || d[0] != 'G' || d[1] != 'M' || d[2] != 'D') return new string[0];
            int stringCount = BitConverter.ToInt32(d, 0x18);
            int stringBlock = BitConverter.ToInt32(d, 0x20);
            int start = d.Length - stringBlock;
            if (start < 0 || stringCount <= 0) return new string[0];
            var list = new List<string>(stringCount);
            int p = start;
            while (p < d.Length && list.Count < stringCount)
            {
                int e = p;
                while (e < d.Length && d[e] != 0) e++;
                list.Add(Encoding.UTF8.GetString(d, p, e - p));
                p = e + 1;
            }
            return list.ToArray();
        }

        public bool HasTable { get { return _typeToMessage.Count > 0; } }

        /// <summary>Reads the game's type id -> message tables. Safe to call again whenever.</summary>
        public void ReadTables(GameMemory mem, AddressMap map)
        {
            _typeToMessage.Clear();
            byte[] t = mem.ReadBytes(map.NameTypeTable, map.NameTypeTableCount * map.NameTypeTableStride);
            byte[] msg = mem.ReadBytes(map.NameMessageTable, (map.NameTypeTableCount + 1) * 4);
            if (t == null || msg == null) return;
            for (int i = 0; i < map.NameTypeTableCount; i++)
            {
                int type = BitConverter.ToInt32(t, i * map.NameTypeTableStride);
                int idx = BitConverter.ToInt32(t, i * map.NameTypeTableStride + 4);
                if (type <= 0 || idx < 0 || idx > map.NameTypeTableCount) continue;
                int m = BitConverter.ToInt32(msg, idx * 4);
                if (m >= 0 && m < 4096 && !_typeToMessage.ContainsKey(type)) _typeToMessage[type] = m;
            }
        }

        public string Get(int typeId)
        {
            string s;
            if (_overrides.TryGetValue(typeId, out s)) return s;
            int m;
            if (_typeToMessage.TryGetValue(typeId, out m) && m < _gmd.Length && _gmd[m].Length > 0) return _gmd[m];
            return "Monster #" + typeId;
        }
    }

    /// <summary>Part names per monster from config\parts.ini ([Monster Name] Part1 = Head ...).</summary>
    public sealed class PartNames
    {
        private Ini _ini = new Ini();
        public void Load(string path) { _ini = Ini.Load(path); }
        public string Get(string monster, int index)
        {
            string v = _ini.Get(monster, "Part" + (index + 1), null);
            return string.IsNullOrEmpty(v) ? "Part " + (index + 1) : v;
        }
    }
}
