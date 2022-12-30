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

        public static string? ErrorString;

        private RenderTarget Frame;
        private Vector2 FrameScale;

        public Renga() : base(new(false, false))
        {
            Window.FilesDropped += WindowOnFilesDropped;
            Window.Title = "れんが";

            int sizeX = (int)Config.GetProperty("scale", 3L) * 160;
            int sizeY = (int)Config.GetProperty("scale", 3L) * 144;
            Window.Size = new System.Drawing.Size(sizeX, sizeY);

            Frame = new RenderTarget(160, 144);
            Frame.FilteringMode = TextureFilteringMode.NearestNeighbor;
            FrameScale = new Vector2(Window.Width / 160f, Window.Height / 144f);

            Graphics.VerticalSyncMode = VerticalSyncMode.None;
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
            ErrorString = null;
            try
            {
                _emu = new Emulator(romPath);
                Window.Title = $"れんが - {_emu.Memory.Cartridge.Title}";
            }
            catch (Exception e)
            {
                _emu = null;
                ErrorString = $"Failed to initialize Emulator: {e.Message}";
            }
        }

        private void WindowOnFilesDropped(object? sender, FileDragDropEventArgs e)
        {
            string path = e.Files[0];
            InitializeEmulator(path);
        }



        protected override void FixedUpdate(float delta)
        {
            UpdateWindowTitle();
            _emu?.Tick(delta);
        }

        protected override void Draw(RenderContext context)
        {
            base.Draw(context);

            if (ErrorString != null)
                context.DrawString($"Oops, Renga crashed!\n\nError:\n{ErrorString}", new Vector2(10, 10), Color.Red);

            try {
                if(_emu != null)
                {
                    context.RenderTo(Frame, () =>
                    {
                        for(int i = 0; i < _emu.PPU.Pixels.Length; i++)
                            context.Pixel(i % 160, i / 160, _emu.PPU.Pixels[i]);
                    });
                    context.DrawTexture(Frame, Vector2.Zero, FrameScale);
                }
            }
            catch(Exception e) {
                _emu = null;
                ErrorString = e.Message;
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
