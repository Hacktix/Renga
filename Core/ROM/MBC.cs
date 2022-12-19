namespace Renga.Core.ROM
{
    internal abstract class MBC
    {
        internal Cartridge _parent;

        public MBC(Cartridge parent)
        {
            _parent = parent;
        }

        public abstract byte Read(ushort address);
        public abstract void Write(ushort address, byte value);
    }
}
