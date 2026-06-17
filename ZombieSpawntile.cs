using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZombieDefence {
    internal class ZombieSpawntile : TickableTile {
        public Point Position { get; set; }

        public string Name => "Zombie Spawn";
        public string Description => "";

        public void RenderPreview(Graphics graphics, float size) {
            throw new NotImplementedException();
        }

        public void Render(Graphics graphics, float size) {
            throw new NotImplementedException();
        }

        public void RenderHover(Graphics graphics, float size) {
            throw new NotImplementedException();
        }

        public void Tick() {
            throw new NotImplementedException();
        }
    }
}
