using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    internal class CPU
    {
        private byte _regF = 0;

        public byte A = 0;
        public byte F {
            get { return _regF; }
            set { _regF = (byte)(value & 0xF0); }
        }
        public byte B = 0;
        public byte C = 0;
        public byte D = 0;
        public byte E = 0;
        public byte H = 0;
        public byte L = 0;

        public ushort AF
        {
            get { return (ushort)(F + (A << 8)); }
            set
            {
                _regF = (byte)(value & 0xF0);
                A = (byte)(value >> 8);
            }
        }
        public ushort BC
        {
            get { return (ushort)(C + (B << 8)); }
            set
            {
                C = (byte)(value & 0xFF);
                B = (byte)(value >> 8);
            }
        }
        public ushort DE
        {
            get { return (ushort)(E + (D << 8)); }
            set
            {
                E = (byte)(value & 0xFF);
                D = (byte)(value >> 8);
            }
        }
        public ushort HL
        {
            get { return (ushort)(L + (H << 8)); }
            set
            {
                L = (byte)(value & 0xFF);
                H = (byte)(value >> 8);
            }
        }

        public ushort SP = 0;
        public ushort PC = 0;

        public bool FlagZ
        {
            get { return (F & (1 << 7)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 7);
                else
                    unchecked { _regF &= (byte)~(1 << 7); }
            }
        }
        public bool FlagN
        {
            get { return (F & (1 << 6)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 6);
                else
                    unchecked { _regF &= (byte)~(1 << 6); }
            }
        }
        public bool FlagH
        {
            get { return (F & (1 << 5)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 5);
                else
                    unchecked { _regF &= (byte)~(1 << 5); }
            }
        }
        public bool FlagC
        {
            get { return (F & (1 << 4)) != 0; }
            set
            {
                if (value)
                    _regF |= (1 << 4);
                else
                    unchecked { _regF &= (byte)~(1 << 4); }
            }
        }

        public MemoryBus Memory;

        public CPU(MemoryBus memory)
        {
            Memory = memory;
        }
    }
}
