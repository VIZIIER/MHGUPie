using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MHGUPie.Core
{
    /// <summary>Small INI reader/writer that keeps section and key order.</summary>
    public sealed class Ini
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, List<KeyValuePair<string, string>>> _sections =
            new Dictionary<string, List<KeyValuePair<string, string>>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<string> Sections { get { return _order; } }

        public static Ini Load(string path)
        {
            var ini = new Ini();
            if (!File.Exists(path)) return ini;
            string section = "";
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                if (line[0] == '[' && line.EndsWith("]"))
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    ini.EnsureSection(section);
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();
                int comment = val.IndexOf(" ;", StringComparison.Ordinal);
                if (comment >= 0) val = val.Substring(0, comment).Trim();
                ini.Set(section, key, val);
            }
            return ini;
        }

        public void Save(string path, string header)
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(header)) sb.AppendLine(header);
            foreach (string s in _order)
            {
                sb.AppendLine("[" + s + "]");
                foreach (var kv in _sections[s]) sb.AppendLine(kv.Key + " = " + kv.Value);
                sb.AppendLine();
            }
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
        }

        private List<KeyValuePair<string, string>> EnsureSection(string section)
        {
            List<KeyValuePair<string, string>> list;
            if (!_sections.TryGetValue(section, out list))
            {
                list = new List<KeyValuePair<string, string>>();
                _sections[section] = list;
                _order.Add(section);
            }
            return list;
        }

        public bool HasSection(string section) { return _sections.ContainsKey(section); }

        public void Set(string section, string key, string value)
        {
            var list = EnsureSection(section);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    list[i] = new KeyValuePair<string, string>(list[i].Key, value);
                    return;
                }
            }
            list.Add(new KeyValuePair<string, string>(key, value));
        }

        public string Get(string section, string key, string def)
        {
            List<KeyValuePair<string, string>> list;
            if (!_sections.TryGetValue(section, out list)) return def;
            foreach (var kv in list)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) return kv.Value;
            return def;
        }

        public IEnumerable<KeyValuePair<string, string>> Keys(string section)
        {
            List<KeyValuePair<string, string>> list;
            if (_sections.TryGetValue(section, out list)) return list;
            return new KeyValuePair<string, string>[0];
        }

        public long GetNum(string section, string key, long def)
        {
            string v = Get(section, key, null);
            long r;
            return TryParseNum(v, out r) ? r : def;
        }

        public bool GetBool(string section, string key, bool def)
        {
            string v = Get(section, key, null);
            if (v == null) return def;
            v = v.ToLowerInvariant();
            if (v == "1" || v == "true" || v == "on" || v == "yes") return true;
            if (v == "0" || v == "false" || v == "off" || v == "no") return false;
            return def;
        }

        public double GetDouble(string section, string key, double def)
        {
            string v = Get(section, key, null);
            double r;
            if (v != null && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out r)) return r;
            return def;
        }

        public static bool TryParseNum(string v, out long result)
        {
            result = 0;
            if (string.IsNullOrEmpty(v)) return false;
            v = v.Trim();
            bool neg = v.StartsWith("-");
            if (neg) v = v.Substring(1);
            bool ok;
            if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                ulong u;
                ok = ulong.TryParse(v.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out u);
                result = (long)u;
            }
            else ok = long.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
            if (neg) result = -result;
            return ok;
        }
    }
}
