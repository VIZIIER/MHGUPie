using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace MHGUPie.Core
{
    public sealed class GeneralSettings
    {
        public bool HideWhenGameUnfocused = true;
        public bool HideAllOverlays = false;
        public int PollIntervalMs = 100;
        public string DesignModeKey = "Scroll";
        public string ToggleOverlayKey = "F9";
        public string ResetMeterKey = "F10";
        public string GameDataPath = "";
        public bool StartMinimized = false;
    }

    public sealed class MonsterWidgetSettings
    {
        public bool Enabled = true;
        public double X = 1440, Y = 90;
        public double Scale = 1.0;
        public double Opacity = 1.0;
        public double Width = 440;
        public int MaxMonsters = 3;
        public bool ShowHpNumbers = true;
        public bool ShowHpPercent = true;
        public bool ShowParts = true;
        public bool ShowFlinch = true;
        public bool ShowBreak = true;
        public bool OnlyHitParts = false;
        public bool HideBrokenParts = false;
        public bool ShowAilments = true;
        public bool OnlyBuiltAilments = false;
        public bool ShowAilmentNumbers = true;
        public bool ShowAilmentCounts = true;
        public bool ShowAilmentTimers = true;
        public double CaptureLinePercent = 0;
        public bool ShowDamageTaken = false;
        /// <summary>Ailment name -> shown. Filled from the address map.</summary>
        public Dictionary<string, bool> AilmentEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public bool IsAilmentOn(string name) { bool b; return !AilmentEnabled.TryGetValue(name, out b) || b; }
    }

    public sealed class DamageMeterSettings
    {
        public bool Enabled = true;
        public double X = 40, Y = 380;
        public double Scale = 1.0;
        public double Opacity = 1.0;
        public double Width = 300;
        public bool ShowTimer = true;
        public bool ShowTotal = true;
        public bool ShowDps = true;
        public bool ShowRows = true;
        public bool ShowGraph = true;
    }

    public sealed class Settings
    {
        public GeneralSettings General = new GeneralSettings();
        public MonsterWidgetSettings Monster = new MonsterWidgetSettings();
        public DamageMeterSettings Meter = new DamageMeterSettings();
        private string _path;

        public static Settings Load(string path)
        {
            var s = new Settings { _path = path };
            Ini ini = Ini.Load(path);
            Bind(ini, "General", s.General);
            Bind(ini, "MonsterWidget", s.Monster);
            Bind(ini, "DamageMeter", s.Meter);
            foreach (var kv in ini.Keys("Ailments"))
                s.Monster.AilmentEnabled[kv.Key] = ini.GetBool("Ailments", kv.Key, true);
            return s;
        }

        public void Save()
        {
            if (_path == null) return;
            var ini = new Ini();
            Dump(ini, "General", General);
            Dump(ini, "MonsterWidget", Monster);
            Dump(ini, "DamageMeter", Meter);
            foreach (var kv in Monster.AilmentEnabled) ini.Set("Ailments", kv.Key, kv.Value ? "On" : "Off");
            try { ini.Save(_path, "; MHGUPie settings (the app writes this file; editing by hand is fine while it's closed)"); }
            catch (Exception) { }
        }

        private static void Bind(Ini ini, string section, object target)
        {
            foreach (FieldInfo f in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                string v = ini.Get(section, f.Name, null);
                if (v == null) continue;
                if (f.FieldType == typeof(bool)) f.SetValue(target, ini.GetBool(section, f.Name, (bool)f.GetValue(target)));
                else if (f.FieldType == typeof(int)) f.SetValue(target, (int)ini.GetNum(section, f.Name, (int)f.GetValue(target)));
                else if (f.FieldType == typeof(double)) f.SetValue(target, ini.GetDouble(section, f.Name, (double)f.GetValue(target)));
                else if (f.FieldType == typeof(string)) f.SetValue(target, v);
            }
        }

        private static void Dump(Ini ini, string section, object target)
        {
            foreach (FieldInfo f in target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object v = f.GetValue(target);
                if (v is bool) ini.Set(section, f.Name, (bool)v ? "On" : "Off");
                else if (v is int) ini.Set(section, f.Name, ((int)v).ToString(CultureInfo.InvariantCulture));
                else if (v is double) ini.Set(section, f.Name, ((double)v).ToString("0.###", CultureInfo.InvariantCulture));
                else if (v is string) ini.Set(section, f.Name, (string)v);
            }
        }
    }
}
