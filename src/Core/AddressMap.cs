using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace MHGUPie.Core
{
    public sealed class AilmentDef
    {
        public string Name;
        public int Buildup, Threshold, Count, Timer;
        public Color Color;
        public string Source;
        public bool Verified { get { return string.Equals(Source, "verified", StringComparison.OrdinalIgnoreCase); } }
    }

    /// <summary>All game addresses/offsets, loaded from config\MHGU-1.4.0.map.ini (HunterPie-style map file).</summary>
    public sealed class AddressMap
    {
        public string[] ProcessNames;
        public ulong GuestBase;
        public uint PtrMin, PtrMax;

        public uint ListPointer;
        public int FirstSlot, SlotCount, EntityToStatus, TypeId, LargeIdLimit, Hp, MaxHp, StatusBlockSize;

        public int PartBase, PartStride, PartCount, PartFlinchCount, PartBreakCount, PartFlinchHp, PartBreakHp;

        public uint NameTypeTable, NameMessageTable;
        public int NameTypeTableCount, NameTypeTableStride;
        public string NamesFile;

        public List<AilmentDef> Ailments = new List<AilmentDef>();

        public static AddressMap Load(string path)
        {
            Ini ini = Ini.Load(path);
            var m = new AddressMap();
            m.ProcessNames = ini.Get("Process", "Names", "mhgurecomp")
                .Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            m.GuestBase = (ulong)ini.GetNum("Process", "GuestBase", 0x10000000000L);
            m.PtrMin = (uint)ini.GetNum("Process", "GuestPointerMin", 0x08000000);
            m.PtrMax = (uint)ini.GetNum("Process", "GuestPointerMax", 0xF0000000L);

            m.ListPointer = (uint)ini.GetNum("Monsters", "ListPointer", 0x1A51FE0);
            m.FirstSlot = (int)ini.GetNum("Monsters", "FirstSlot", 0x1C);
            m.SlotCount = (int)ini.GetNum("Monsters", "SlotCount", 10);
            m.EntityToStatus = (int)ini.GetNum("Monsters", "EntityToStatus", 0x1428);
            m.TypeId = (int)ini.GetNum("Monsters", "TypeId", 0x1B6);
            m.LargeIdLimit = (int)ini.GetNum("Monsters", "LargeMonsterIdLimit", 10000);
            m.Hp = (int)ini.GetNum("Monsters", "Hp", 0x370);
            m.MaxHp = (int)ini.GetNum("Monsters", "MaxHp", 0x374);
            m.StatusBlockSize = (int)ini.GetNum("Monsters", "StatusBlockSize", 0x5F00);

            m.PartBase = (int)ini.GetNum("Parts", "Base", 0x3B6);
            m.PartStride = (int)ini.GetNum("Parts", "Stride", 0xC);
            m.PartCount = (int)ini.GetNum("Parts", "Count", 8);
            m.PartFlinchCount = (int)ini.GetNum("Parts", "FlinchCount", 6);
            m.PartBreakCount = (int)ini.GetNum("Parts", "BreakCount", 7);
            m.PartFlinchHp = (int)ini.GetNum("Parts", "FlinchHp", 8);
            m.PartBreakHp = (int)ini.GetNum("Parts", "BreakHp", 0xA);

            m.NameTypeTable = (uint)ini.GetNum("Names", "TypeTable", 0x1797EA4);
            m.NameTypeTableCount = (int)ini.GetNum("Names", "TypeTableCount", 0x88);
            m.NameTypeTableStride = (int)ini.GetNum("Names", "TypeTableStride", 8);
            m.NameMessageTable = (uint)ini.GetNum("Names", "MessageTable", 0x182E7F8);
            m.NamesFile = ini.Get("Names", "NamesFile", @"data\romfs\nativeNX\eng\GUI\06_msg\monsterName_eng.gmd");

            foreach (string s in ini.Sections)
            {
                if (!s.StartsWith("Ailment.", StringComparison.OrdinalIgnoreCase)) continue;
                var a = new AilmentDef();
                a.Name = s.Substring("Ailment.".Length);
                a.Buildup = (int)ini.GetNum(s, "Buildup", 0);
                a.Threshold = (int)ini.GetNum(s, "Threshold", 0);
                a.Count = (int)ini.GetNum(s, "Count", 0);
                a.Timer = (int)ini.GetNum(s, "Timer", 0);
                a.Source = ini.Get(s, "Source", "");
                try { a.Color = (Color)ColorConverter.ConvertFromString(ini.Get(s, "Color", "#CCCCCC")); }
                catch (Exception) { a.Color = Colors.LightGray; }
                if (a.Buildup > 0 && a.Threshold > 0) m.Ailments.Add(a);
            }

            int needed = Math.Max(m.MaxHp + 4, m.PartBase + m.PartStride * m.PartCount);
            foreach (var a in m.Ailments)
                needed = Math.Max(needed, Math.Max(Math.Max(a.Buildup, a.Threshold) + 2, Math.Max(a.Count + 1, a.Timer + 4)));
            if (m.StatusBlockSize < needed) m.StatusBlockSize = (needed + 0xFF) & ~0xFF;
            return m;
        }
    }
}
