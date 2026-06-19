using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace ZombieDefence; 
public struct Zombie {
    public Point position;
    public Stack<Point> path;
    public int health = 8;

    public Zombie(Point position, Stack<Point> path) {
        this.position = position;
        this.path = path;
    }
}

public struct Turret {
    public Point position;
    public float rotation;

    public Turret(Point position) {
        this.position = position;
        this.rotation = 0;
    }
}

public enum TileType {
    ZombieSpawn = -2,
    Unbuildable = -1,
    Empty = 0,
    Target = 1,
    BrickWall = 2,
    SteelWall = 3,
    Turret = 4
}

public struct Tile {
    public TileType Type { get; private set; }
    public int MaxHealth { get; private set; }
    public int health;
    public int Health {
        get { return health; }
        set { health = Math.Clamp(value, 0, MaxHealth); }
    }

    public Tile(TileType type, int health) {
        Type = type;
        MaxHealth = health;
        Health = health;
    }

    public static Tile ZombieSpawn = new Tile(TileType.ZombieSpawn, 0);
    public static Tile Unbuildable = new Tile(TileType.Unbuildable, 0);
    public static Tile Empty = new Tile(TileType.Empty, 0);
    public static Tile Target = new Tile(TileType.Target, 10);
    public static Tile BrickWall = new Tile(TileType.BrickWall, 4);
    public static Tile SteelWall = new Tile(TileType.SteelWall, 6);
    public static Tile Turret = new Tile(TileType.Turret, 2);
}

public partial class GameForm : Form {
    public static GameForm instance;

    public static Vector2 PointToVector2(Point point) {
        return new Vector2(point.X, point.Y);
    }

    public static float EuclideanDist(Point A, Point B) {
        return Vector2.Distance(PointToVector2(A), PointToVector2(B));
    }

    public static bool OnBoard(Point tile) =>
        tile.X >= 0 && tile.X < hTiles &&
        tile.Y >= 0 && tile.Y < vTiles;

