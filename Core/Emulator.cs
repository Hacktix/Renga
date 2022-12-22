namespace Renga.Core
{
    internal class Emulator
    {
        public readonly MemoryBus Memory;
        public readonly CPU CPU;

        public Emulator(string romPath) {
            byte[] romContents = File.ReadAllBytes(romPath);
            Memory = new MemoryBus(romContents, Properties.Resources.BootromDMG);
            CPU = new CPU(Memory);
        }
    }
}
