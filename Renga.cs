using Chroma;
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
    }
}
