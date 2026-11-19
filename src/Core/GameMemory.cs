using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MHGUPie.Core
{
    /// <summary>
    /// Read-only access to the game's memory. MHGURecomp keeps the Switch's 32-bit address space in one window of
    /// its own process, so a game ("guest") address g lives at host address GuestBase + g.
    /// Nothing is ever written to the game.
    /// </summary>
    public sealed class GameMemory : IDisposable
    {
        private IntPtr _handle = IntPtr.Zero;
        private readonly byte[] _scratch = new byte[8];
        private readonly AddressMap _map;

        public int ProcessId { get; private set; }
        public string ProcessName { get; private set; }
        public string ExecutablePath { get; private set; }
        public ulong GuestBase { get; private set; }
        public bool IsAttached { get { return _handle != IntPtr.Zero; } }

        public GameMemory(AddressMap map) { _map = map; }

        public bool TryAttach(Process p, out string error)
        {
            error = null;
            Detach();
            IntPtr h = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_QUERY_INFORMATION, false, p.Id);
            if (h == IntPtr.Zero)
            {
                error = "Could not open the game process (error " + Marshal.GetLastWin32Error() + "). " +
                        "If the game runs as administrator, run MHGUPie as administrator too.";
                return false;
            }
            _handle = h;
            ProcessId = p.Id;
            ProcessName = p.ProcessName;
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            ExecutablePath = Native.QueryFullProcessImageName(h, 0, sb, ref size) ? sb.ToString() : null;

            ulong found;
            if (!FindGuestBase(out found))
            {
                error = "Attached, but the game's memory window isn't there yet (still starting up?)";
                Detach();
                return false;
            }
            GuestBase = found;
            return true;
        }

        /// <summary>Uses the configured base if its pages are there; otherwise looks for a 4 GiB-aligned window.</summary>
        private bool FindGuestBase(out ulong result)
        {
            result = 0;
            if (LooksLikeGuestBase(_map.GuestBase)) { result = _map.GuestBase; return true; }

            ulong addr = 0x10000;
            var seen = new System.Collections.Generic.HashSet<ulong>();
            Native.MEMORY_BASIC_INFORMATION mbi;
            int mbiSize = Marshal.SizeOf(typeof(Native.MEMORY_BASIC_INFORMATION));
            while (addr < 0x7FFFFFFF0000UL)
            {
                if (Native.VirtualQueryEx(_handle, new IntPtr((long)addr), out mbi, new IntPtr(mbiSize)) == IntPtr.Zero) break;
                ulong ab = mbi.AllocationBase;
                if (ab != 0 && (ab & 0xFFFFFFFFUL) == 0 && seen.Add(ab) && LooksLikeGuestBase(ab))
                {
                    result = ab;
                    return true;
                }
                ulong next = mbi.BaseAddress + mbi.RegionSize;
                if (next <= addr) break;
                addr = next;
            }
            return false;
        }

        private bool LooksLikeGuestBase(ulong b)
        {
            byte[] buf = new byte[4];
            if (!ReadHost(b + _map.ListPointer, buf, 4)) return false;
            uint list = BitConverter.ToUInt32(buf, 0);
            return list == 0 || IsGuestPtr(list);
        }

        public bool IsAlive()
        {
            if (_handle == IntPtr.Zero) return false;
            uint code;
            return Native.GetExitCodeProcess(_handle, out code) && code == 259; // STILL_ACTIVE
        }

        public bool IsGuestPtr(uint p)
        {
            return p >= _map.PtrMin && p < _map.PtrMax && (p & 3) == 0;
        }

        private bool ReadHost(ulong host, byte[] buffer, int count)
        {
            IntPtr read;
            return Native.ReadProcessMemory(_handle, new IntPtr((long)host), buffer, new IntPtr(count), out read)
                   && read.ToInt64() == count;
        }

        public bool Read(uint guest, byte[] buffer, int count)
        {
            if (_handle == IntPtr.Zero) return false;
            return ReadHost(GuestBase + guest, buffer, count);
        }

        public uint ReadU32(uint guest)
        {
            lock (_scratch) { return Read(guest, _scratch, 4) ? BitConverter.ToUInt32(_scratch, 0) : 0; }
        }

        public byte[] ReadBytes(uint guest, int count)
        {
            var b = new byte[count];
            return Read(guest, b, count) ? b : null;
        }

        public void Detach()
        {
            if (_handle != IntPtr.Zero) Native.CloseHandle(_handle);
            _handle = IntPtr.Zero;
            ProcessId = 0;
        }

        public void Dispose() { Detach(); }
    }
}
