using System.Diagnostics;
using System.Numerics;

namespace ZombieDefence; 
public struct Zombie() {
    public Point position;
    public Stack<Point> path;

    public Zombie(Point position, Stack<Point> path) : this() {
        this.position = position;
        this.path = path;
    }
}

public partial class GameForm : Form {
    public static GameForm instance;

    public static Dictionary<int, int> defaultTileHealth = new Dictionary<int, int>() {
        { -2, 0 }, { -1, 0 }, { 0, 0 }, { 1, 0 },
        { 2, 4 }, { 3, 6 },
        { 4, 2 }
    };

    public static bool OnBoard(Point tile) =>
        tile.X >= 0 && tile.X < hTiles &&
        tile.Y >= 0 && tile.Y < vTiles;

    public static Point[] GetNeighbours(Point tile) {
        List<Point> neighbours = new List<Point>();
        Point neighbour;
        if (OnBoard(neighbour = new(tile.X - 1, tile.Y))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X, tile.Y - 1))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X + 1, tile.Y))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X, tile.Y + 1))) neighbours.Add(neighbour);
        return neighbours.ToArray();
    }

    public static int Dist(Point A, Point B) {
        return Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    }

    public static (int dist, Point target) GetClosestTarget(Point start) {
        return targets.Select(target => (Dist(start, target), target)).MinBy(t => t.Item1);
    }

    public static Point GetClosestTargetPoint(Point start) {
        return GetClosestTarget(start).target;
    }

    public static int GetClosestTargetDist(Point start) {
        return GetClosestTarget(start).dist;
    }

    public static Stack<Point> AssembleCheapestPath(Point current, Dictionary<Point, Point> cheapestPaths) {
        Stack<Point> cheapestPath = new Stack<Point>();
        while (cheapestPaths.TryGetValue(current, out Point previous)) {
            cheapestPath.Push(current);
            current = previous;
        }
        return cheapestPath;
    }

    public static Stack<Point> Pathfind(Point start) {
        Dictionary<Point, Point> cheapestPaths = new(); // point -> prev point in cheapest path
        Dictionary<Point, int> smallestCost = new(); // point -> cost of shortest path to that point
        PriorityQueue<Point, int> queue = new PriorityQueue<Point, int>();
        queue.Enqueue(start, GetClosestTargetDist(start));
        smallestCost[start] = 0;
        while (queue.Count > 0) {
            Point current = queue.Dequeue();
            if (tiles[current.X, current.Y] == 1)
                return AssembleCheapestPath(current, cheapestPaths);
            foreach (Point neighbour in GetNeighbours(current)) {
                int tile = tiles[neighbour.X, neighbour.Y];
                int cost = smallestCost[current] + 1 + tileHealths[neighbour.X, neighbour.Y];
                if (!smallestCost.ContainsKey(neighbour) ||
                    cost < smallestCost[neighbour]) {
                    smallestCost[neighbour] = cost;
                    cheapestPaths[neighbour] = current;
                    //if (queue.UnorderedItems.Where(item => item.Element == neighbour).Count() == 0)
                    queue.Enqueue(neighbour, smallestCost[neighbour] + GetClosestTargetDist(neighbour));
                }
            }
        }
        throw new Exception("Cannot pathfind from starting point");
    }

    public static Random random = new Random();

    const int hTiles = 24;
    const int vTiles = 16;
    static float tileSize;
    const int buildLimit = 16;
    const int zombieLimit = 18;
    static Point[] targets = {
        new(0, 9), new(0, 10), new(0, 11),
        new(0, 12), new(0, 13), new(0, 14),
        new(1, 9), new(1, 10), new(1, 11),
        new(1, 12), new(1, 13), new(1, 14)
    };
    static int[,] tiles;
    static int[,] tileHealths;
    static Vector2 margin = new Vector2(5, 5);
    static Vector2 padding;
    static RectangleF gameArea;
    static RectangleF boardArea;
    static List<Zombie> zombies = new List<Zombie>();
    static bool mapUpdated = false;
    static int selectedTile = 2;

    Thread logicThread = new Thread(RunLogic);

    public static void RunLogic() {
        Stopwatch stopwatch = new Stopwatch();
        const int tickTime = 500;
        try {
            while (true) {
                stopwatch.Start();

                // Logic Start

                List<int> forRemoval = new List<int>();
                lock (zombies) {
                    HashSet<Point> occupied = new HashSet<Point>();
                    for (int i = 0; i < zombies.Count; i++) {
                        Zombie zombie = zombies[i];
                        if (mapUpdated) zombie.path = Pathfind(zombie.position);
                        if (zombie.path.Count > 0) {
                            if (occupied.Add(zombie.path.Peek())) {
                                zombie.position = zombie.path.Pop();
                            } else occupied.Add(zombie.position);
                        } else {
                            forRemoval.Add(i);
                            // deal damage
                        }
                        zombies[i] = zombie;
                    }
                    forRemoval.Reverse();
                    foreach (int i in forRemoval)
                        zombies.RemoveAt(i);
                }
                if (forRemoval.Count > 0 || zombies.Count > 0)
                    instance.Invoke(() => instance.Invalidate());
                mapUpdated = false;

                // Logic End

                stopwatch.Stop();
                int elapsedTime = (int)stopwatch.ElapsedMilliseconds;

                int timeToSleep = tickTime - elapsedTime;
                if (timeToSleep > 0) Thread.Sleep(timeToSleep);
                //else Console.Error.WriteLine($"Falling behind, tick took {elapsedTime}ms, current tick late {-timeToSleep}ms");
                stopwatch.Reset();
            }
        } catch (ThreadInterruptedException) {

        }
    }

    public GameForm() {
        InitializeComponent();

        CalculateGraphics();

        tiles = new int[hTiles, vTiles];
        for (int x = 0; x < hTiles; x++) {
            for (int y = 0; y < vTiles; y++) {
                if (x >= zombieLimit)
                    tiles[x, y] = -2;
                else if (x >= buildLimit)
                    tiles[x, y] = -1;
            }
        }

        foreach (Point target in targets) {
            if (tiles[target.X, target.Y] != 0)
                throw new ArgumentException("Target tiles must not be outside of build limit");
            tiles[target.X, target.Y] = 1;
        }

        tileHealths = new int[hTiles, vTiles];

        instance = this;
    }

    private void CalculateGraphics() {
        gameArea = new RectangleF(
            margin.X,
            margin.Y,
            ClientSize.Width - margin.X * 2,
            ClientSize.Height - margin.Y * 2
        );
        tileSize = MathF.Min(
            gameArea.Width / (hTiles + 3),
            gameArea.Height / vTiles
        );
        boardArea = new RectangleF(
            gameArea.X + 3 * tileSize + (gameArea.Width - (hTiles + 3) * tileSize) / 2,
            gameArea.Y + (gameArea.Height - vTiles * tileSize) / 2,
            hTiles * tileSize,
            vTiles * tileSize
        );
        padding = new Vector2(3 * tileSize, tileSize);
    }

    private void GameForm_Load(object sender, EventArgs e) {
        zombieTimer.Start();
        logicThread.Start();
    }

    public void DrawTile(int tile, float x, float y, float size, Graphics graphics) {
        switch (tile) {
            case -2:
                DrawTile(0, x, y, size, graphics);
                graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(127, 0, 0, 0)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    Pens.Black,
                    x, y, size, size);
                break;
            case -1:
                DrawTile(0, x, y, size, graphics);
                graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(63, 0, 0, 0)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    Pens.Black,
                    x, y, size, size);
                break;
            case 0:
                graphics.FillRectangle(
                    Brushes.LawnGreen,
                    x, y, size, size);
                graphics.DrawRectangle(
                    Pens.DarkGreen,
                    x, y, size, size);
                break;
            case 2:
                graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(188, 74, 60)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(Color.FromArgb(89, 45, 29)),
                    x, y, size, size);
                break;
            case 3:
                graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(45, 50, 50)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    Pens.Black,
                    x, y, size, size);
                break;
            default:
                graphics.DrawLine(
                    Pens.Red,
                    x,
                    y,
                    x + tileSize,
                    y + tileSize
                );
                graphics.DrawLine(
                    Pens.Red,
                    x,
                    y + tileSize,
                    x + tileSize,
                    y
                );
                break;
        }
    }

    private void Form1_Paint(object sender, PaintEventArgs e) {
        Graphics graphics = e.Graphics;

        graphics.DrawRectangle(new Pen(Color.Black, 2), gameArea);
        graphics.DrawRectangle(new Pen(Color.Black, 2), boardArea);

        for (int x = 0; x < hTiles; x++) {
            for (int y = 0; y < vTiles; y++) {
                DrawTile(
                    tiles[x, y],
                    boardArea.X + x * tileSize,
                    boardArea.Y + y * tileSize,
                    tileSize,
                    graphics
                );
            }
        }


        DrawTile(2, boardArea.X - 2 * tileSize, boardArea.Y + 6 * tileSize, tileSize, graphics);
        DrawTile(3, boardArea.X - 2 * tileSize, boardArea.Y + 7 * tileSize, tileSize, graphics);

        lock (zombies) {
            foreach (var zombie in zombies) {
                graphics.FillEllipse(
                    Brushes.DarkGreen,
                    boardArea.X + zombie.position.X * tileSize + 1,
                    boardArea.Y + zombie.position.Y * tileSize + 1,
                    tileSize - 1,
                    tileSize - 1
                );
                graphics.DrawEllipse(
                    Pens.Black,
                    boardArea.X + zombie.position.X * tileSize + 1,
                    boardArea.Y + zombie.position.Y * tileSize + 1,
                    tileSize - 2,
                    tileSize - 2
                );
            }
        }
    }

    private void Form1_Resize(object sender, EventArgs e) {
        CalculateGraphics();
        Invalidate();
    }

    private void zombieTimer_Tick(object sender, EventArgs e) {
        lock (zombies) {
            Point position = new Point(
                random.Next(zombieLimit, hTiles),
                random.Next(0, vTiles)
            );
            zombies.Add(new Zombie(position, Pathfind(position)));
        }
        Invalidate();
    }

    private void GameForm_FormClosing(object sender, FormClosingEventArgs e) {
        zombieTimer.Stop();
        logicThread.Interrupt();
    }

    private Point MouseToTile(PointF mouseLocation) {
        mouseLocation.X -= boardArea.X;
        mouseLocation.Y -= boardArea.Y;
        return new Point((int)MathF.Floor(mouseLocation.X / tileSize), (int) MathF.Floor(mouseLocation.Y / tileSize));
    }

    private void GameForm_MouseClick(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        PointF relativeClick = e.Location;
        relativeClick.X -= boardArea.X;
        relativeClick.Y -= boardArea.Y;
        Point clickedTile = MouseToTile(e.Location);
        //MessageBox.Show(
        //    $"Clicked: {e.X}, {e.Y}\n" +
        //    $"Clicked Relative: {relativeClick.X}, {relativeClick.Y}\n" +
        //    $"Clicked tile: {clickedTile.X}, {clickedTile.Y}\n" +
        //    $"Tile size: {tileSize}");
        if (clickedTile.X == -2) {
            if (clickedTile.Y == 6) selectedTile = 2;
            else if (clickedTile.Y == 7) selectedTile = 3;
            else if (clickedTile.Y == 9) selectedTile = 4;
        } else if (OnBoard(clickedTile)) {
            int tile = tiles[clickedTile.X, clickedTile.Y];
            if (tile < 0 || tile == 1) return;
            tiles[clickedTile.X, clickedTile.Y] = selectedTile;
            mapUpdated = true;
            Invalidate();
        }
    }
}
