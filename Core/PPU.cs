using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Renga.Core
{
    internal class PPU
    {
        public byte[] VRAM = new byte[0x2000];
        public byte LY = 144;

        public byte ReadVRAM(ushort addr)
        {
            return VRAM[addr & 0x1FFF];
        }

        public void WriteVRAM(ushort addr, byte value) {
            VRAM[addr & 0x1FFF] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Tick()
        {

        }
    }
}
