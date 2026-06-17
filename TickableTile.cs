using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZombieDefence {
    internal interface TickableTile : Tile {
        public abstract void Tick();
    }
}
