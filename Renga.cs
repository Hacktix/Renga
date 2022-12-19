using Chroma;
using Chroma.Graphics;
using Chroma.Windowing.DragDrop;
using Renga.Core;
using Renga.Config;

namespace Renga
{
    public class Renga : Game
    {
        private RengaConfig _conf = new RengaConfig();
        private Emulator _emu;

        public Renga() : base(new(true, false))
        {
            Window.FilesDropped += WindowOnFilesDropped;
            Window.Title = "れんが";
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            Window.SetIcon(Content.Load<Texture>("icon.png"));
        }

        private void InitializeEmulator(string romPath)
        {
            _emu = new Emulator(romPath);
            Window.Title = $"れんが - {_emu.Memory.Cartridge.Title}";
        }

        private void WindowOnFilesDropped(object sender, FileDragDropEventArgs e)
        {
            string path = e.Files[0];
            InitializeEmulator(path);
        }
    }
}
