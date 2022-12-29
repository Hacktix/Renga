using System.Runtime.CompilerServices;

namespace Renga.Core.ROM
{
    internal class MBC1 : MBC
    {
        public byte ROMBank = 1;
        public byte RAMBank = 0;
        public bool Mode = false;

        public byte[] RAM;
        public bool EnabledRAM = false;

        private bool _hasRAM;

        public MBC1(Cartridge parent) : base(parent)
        {
            RAM = new byte[parent.RAMSize];
            _hasRAM = parent.RAMSize > 0;

            // TODO: Battery-backed RAM
        }

        public override byte Read(ushort address)
        {
            if(address < 0x4000) return _parent.Data[GetLowAddressROM(address)];
            if(address < 0x8000) return _parent.Data[GetHighAddressROM(address)];

            if(!EnabledRAM || !_hasRAM) return 0xFF;
            return RAM[GetAddressRAM(address)];
        }

        public override void Write(ushort address, byte value)
        {
            if(address < 0x2000)
            {
                EnabledRAM = (value & 0xF) == 0xA;
                return;
            }

            if(address < 0x4000)
            {
                ROMBank = (byte)(value & 0b11111);
                if (ROMBank == 0)
                    ROMBank++;
                return;
            }

            if(address < 0x6000)
            {
                RAMBank = (byte)(value & 0b11);
                return;
            }

            if(address < 0x8000)
            {
                Mode = (value & 1) == 1;
                return;
            }

            if (!EnabledRAM || !_hasRAM) return;
            RAM[GetAddressRAM(address)] = value;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLowAddressROM(ushort address)
        {
            int addr = address;
            if (Mode)
                addr |= RAMBank << 19;
            return addr & (_parent.ROMSize - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetHighAddressROM(ushort address)
        {
            int addr = address & 0x3FFF;
            addr |= ROMBank << 14;
            addr |= RAMBank << 19;
            return addr & (_parent.ROMSize - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetAddressRAM(ushort address)
        {
            int addr = address & 0x1FFF;
            if (Mode)
                addr |= RAMBank << 13;
            return addr & (_parent.RAMSize - 1);
        }
    }
}
