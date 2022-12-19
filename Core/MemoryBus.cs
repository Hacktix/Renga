using Renga.Core.ROM;

namespace Renga.Core
{
    internal class MemoryBus
    {
        public Cartridge Cartridge;

        public MemoryBus(byte[] rom) {
            Cartridge = new Cartridge(rom);
        }
    }
}
