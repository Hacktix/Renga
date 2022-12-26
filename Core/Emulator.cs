namespace Renga.Core
{
    internal class Emulator
    {
        public readonly MemoryBus Memory;
        public readonly CPU CPU;
        public readonly PPU PPU;
        public readonly Timer Timer;

        public static readonly int TicksPerFrame = 70224;

        public Emulator(string romPath) {
            byte[] romContents = File.ReadAllBytes(romPath);
            byte[] bootrom = Properties.Resources.BootromDMG;

            bool useBootrom = Renga.Config.GetProperty("useBootrom", true);
            string customBootromPath = Renga.Config.GetProperty("customBootrom", "");
            if (File.Exists(customBootromPath))
                bootrom = File.ReadAllBytes(customBootromPath);

            Memory = useBootrom ? new MemoryBus(this, romContents, bootrom) : new MemoryBus(this, romContents);
            CPU = new CPU(Memory);
            PPU = new PPU();
            Timer = new Timer(this);
        }

        public void TickFrame()
        {
            for(int i = 0; i < TicksPerFrame; i++)
            {
                if ((i & 0b11) == 0)
                {
                    Timer.Tick();
                    CPU.Tick();
                }
                PPU.Tick();
            }
        }
    }
}
