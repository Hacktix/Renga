using System.Text;

namespace Renga.Core.ROM
{
    public enum CGBCompatibility { NONE, CGB_SUPPORTED, CGB_ONLY }

    internal class Cartridge
    {
        public byte[] Data;
        public string Title {
            get { return Encoding.ASCII.GetString(Data.Skip(0x0134).Take(16).ToArray()); }
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
        public MBC MBC;

        public Cartridge(byte[] data) {
            Data = data;
            switch(data[0x147])
            {
                case 0x00: MBC = new ROM(this); break;
                default: throw new NotImplementedException($"Unknown/Unimplemented MBC with ID ${data[0x147]:X}");
            }
        }
    }
}
