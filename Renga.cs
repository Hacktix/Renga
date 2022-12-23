using Chroma;
using Chroma.Graphics;
using Chroma.Windowing.DragDrop;
using Renga.Core;
using Renga.Config;
using Chroma.Diagnostics;

namespace Renga
{
    public class Renga : Game
    {
        public static RengaConfig Config = new RengaConfig();
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



        protected override void Update(float delta)
        {
            base.Update(delta);
            UpdateWindowTitle();
        }

        protected override void Draw(RenderContext context)
        {
            base.Draw(context);

            if(_emu !=  null)
                _emu.TickFrame();
        }



        private void UpdateWindowTitle()
        {
            string gameTitle = _emu == null ? "No game loaded" : _emu.Memory.Cartridge.Title;
            string windowTitle = $"れんが - {gameTitle} ({(int)PerformanceCounter.FPS} FPS)";
            Window.Title = windowTitle;
        }
    }
}
