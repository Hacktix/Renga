namespace Renga.Core.ROM
{
    internal class ROM : MBC
    {
        public ROM(Cartridge parent) : base(parent)
        {
        }

        public override byte Read(ushort address)
        {
            return _parent.Data[address];
        }

        public override void Write(ushort address, byte value)
        {
            
        }
    }
}
