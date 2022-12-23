namespace Renga.Core
{
    internal class Emulator
    {
        public readonly MemoryBus Memory;
        public readonly CPU CPU;

        public static readonly int TicksPerFrame = 0x100;//70224;

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

        public void TickFrame()
        {
            for(int i = 0; i < TicksPerFrame; i++)
            {
                CPU.Tick();
            }
        }
    }
}