    public static Point[] GetValidNeighbours(Point tile) {
        List<Point> neighbours = new List<Point>();
        Point neighbour;
        if (OnBoard(neighbour = new(tile.X - 1, tile.Y))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X, tile.Y - 1))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X + 1, tile.Y))) neighbours.Add(neighbour);
        if (OnBoard(neighbour = new(tile.X, tile.Y + 1))) neighbours.Add(neighbour);
        return neighbours.ToArray();
    }

    public static int ManhattanDist(Point A, Point B) {
        return Math.Abs(A.X - B.X) + Math.Abs(A.Y - B.Y);
    }

    public static (int dist, Point target) GetClosestTarget(Point start) {
        if (targets.Count == 0) {
            return (0, Point.Empty);
        }
        return targets.Select(target => (ManhattanDist(start, target), target)).MinBy(t => t.Item1);
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
            if (tiles[current.X, current.Y].Type == TileType.Target)
                return AssembleCheapestPath(current, cheapestPaths);
            foreach (Point neighbour in GetValidNeighbours(current)) {
                Tile tile = tiles[neighbour.X, neighbour.Y];
                int cost = smallestCost[current] + 1 + tile.Health;
                if (occupied.Contains(neighbour)) cost += 10000;
                if (!smallestCost.ContainsKey(neighbour) ||
                    cost < smallestCost[neighbour]) {
                    smallestCost[neighbour] = cost;
                    cheapestPaths[neighbour] = current;
                    //if (queue.UnorderedItems.Where(item => item.Element == neighbour).Count() == 0)
                    queue.Enqueue(neighbour, smallestCost[neighbour] + GetClosestTargetDist(neighbour));
                }
            }
        }
        if (targets.Count == 0) throw new OperationCanceledException();
        else throw new Exception($"Cannot pathfind from starting point");
    }

    public static Random random = new Random();

    const int hTiles = 24;
    const int vTiles = 16;
    static float tileSize;
    const int buildLimit = 20;
    const int zombieLimit = 22;
    static HashSet<Point> targets = [
        new(0, 9), new(0, 10), new(0, 11),
        new(0, 12), new(0, 13), new(0, 14),
        new(1, 9), new(1, 10), new(1, 11),
        new(1, 12), new(1, 13), new(1, 14)
    ];
    static Tile[,] tiles;
    static Vector2 margin = new Vector2(5, 5);
    static RectangleF gameArea;
    static RectangleF boardArea;
    static List<Zombie> zombies = new List<Zombie>();
    static HashSet<Point> occupied = new HashSet<Point>();
    static Tile selectedTile = Tile.BrickWall;
    static Dictionary<Point, Turret> turrets = [];

    static Thread logicThread = new Thread(RunLogic);

    public static void RunLogic() {
        Stopwatch stopwatch = new Stopwatch();
        const int tickTime = 500;
        try {
            while (true) {
                stopwatch.Start();

                // Logic Start

                occupied.Clear();
                List<int> forRemoval = new List<int>();
                lock (zombies) {
                    foreach (Zombie zombie in zombies) occupied.Add(zombie.position);
                    for (int i = 0; i < zombies.Count; i++) {
                        Zombie zombie = zombies[i];
                        zombie.path = Pathfind(zombie.position);
                        if (zombie.path.Count > 0) {
                            Tile tile = GetTile(zombie.path.Peek());
                            if (tile.Health > 0) {
                                tile.Health--;
                                if (tile.Health == 0) {
                                    if (tile.Type == TileType.Target)
                                        targets.Remove(zombie.path.Peek());
                                    tile = Tile.Empty;
                                }
                                SetTile(zombie.path.Peek(), tile);
                            } else if (occupied.Add(zombie.path.Peek())) {
                                occupied.Remove(zombie.position);
                                zombie.position = zombie.path.Pop();
                            }
                            ;
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

                lock (turrets) {
                    foreach (Point position in turrets.Keys) {
                        Turret turret = turrets[position];
                        Vector2 diff;
                        lock (zombies) {
                            if (zombies.Count == 0) break;
                            (int iClosest, Zombie closestZombie, float distance) = zombies.Select((z, i) => (i, z, EuclideanDist(z.position, turret.position))).MinBy(x => x.Item3);
                            if (distance > 2) continue;
                            closestZombie.health--;
                            if (closestZombie.health <= 0) {
                                zombies.RemoveAt(iClosest);
                                continue;
                            } else zombies[iClosest] = closestZombie;
                            diff = (PointToVector2(closestZombie.position) - PointToVector2(turret.position)) * new Vector2(1, -1);
                        }
                        float angle = MathF.Acos(diff.X / diff.Length());
                        turret.rotation = diff.Y < 0 ? angle : -angle;
                        turrets[position] = turret;
                    }
                }

                // Logic End

                stopwatch.Stop();
                int elapsedTime = (int) stopwatch.ElapsedMilliseconds;

                int timeToSleep = tickTime - elapsedTime;
                if (timeToSleep > 0) Thread.Sleep(timeToSleep);
                //else Console.Error.WriteLine($"Falling behind, tick took {elapsedTime}ms, current tick late {-timeToSleep}ms");
                stopwatch.Reset();
            }
        } catch (ThreadInterruptedException) {

        } catch (OperationCanceledException) {
            MessageBox.Show("The zombies hath eaten thy brains! Game over.");
            instance.BeginInvoke(() => {
                instance.zombieTimer.Stop();
                logicThread.Interrupt();
                instance.Close();
            });
        }
    }

    public GameForm() {
        InitializeComponent();

        CalculateGraphics();

        tiles = new Tile[hTiles, vTiles];
        for (int x = 0; x < hTiles; x++) {
            for (int y = 0; y < vTiles; y++) {
                if (x >= zombieLimit)
                    tiles[x, y] = Tile.ZombieSpawn;
                else if (x >= buildLimit)
                    tiles[x, y] = Tile.Unbuildable;
            }
        }

        foreach (Point target in targets) {
            if (tiles[target.X, target.Y].Type != 0)
                throw new ArgumentException("Target tiles must not be outside of build limit");
            tiles[target.X, target.Y] = Tile.Target;
        }

        instance = this;
    }

    public static Tile GetTile(Point position) {
        return tiles[position.X, position.Y];
    }

    public static void SetTile(Point position, Tile tile) {
        tiles[position.X, position.Y] = tile;
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
    }

    private void GameForm_Load(object sender, EventArgs e) {
        zombieTimer.Start();
        logicThread.Start();
    }

    public Color ApplyOpacity(Color color, float opacity) {
        return Color.FromArgb((int)Math.Clamp(opacity * color.A, 0, 255), color.R, color.G, color.B);
    }

    public void RotatePolygon(ref PointF[] polygon, float angle) {
        for (int i = 0; i < polygon.Length; i++) {
            float x = polygon[i].X;
            float y = polygon[i].Y;
            polygon[i] = new PointF(
                x * MathF.Cos(angle) - y * MathF.Sin(angle),
                x * MathF.Sin(angle) + y * MathF.Cos(angle)
            );
        }
    }
    public void OffsetPolygon(ref PointF[] polygon, PointF offset) {
        for (int i = 0; i < polygon.Length; i++) {
            polygon[i] = new PointF(
                polygon[i].X + offset.X,
                polygon[i].Y + offset.Y
            );
        }
    }

    public void DrawTile(TileType tile, float x, float y, float size, Graphics graphics) {
        DrawTile(tile, x, y, size, 1, 0, graphics);
    }

    public void DrawTile(TileType tile, float x, float y, float size, float opacity, float angle, Graphics graphics) {
        switch (tile) {
            case TileType.ZombieSpawn:
                DrawTile(0, x, y, size, 1, 0, graphics);
                graphics.FillRectangle(
                    new SolidBrush(ApplyOpacity(Color.FromArgb(127, 0, 0, 0), opacity)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    x, y, size, size);
                break;
            case TileType.Unbuildable:
                DrawTile(0, x, y, size, 1, 0, graphics);
                graphics.FillRectangle(
                    new SolidBrush(ApplyOpacity(Color.FromArgb(63, 0, 0, 0), opacity)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    x, y, size, size);
                break;
            case TileType.Empty:
                graphics.FillRectangle(
                    new SolidBrush(ApplyOpacity(Color.LawnGreen, opacity)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(ApplyOpacity(Color.DarkGreen, opacity)),
                    x, y, size, size);
                break;
            case TileType.BrickWall:
                DrawTile(0, x, y, size, 1, 0, graphics);
                graphics.FillRectangle(
                    new SolidBrush(ApplyOpacity(Color.FromArgb(188, 74, 60), opacity)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(ApplyOpacity(Color.FromArgb(89, 45, 29), opacity)),
                    x, y, size, size);
                break;
            case TileType.SteelWall:
                DrawTile(0, x, y, size, 1, 0, graphics);
                graphics.FillRectangle(
                    new SolidBrush(ApplyOpacity(Color.FromArgb(45, 50, 50), opacity)),
                    x, y, size, size);
                graphics.DrawRectangle(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    x, y, size, size);
                break;
            case TileType.Turret:
                DrawTile(0, x, y, size, 1, 0, graphics);
                graphics.FillEllipse(
                    new SolidBrush(ApplyOpacity(Color.LightGray, opacity)),
                    x + size * 0.1f,
                    y + size * 0.1f,
                    size * 0.8f,
                    size * 0.8f
                );
                graphics.DrawEllipse(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    x + size * 0.1f,
                    y + size * 0.1f,
                    size * 0.8f,
                    size * 0.8f
                );
                PointF[] barrelRect = [
                    new PointF(0, size * 0.15f),
                    new PointF(size * 0.5f, size * 0.15f),
                    new PointF(size * 0.5f, size * -0.15f),
                    new PointF(0, size * -0.15f),
                ];
                RotatePolygon(ref barrelRect, angle);
                OffsetPolygon(ref barrelRect, new PointF(x + size * 0.5f, y + size * 0.5f));
                graphics.FillPolygon(
                    new SolidBrush(ApplyOpacity(Color.Gray, opacity)),
                    barrelRect
                );
                graphics.DrawPolygon(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    barrelRect
                );
                graphics.FillEllipse(
                    new SolidBrush(ApplyOpacity(Color.Gray, opacity)),
                    x + size * 0.35f,
                    y + size * 0.35f,
                    size * 0.3f,
                    size * 0.3f
                );
                graphics.DrawEllipse(
                    new Pen(ApplyOpacity(Color.Black, opacity)),
                    x + size * 0.35f,
                    y + size * 0.35f,
                    size * 0.3f,
                    size * 0.3f
                );
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
                if (tiles[x, y].MaxHealth != 0) {
                    DrawTile(
                        tiles[x, y].Type,
                        boardArea.X + x * tileSize,
                        boardArea.Y + y * tileSize,
                        tileSize,
                        (float) tiles[x, y].Health / tiles[x, y].MaxHealth,
                        tiles[x, y].Type == TileType.Turret ? turrets[new Point(x, y)].rotation : 0,
                        graphics
                    );
                } else {
                    DrawTile(
                        tiles[x, y].Type,
                        boardArea.X + x * tileSize,
                        boardArea.Y + y * tileSize,
                        tileSize,
                        graphics
                    );
                }
            }
        }

        DrawTile(TileType.Empty, boardArea.X - 2 * tileSize, boardArea.Y + 6 * tileSize, tileSize, graphics);
        DrawTile(TileType.BrickWall, boardArea.X - 2 * tileSize, boardArea.Y + 7 * tileSize, tileSize, graphics);
        DrawTile(TileType.SteelWall, boardArea.X - 2 * tileSize, boardArea.Y + 8 * tileSize, tileSize, graphics);
        DrawTile(TileType.Turret, boardArea.X - 2 * tileSize, boardArea.Y + 9 * tileSize, tileSize, graphics);

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
        try {
            lock (zombies) {
                Point position = new Point(
                    random.Next(zombieLimit, hTiles),
                    random.Next(0, vTiles)
                );
                zombies.Add(new Zombie(position, Pathfind(position)));
            }
        } catch (OperationCanceledException) {
            zombieTimer.Stop();
        }
        Invalidate();
    }

    private void GameForm_FormClosing(object sender, FormClosingEventArgs e) {
        zombieTimer.Stop();
        logicThread.Interrupt();
    }

    private Point PositionToTile(PointF mouseLocation) {
        mouseLocation.X -= boardArea.X;
        mouseLocation.Y -= boardArea.Y;
        return new Point((int) MathF.Floor(mouseLocation.X / tileSize), (int) MathF.Floor(mouseLocation.Y / tileSize));
    }

    private void GameForm_MouseClick(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        PointF relativeClick = e.Location;
        relativeClick.X -= boardArea.X;
        relativeClick.Y -= boardArea.Y;
        Point clickedTile = PositionToTile(e.Location);
        //MessageBox.Show(
        //    $"Clicked: {e.X}, {e.Y}\n" +
        //    $"Clicked Relative: {relativeClick.X}, {relativeClick.Y}\n" +
        //    $"Clicked tile: {clickedTile.X}, {clickedTile.Y}\n" +
        //    $"Tile size: {tileSize}");
        if (clickedTile.X == -2) {
            if (clickedTile.Y == 6) selectedTile = Tile.Empty;
            else if (clickedTile.Y == 7) selectedTile = Tile.BrickWall;
            else if (clickedTile.Y == 8) selectedTile = Tile.SteelWall;
            else if (clickedTile.Y == 9) selectedTile = Tile.Turret;
        } else if (OnBoard(clickedTile)) {
            Tile tile = tiles[clickedTile.X, clickedTile.Y];
            if (tile.Type < TileType.Empty || tile.Type == TileType.Target) return;
            tiles[clickedTile.X, clickedTile.Y] = selectedTile;
            lock (turrets) {
                turrets.Remove(clickedTile);
                if (selectedTile.Type == TileType.Turret)
                    turrets[clickedTile] = new Turret(clickedTile);
            }
            Invalidate();
        }
    }

    private void GameForm_MouseMove(object sender, MouseEventArgs e) {
        if (e.Button != MouseButtons.Left) return;
        Point clickedTile = PositionToTile(e.Location);
        if (!OnBoard(clickedTile)) return;
        Tile tile = tiles[clickedTile.X, clickedTile.Y];
        if (tile.Type < TileType.Empty || tile.Type == TileType.Target) return;
        tiles[clickedTile.X, clickedTile.Y] = selectedTile;
        lock (turrets) {
            turrets.Remove(clickedTile);
            if (selectedTile.Type == TileType.Turret)
                turrets[clickedTile] = new Turret(clickedTile);
        }
        Invalidate();
    }
}
