using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace MHGUPie.Sim
{
    /// <summary>
    /// Stand-in for MHGURecomp, for testing MHGUPie without the game: lays out two large monsters in memory at the
    /// same guest addresses/offsets the real game uses (guest window at 0x10000000000) and fights them.
    ///   MHGUPieSimulator.exe [--seconds N]
    /// </summary>
    internal static class Simulator
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr addr, UIntPtr size, uint type, uint protect);

        private const ulong Base = 0x10000000000UL;
        private const uint List = 0x20000000;
        private static readonly uint[] Ent = { 0x20010000, 0x20020000 };
        private static readonly uint[] St = { 0x20100000, 0x20110000 };

        private static void Alloc(uint guest, uint size)
        {
            IntPtr p = VirtualAlloc(new IntPtr((long)(Base + guest)), new UIntPtr(size), 0x3000, 0x04);
            if (p == IntPtr.Zero) throw new Exception("VirtualAlloc at guest 0x" + guest.ToString("X") + " failed: " + Marshal.GetLastWin32Error());
        }

        private static IntPtr H(uint g) { return new IntPtr((long)(Base + g)); }
        private static void W32(uint g, int v) { Marshal.WriteInt32(H(g), v); }
        private static void W16(uint g, short v) { Marshal.WriteInt16(H(g), v); }
        private static void W8(uint g, byte v) { Marshal.WriteByte(H(g), v); }
        private static int R32(uint g) { return Marshal.ReadInt32(H(g)); }
        private static short R16(uint g) { return Marshal.ReadInt16(H(g)); }
        private static byte R8(uint g) { return Marshal.ReadByte(H(g)); }

        // ailment offsets, same as config\MHGU-1.4.0.map.ini
        private static readonly uint[,] Ail =
        {
            // buildup, threshold, count, timer
            { 0x5C9C, 0x5CA8, 0x5D68, 0x5CAC }, // poison
            { 0x5CB6, 0x5CB4, 0x5D69, 0x5CB8 }, // paralysis
            { 0x5CA0, 0x5C9E, 0x5D6A, 0 },      // sleep
            { 0x5D76, 0x5D78, 0x5D80, 0x5D7C }, // stun
            { 0x5D82, 0x5D84, 0, 0x5D88 },      // exhaust
            { 0x5D9A, 0x5D9C, 0x5DA4, 0 },      // mount
            { 0x5DAA, 0x5DA8, 0x5DA6, 0x5DB0 }, // blast
        };

        private static void Spawn(int i, int type, int hp)
        {
            uint s = St[i];
            for (uint o = 0; o < 0x6000; o += 4) W32(s + o, 0);
            W16(s + 0x1B6, (short)type);
            W32(s + 0x370, hp);
            W32(s + 0x374, hp);
            int[] flinch = { 300, 220, 180, 180, 260, 150, 0, 0 };
            int[] brk = { 450, -1, 300, 300, 500, -1, 0, 0 };
            for (int p = 0; p < 8; p++)
            {
                uint o = s + 0x3B6 + (uint)(p * 0xC);
                W16(o + 8, (short)flinch[p]);
                W16(o + 0xA, (short)brk[p]);
            }
            int[] thr = { 180, 150, 150, 150, 200, 100, 120 };
            for (int a = 0; a < Ail.GetLength(0); a++) W16(s + Ail[a, 1], (short)thr[a]);
            W32(Ent[i] + 0x1428, (int)St[i]);
            W32(List + 0x1C + (uint)(i * 4), (int)Ent[i]);
        }

        private static void Despawn(int i) { W32(List + 0x1C + (uint)(i * 4), 0); }

        public static int Main(string[] args)
        {
            int seconds = 0;
            int ai = Array.IndexOf(args, "--seconds");
            if (ai >= 0 && ai + 1 < args.Length) int.TryParse(args[ai + 1], out seconds);

            Alloc(0x1790000, 0xA0000);   // name tables
            Alloc(0x1A50000, 0x10000);   // monster manager global
            Alloc(List, 0x200000);       // manager, entities, status blocks
            // type id -> index -> monsterName_eng.gmd line (3 = Rathalos, 27 = Tigrex in the real file)
            W32(0x1797EA4, 2); W32(0x1797EA8, 0);
            W32(0x1797EAC, 23); W32(0x1797EB0, 1);
            W32(0x182E7F8, 3); W32(0x182E7FC, 27);
            W32(0x1A51FE0, (int)List);
            Spawn(0, 2, 9800);
            Spawn(1, 23, 12400);
            Console.WriteLine("MHGUPie simulator running: Rathalos + Tigrex at the real MHGURecomp addresses. Ctrl+C to stop.");

            var rnd = new Random(7);
            var start = DateTime.Now;
            DateTime?[] deadAt = { null, null };
            int tick = 0;
            while (seconds <= 0 || (DateTime.Now - start).TotalSeconds < seconds)
            {
                Thread.Sleep(100);
                tick++;
                for (int i = 0; i < 2; i++)
                {
                    uint s = St[i];
                    if (R32(List + 0x1C + (uint)(i * 4)) == 0)
                    {
                        if (deadAt[i].HasValue && (DateTime.Now - deadAt[i].Value).TotalSeconds > 14) { Spawn(i, i == 0 ? 2 : 23, 9000 + rnd.Next(4000)); deadAt[i] = null; }
                        continue;
                    }
                    int hp = R32(s + 0x370);
                    if (hp <= 0)
                    {
                        if (deadAt[i].HasValue && (DateTime.Now - deadAt[i].Value).TotalSeconds > 10) Despawn(i);
                        continue;
                    }
                    // ailment timers run down
                    for (int a = 0; a < Ail.GetLength(0); a++)
                        if (Ail[a, 3] != 0 && R32(s + Ail[a, 3]) > 0) W32(s + Ail[a, 3], Math.Max(0, R32(s + Ail[a, 3]) - 3));
                    if ((tick + i * 2) % (i == 0 ? 4 : 6) != 0) continue;

                    int dmg = 25 + rnd.Next(70);
                    hp -= dmg;
                    W32(s + 0x370, Math.Max(0, hp));
                    if (hp <= 0) deadAt[i] = DateTime.Now;

                    int p = rnd.Next(6);
                    uint po = s + 0x3B6 + (uint)(p * 0xC);
                    int f = R16(po + 8) - dmg;
                    if (f <= 0) { W8(po + 6, (byte)(R8(po + 6) + 1)); f = 300; }
                    W16(po + 8, (short)f);
                    short b = R16(po + 0xA);
                    if (b > 0) { b = (short)(b - dmg); if (b <= 0) { W8(po + 7, (byte)(R8(po + 7) + 1)); } W16(po + 0xA, b); }

                    int which = rnd.Next(Ail.GetLength(0));
                    uint bu = s + Ail[which, 0], th = s + Ail[which, 1];
                    int v = R16(bu) + 10 + rnd.Next(25);
                    if (v >= R16(th))
                    {
                        v = 0;
                        W16(th, (short)(R16(th) + 60));
                        if (Ail[which, 2] != 0) W8(s + Ail[which, 2], (byte)(R8(s + Ail[which, 2]) + 1));
                        if (Ail[which, 3] != 0) W32(s + Ail[which, 3], 300);
                    }
                    W16(bu, (short)v);
                }
            }
            return 0;
        }
    }
}
