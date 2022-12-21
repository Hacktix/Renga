using Renga.Core.ROM;

namespace Renga.Core
{
    internal class MemoryBus
    {
        public Cartridge Cartridge;
        public WRAM WRAM;
        public HRAM HRAM;

        public MemoryBus(byte[] rom) {
            Cartridge = new Cartridge(rom);
            WRAM = new WRAM();
            HRAM = new HRAM();
        }

        public byte Read(ushort addr)
        {
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

        public void Write(ushort addr, byte val)
        {
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
    }
}
