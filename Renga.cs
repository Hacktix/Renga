using Chroma;
using Chroma.Graphics;
using Renga.Config;

namespace Renga
{
    public class Renga : Game
    {
        private RengaConfig _conf = new RengaConfig();

        public Renga() : base(new(true, false))
        {
            Window.Title = "れんが";
        }

        protected override void LoadContent()
        {
            base.LoadContent();
            Window.SetIcon(Content.Load<Texture>("icon.png"));
        }
    }
}
