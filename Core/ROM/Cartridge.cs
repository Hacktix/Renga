using System.Text;

namespace Renga.Core.ROM
{
    public enum CGBCompatibility { NONE, CGB_SUPPORTED, CGB_ONLY }

    internal class Cartridge
    {
        public byte[] Data;
        public string Title {
            get { return Encoding.ASCII.GetString(Data.Skip(0x0134).Take(16).Where((byte b) => b != 0).ToArray()); }
        }
        public CGBCompatibility CGBCompatibility {
            get {
                return Data[0x143] == 0x80 ? CGBCompatibility.CGB_SUPPORTED
                     : Data[0x143] == 0xC0 ? CGBCompatibility.CGB_ONLY
                                           : CGBCompatibility.NONE;
            }
        }
        public bool SGBCompatible
        {
            get { return Data[0x146] == 0x03; }
        }
        public int ROMSize
        {
            get { return 0x8000 * (1 << Data[0x148]); }
        }
        public int RAMSize
        {
            get
            {
                switch(Data[0x149])
                {
                    case 2: return 0x2000;
                    case 3: return 0x8000;
                    case 4: return 0x20000;
                    case 5: return 0x10000;
                    default: return 0;
                }
            }
        }
        public int Version
        {
            get { return Data[0x14C]; }
        }
        public byte HeaderChecksum
        {
            get { return Data[0x14D]; }
        }
        public ushort GlobalChecksum
        {
            get { return (ushort)(Data[0x14E] + (Data[0x14F] << 8)); }
        }

        public MBC MBC;


        public Cartridge(byte[] data) {
            Data = data;

            if (data.Length != ROMSize)
                throw new InvalidDataException($"Invalid ROM File: Header indicates {ROMSize} bytes, actual size is {data.Length}");

            switch(data[0x147])
            {
                case 0x00: MBC = new ROM(this); break;
                default: throw new NotImplementedException($"Unknown/Unimplemented MBC with ID ${data[0x147]:X}");
            }
        }
    }
}
