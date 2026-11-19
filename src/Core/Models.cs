using System;
using System.Collections.Generic;

namespace MHGUPie.Core
{
    public sealed class PartState
    {
        public int Index;
        public string Name;
        public int FlinchHp, MaxFlinchHp, FlinchCount;
        public int BreakHp, MaxBreakHp, BreakCount;
        public bool Breakable;
        public bool Broken;
        public bool EverHit;
    }

    public sealed class AilmentState
    {
        public AilmentDef Def;
        public int Buildup, Threshold, Count;
        public uint Timer, MaxTimer;
        public bool Active { get { return Timer > 0; } }
        public bool EverBuilt;
    }

    public sealed class MonsterState
    {
        public int Slot;
        public uint Entity, Status;
        public int TypeId;
        public string Name;
        public int Hp, MaxHp;
        public List<PartState> Parts = new List<PartState>();
        public List<AilmentState> Ailments = new List<AilmentState>();
        public bool Dead;
        public double DamageTaken;
        public double HealthPercent { get { return MaxHp > 0 ? Math.Max(0, Math.Min(1, Hp / (double)MaxHp)) : 0; } }
    }

    public sealed class DamageRow
    {
        public string Name;
        public double Damage;
        public double Share;
    }

    public sealed class DamageSnapshot
    {
        public bool Running;
        public TimeSpan Elapsed;
        public double Total;
        public double Dps;
        public List<DamageRow> Rows = new List<DamageRow>();
        /// <summary>(seconds since start, DPS at that moment), one point per second.</summary>
        public List<KeyValuePair<double, double>> History = new List<KeyValuePair<double, double>>();
    }

    public sealed class GameSnapshot
    {
        public bool Attached;
        public string Status = "Waiting for MHGURecomp...";
        public string ProcessInfo = "";
        public List<MonsterState> Monsters = new List<MonsterState>();
        public DamageSnapshot Damage = new DamageSnapshot();
        public bool GameFocused;
    }
}
