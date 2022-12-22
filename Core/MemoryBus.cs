using Renga.Core.ROM;
using System.Runtime.CompilerServices;

namespace Renga.Core
{
    internal class MemoryBus
    {
        public Cartridge Cartridge;
        public WRAM WRAM;
        public HRAM HRAM;

        public byte[] Bootrom = new byte[0x100];
        public bool OverlayBootrom;

        public MemoryBus(byte[] rom)
        {
            Cartridge = new Cartridge(rom);
            WRAM = new WRAM();
            HRAM = new HRAM();
            OverlayBootrom = false;
        }

        public MemoryBus(byte[] rom, byte[] bootrom) : this(rom)
        {
            OverlayBootrom = true;
            Bootrom = bootrom;
        }

        public byte Read(ushort addr)
        {
            if(OverlayBootrom && addr < 0x100)
                return Bootrom[addr];

            if (addr < 0x8000) return Cartridge.MBC.Read(addr);
            if (addr < 0xA000) throw new NotImplementedException($"Read from unimplemented VRAM address 0x{addr:X4}");
            if (addr < 0xC000) return Cartridge.MBC.Read(addr);
            if (addr < 0xFE00) return WRAM.Read(addr);
            if (addr < 0xFEA0) throw new NotImplementedException($"Read from unimplemented OAM address 0x{addr:X4}");
            if (addr < 0xFEA0) throw new NotImplementedException($"Read from unimplemented OAM address 0x{addr:X4}");
            if (addr < 0xFEFF) throw new NotImplementedException($"Read from unimplemented unusable address 0x{addr:X4}");
            if (addr < 0xFF80) throw new NotImplementedException($"Read from unimplemented I/O Register 0x{addr:X4}");
            if (addr < 0xFFFE) return HRAM.Read(addr);

            throw new NotImplementedException("Read from unimplemented IE Register");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadMMIO(ushort addr)
        {
            switch(addr)
            {
                default: throw new NotImplementedException($"Read from unimplemented I/O Register 0x{addr:X4}");
            }
        }

        public void Write(ushort addr, byte val)
        {
            if (OverlayBootrom && addr < 0x100)
                return;

            if (addr < 0x8000) { Cartridge.MBC.Write(addr, val); return; }
            if (addr < 0xA000) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented VRAM address 0x{addr:X4}");
            if (addr < 0xC000) { Cartridge.MBC.Write(addr, val); return; }
            if (addr < 0xFE00) { WRAM.Write(addr, val); return; }
            if (addr < 0xFEA0) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented OAM address 0x{addr:X4}");
            if (addr < 0xFEA0) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented OAM address 0x{addr:X4}");
            if (addr < 0xFEFF) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented unusable address 0x{addr:X4}");
            if (addr < 0xFF80) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented I/O Register 0x{addr:X4}");
            if (addr < 0xFFFE) { HRAM.Write(addr, val); return; }

            throw new NotImplementedException("Write 0x{val:X2} to unimplemented IE Register");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteMMIO(ushort addr, byte val)
        {
            switch(addr)
            {
                case 0xFF50:
                    if ((val & 1) != 0)
                        OverlayBootrom = false;
                    break;
                default: throw new NotImplementedException($"Write 0x{val:X2} to unimplemented I/O Register 0x{addr:X4}");
            }
        }
    }
}
