namespace Renga.Core
{
    internal class Emulator
    {
        public readonly MemoryBus Memory;
        public readonly CPU CPU;

        public Emulator(string romPath) {
            byte[] romContents = File.ReadAllBytes(romPath);
            byte[] bootrom = Properties.Resources.BootromDMG;

            bool useBootrom = Renga.Config.GetProperty("useBootrom", true);
            string customBootromPath = Renga.Config.GetProperty("customBootrom", "");
            if (File.Exists(customBootromPath))
                bootrom = File.ReadAllBytes(customBootromPath);

            Memory = useBootrom ? new MemoryBus(romContents, bootrom) : new MemoryBus(romContents);
            CPU = new CPU(Memory);
        }
    }
}
