using System.Diagnostics;

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

    public static Dictionary<int, int> pathfindingCost = new Dictionary<int, int>() {
        { -2, 1 }, { -1, 1 }, { 0, 1 }, { 1, 1 },
        { 2, 3 }, { 3, 4 }, { 4, 5 },
        { 5, 2 }
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
                int cost = smallestCost[current] + pathfindingCost[tile];
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

    Dictionary<int, Action<Graphics, int, int, int>> tileDrawFuncs = new Dictionary<int, Action<Graphics, int, int, int>>() {
        { -2, (graphics, x, y, size) => {
            graphics.FillRectangle(
                new SolidBrush(Color.FromArgb(127, 255, 0, 0)),
                x, y, size, size);
            graphics.DrawRectangle(
                Pens.Black,
                x, y, size, size);
        } },
        { -1, (graphics, x, y, size) => {
            graphics.FillRectangle(
                new SolidBrush(Color.FromArgb(63, 0, 0, 0)),
                x, y, size, size);
            graphics.DrawRectangle(
                Pens.Black,
                x, y, size, size);
        } },
        { 0, (Graphics graphics, int x, int y, int size) => {
            graphics.DrawRectangle(
                Pens.Black,
                x, y, size, size);
        } }
    };

    const int hTiles = 32;
    const int vTiles = 24;
    static int tileSize;
    const int buildLimit = 24;
    const int zombieLimit = 26;
    static Point[] targets = {
        new(0, 9), new(0, 10), new(0, 11),
        new(0, 12), new(0, 13), new(0, 14),
        new(1, 9), new(1, 10), new(1, 11),
        new(1, 12), new(1, 13), new(1, 14)
    };
    static int[,] tiles;
    static Rectangle boardArea;
    static List<Zombie> zombies = new List<Zombie>();
    static bool mapUpdated = false;

    Thread logicThread = new Thread(() => {
        Stopwatch stopwatch = new Stopwatch();
        const int tickTime = 500;
        while (true) {
            stopwatch.Start();

            // Logic Start

            lock (zombies) {
                List<Point> occupied = new List<Point>();
                List<int> forRemoval = new List<int>();
                for (int i = 0; i < zombies.Count; i++) {
                    Zombie zombie = zombies[i];
                    if (mapUpdated) zombie.path = Pathfind(zombie.position);
                    if (zombie.path.Count > 0) {
                        if (!occupied.Contains(zombie.path.Peek())) {
                            zombie.position = zombie.path.Pop();
                            occupied.Add(zombie.position);
                        }
                    } else {
                        forRemoval.Add(i);
                        // deal damage
                    }
                    zombies[i] = zombie;
                }
                forRemoval.Reverse();
                foreach (int i in forRemoval)
                    zombies.RemoveAt(i);
                if (zombies.Count > 0 || forRemoval.Count > 0)
                    instance.Invoke(instance.Invalidate);
            }
            mapUpdated = false;

            // Logic End

            stopwatch.Stop();
            int elapsedTime = (int)stopwatch.ElapsedMilliseconds;

            int timeToSleep = tickTime - elapsedTime;
            if (timeToSleep > 0) Thread.Sleep(timeToSleep);
            else Console.Error.WriteLine($"Falling behind, tick took {elapsedTime}ms, current tick late {-timeToSleep}ms");
            stopwatch.Reset();
        }
    });

    public GameForm() {
        InitializeComponent();


        tileSize = Math.Min(ClientSize.Width / hTiles, ClientSize.Height / vTiles);
        boardArea = new Rectangle(
            (ClientSize.Width - hTiles * tileSize) / 2,
            (ClientSize.Height - vTiles * tileSize) / 2,
            hTiles * tileSize,
            vTiles * tileSize
        );

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

        instance = this;
    }

    private void GameForm_Load(object sender, EventArgs e) {
        zombieTimer.Start();
        logicThread.Start();
    }

    private void Form1_Paint(object sender, PaintEventArgs e) {
        Graphics graphics = e.Graphics;

        for (int x = 0; x < hTiles; x++) {
            for (int y = 0; y < vTiles; y++) {

                if (tileDrawFuncs.TryGetValue(tiles[x, y], out var drawFunction)) {
                    drawFunction.Invoke(
                        graphics,
                        boardArea.X + x * tileSize,
                        boardArea.Y + y * tileSize,
                        tileSize
                    );
                } else {
                    graphics.DrawLine(
                        Pens.Red,
                        boardArea.X + x * tileSize,
                        boardArea.Y + y * tileSize,
                        boardArea.X + x * tileSize + tileSize,
                        boardArea.Y + y * tileSize + tileSize
                    );
                    graphics.DrawLine(
                        Pens.Red,
                        boardArea.X + x * tileSize,
                        boardArea.Y + y * tileSize + tileSize,
                        boardArea.X + x * tileSize + tileSize,
                        boardArea.Y + y * tileSize
                    );
                }
            }
        }

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
                    Pens.DarkOliveGreen,
                    boardArea.X + zombie.position.X * tileSize + 1,
                    boardArea.Y + zombie.position.Y * tileSize + 1,
                    tileSize - 2,
                    tileSize - 2
                );
            }
        }
    }

    private void Form1_Resize(object sender, EventArgs e) {
        tileSize = Math.Min(ClientSize.Width / hTiles, ClientSize.Height / vTiles);
        boardArea = new Rectangle(
            (ClientSize.Width - hTiles * tileSize) / 2,
            (ClientSize.Height - vTiles * tileSize) / 2,
            hTiles * tileSize,
            vTiles * tileSize
        );
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
    }
}
