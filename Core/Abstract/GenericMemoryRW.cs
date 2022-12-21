using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core.Abstract
{
    internal class GenericMemoryRW
    {
        public byte[] Data;
        internal ushort _mask;

        public GenericMemoryRW(ushort size, ushort mask = 0xFFFF)
        {
            Data = new byte[size];
            _mask = mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte Read(ushort addr)
        {
            return Data[addr & _mask];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(ushort addr, byte val) {
            Data[addr & _mask] = val;
        }

    }
}
