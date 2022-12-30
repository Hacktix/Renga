using Renga.Core.ROM;
using System.Runtime.CompilerServices;
using System.Text;

namespace Renga.Core
{
    internal class MemoryBus
    {
        public Cartridge Cartridge;
        public WRAM WRAM;
        public HRAM HRAM;

        public byte[] Bootrom = new byte[0x100];
        public bool OverlayBootrom;

        private Emulator _emu;

        public MemoryBus(Emulator emu, byte[] rom)
        {
            _emu = emu;

            Cartridge = new Cartridge(rom);
            WRAM = new WRAM();
            HRAM = new HRAM();
            OverlayBootrom = false;
        }

        public MemoryBus(Emulator emu, byte[] rom, byte[] bootrom) : this(emu, rom)
        {
            OverlayBootrom = true;
            Bootrom = bootrom;
        }

        public byte Read(ushort addr)
        {
            if(OverlayBootrom && addr < 0x100)
                return Bootrom[addr];

            if (addr < 0x8000) return Cartridge.MBC.Read(addr);
            if (addr < 0xA000) return _emu.PPU.ReadVRAM(addr);
            if (addr < 0xC000) return Cartridge.MBC.Read(addr);
            if (addr < 0xFE00) return WRAM.Read(addr);
            if (addr < 0xFEA0) return _emu.PPU.ReadOAM(addr);
            if (addr < 0xFEFF) throw new NotImplementedException($"Read from unimplemented unusable address 0x{addr:X4}");
            if (addr < 0xFF80) return ReadMMIO(addr);
            if (addr < 0xFFFE) return HRAM.Read(addr);

            return _emu.CPU.IE;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte ReadMMIO(ushort addr)
        {
            switch(addr)
            {
                case 0xFF04:
                case 0xFF05:
                case 0xFF06:
                case 0xFF07:
                    return _emu.Timer.Read(addr);

                case 0xFF0F: return _emu.CPU.IF;

                case 0xFF40:
                case 0xFF41:
                case 0xFF42:
                case 0xFF43:
                case 0xFF44:
                case 0xFF45:
                case 0xFF4A:
                case 0xFF4B:
                    return _emu.PPU.ReadRegister(addr);

                default:
                    return 0xFF;
            }
        }

        public void Write(ushort addr, byte val)
        {
            if (OverlayBootrom && addr < 0x100)
                return;

            if (addr < 0x8000) { Cartridge.MBC.Write(addr, val); return; }
            if (addr < 0xA000) { _emu.PPU.WriteVRAM(addr, val); return; }
            if (addr < 0xC000) { Cartridge.MBC.Write(addr, val); return; }
            if (addr < 0xFE00) { WRAM.Write(addr, val); return; }
            if (addr < 0xFEA0) { _emu.PPU.WriteOAM(addr, val); return; }
            if (addr < 0xFEFF) throw new NotImplementedException($"Write 0x{val:X2} to unimplemented unusable address 0x{addr:X4}");
            if (addr < 0xFF80) { WriteMMIO(addr, val); return; }
            if (addr < 0xFFFE) { HRAM.Write(addr, val); return; }

            _emu.CPU.IE = val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteMMIO(ushort addr, byte val)
        {
            switch(addr)
            {
                case 0xFF01:
                    Console.Write(Encoding.ASCII.GetString(new byte[] { val }));
                    break;
                case 0xFF04:
                case 0xFF05:
                case 0xFF06:
                case 0xFF07:
                    _emu.Timer.Write(addr, val);
                    break;

                case 0xFF0F:
                    _emu.CPU.IF = val;
                    break;

                case 0xFF40:
                case 0xFF41:
                case 0xFF42:
                case 0xFF43:
                case 0xFF44:
                case 0xFF45:
                case 0xFF4A:
                case 0xFF4B:
                    _emu.PPU.WriteRegister(addr, val);
                    break;

                case 0xFF50:
                    if ((val & 1) != 0)
                        OverlayBootrom = false;
                    break;
            }
        }
    }
}
