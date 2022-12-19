namespace Renga.Core
{
    internal class Emulator
    {
        public readonly MemoryBus Memory;

        public Emulator(string romPath) {
            byte[] romContents = File.ReadAllBytes(romPath);
            Memory = new MemoryBus(romContents);
        }
    }
}
