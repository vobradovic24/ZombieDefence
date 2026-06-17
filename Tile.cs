using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZombieDefence; 
internal interface Tile {
    public Point Position { get; set; }

    public string Name { get; }
    public string Description { get; }

    public void RenderPreview(Graphics graphics, float size);
    public void Render(Graphics graphics, float size);
    public void RenderHover(Graphics graphics, float size);
}
