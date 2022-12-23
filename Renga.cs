using Chroma;
using Chroma.Graphics;
using Chroma.Windowing.DragDrop;
using Renga.Core;
using Renga.Config;
using Chroma.Diagnostics;
using System.Numerics;
using Chroma.Diagnostics.Logging;

namespace Renga
{
    public class Renga : Game
    {
        public static Log Log = LogManager.GetForCurrentAssembly();
        public static RengaConfig Config = new RengaConfig();
        private Emulator? _emu;

        private bool _error = false;

        public Renga() : base(new(false, false))
        {
            Window.FilesDropped += WindowOnFilesDropped;
            Window.Title = "れんが";
        }

        public Renga(string[] args) : this()
        {
            string romPath = args[0];

            if(File.Exists(romPath))
                InitializeEmulator(romPath);
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            Window.SetIcon(Content.Load<Texture>("icon.png"));
        }

        private void InitializeEmulator(string romPath)
        {
            _error = false;
            try
            {
                _emu = new Emulator(romPath);
                Window.Title = $"れんが - {_emu.Memory.Cartridge.Title}";
            }
            catch (Exception)
            {
                _emu = null;
                _error = true;
            }
        }

        private void WindowOnFilesDropped(object? sender, FileDragDropEventArgs e)
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

            if (_error)
                context.DrawString("Oops! Renga crashed. Check the logs for details.", new Vector2(Window.Width/10, Window.Height/4), Color.Red);

            try { _emu?.TickFrame(); }
            catch(Exception) {
                _emu = null;
                _error = true;
            }
        }



        private void UpdateWindowTitle()
        {
            string gameTitle = _emu == null ? "No game loaded" : _emu.Memory.Cartridge.Title;
            string windowTitle = $"れんが - {gameTitle} ({(int)PerformanceCounter.FPS} FPS)";
            Window.Title = windowTitle;
        }
    }
}
