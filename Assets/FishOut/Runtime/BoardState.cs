using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FishOut
{
    public enum Facing { Up, Right, Down, Left }
    public enum ObstacleKind { Rock, Crab }

    [Serializable]
    public sealed class FishData
    {
        public int id;
        public Vector2Int cell;
        public Facing facing;
        public int appearance;
        // A crab is a piece like a fish (it swims out the same way) that carries a bump counter.
        public bool crab;
        public FishData Copy() => (FishData)MemberwiseClone();
    }

    [Serializable]
    public sealed class ObstacleData
    {
        public Vector2Int cell;
        public ObstacleKind kind;
    }

    [Serializable]
    public sealed class LevelData
    {
        public int number;
        public int width = 6;
        public int height = 8;
        public List<FishData> fish = new List<FishData>();
        public List<ObstacleData> obstacles = new List<ObstacleData>();
        public string title;
        public bool showcase;
        public Vector2Int gate;
        public List<Vector2Int> channelCells = new List<Vector2Int>();
        public List<Vector2[]> channelLines = new List<Vector2[]>();
        private HashSet<Vector2Int> channelSet;
        public bool IsChannel(Vector2Int cell)
        {
            if (!showcase) return false;
            if (channelSet == null) channelSet = new HashSet<Vector2Int>(channelCells);
            return channelSet.Contains(cell);
        }
    }

    public readonly struct MoveResult
    {
        public readonly int fishId;
        public readonly Vector2Int from, to;
        // hitsCrab: the piece it runs into is a crab. crabStops: the moving piece is a crab and ends against something.
        public readonly bool exits, hitsCrab, hitsRock, crabStops;
        public readonly int struckId;
        public bool ChangesBoard => exits || from != to;
        // Every bump a crab takes counts its number down: being run into, or running into anything itself.
        public bool CostsCrab => hitsCrab || crabStops;
        public MoveResult(int id, Vector2Int start, Vector2Int end, bool escape, bool crab, bool rock = false, int struck = -1, bool crabStop = false)
        { fishId = id; from = start; to = end; exits = escape; hitsCrab = crab; hitsRock = rock; struckId = struck; crabStops = crabStop; }
    }

    public sealed class BoardPath
    {
        public MoveResult Result { get; }
        public LevelData Level { get; }
        // Includes the start cell, each traversable cell, and the first outside cell on escape.
        public IReadOnlyList<Vector2Int> Cells { get; }
        internal BoardPath(MoveResult result, List<Vector2Int> cells, LevelData level)
        { Result = result; Cells = cells.AsReadOnly(); Level = level; }
    }

    // All board rules live here. Animation and UI cannot change rule outcomes.
    public sealed class BoardState
    {
        public LevelData Level { get; }
        // Each crab carries a number: every bump counts it down, and the stage is lost when one reaches 0.
        public const int CrabStrength = 3;
        public bool HintUsed { get; private set; }
        public bool RescueUsed { get; private set; }
        // Fish and crabs alike must reach the cave.
        public bool IsClear => fish.Count == 0;
        public bool CrabExhausted => crabLeft.Values.Any(n => n <= 0);
        // Lost when no sequence of taps (plus the Rescue still in hand) can bring every piece to the cave.
        public bool NoWayOut { get; private set; }
        public bool IsFailed => CrabExhausted || NoWayOut;
        public int CrabLeft(int id) => crabLeft.TryGetValue(id, out var n) ? n : 0;
        public bool IsCrab(int id) { var f = Find(id); return f != null && f.crab; }
        private readonly Dictionary<int, int> crabLeft = new Dictionary<int, int>();
        public IReadOnlyList<FishData> Fish => fish;
        private readonly List<FishData> fish;
        // Spec: rocks never move or break. Kept for the presentation code that asks.
        public bool IsCracked(Vector2Int cell) => false;
        public bool IsBroken(Vector2Int cell) => false;
        public BoardState(LevelData level)
        {
            Level = level;
            fish = level.fish.Select(f => f.Copy()).ToList();
            var occupied = new HashSet<Vector2Int>();
            foreach (var f in fish)
                if (!Inside(f.cell) || level.IsChannel(f.cell) || !occupied.Add(f.cell)) throw new ArgumentException("Invalid fish placement");
            foreach (var o in level.obstacles)
            {
                if (!Inside(o.cell) || !occupied.Add(o.cell)) throw new ArgumentException("Invalid obstacle placement");
                if (o.kind == ObstacleKind.Crab) throw new ArgumentException("Crabs are pieces now (FishData.crab), not obstacles");
            }
            if (fish.Select(f => f.id).Distinct().Count() != fish.Count) throw new ArgumentException("Duplicate fish ID");
            foreach (var f in fish) if (f.crab) crabLeft[f.id] = CrabStrength;
            Reassess();
        }
        public static Vector2Int Direction(Facing direction)
        {
            switch (direction)
            {
                case Facing.Up: return Vector2Int.up;
                case Facing.Right: return Vector2Int.right;
                case Facing.Down: return Vector2Int.down;
                default: return Vector2Int.left;
            }
        }
        public bool Inside(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < Level.width && p.y < Level.height;
        public FishData Find(int id) => fish.Find(f => f.id == id);
        public MoveResult Preview(int id) => TraceForward(id, null);
        public BoardPath CalculatePath(int id)
        {
            var cells = new List<Vector2Int>();
            var result = TraceForward(id, cells);
            return new BoardPath(result, cells, Level);
        }
        // Grid line check is authoritative, so rig animation/collider sizes cannot change the rules.
        private MoveResult TraceForward(int id, List<Vector2Int> cells)
        {
            var f = Find(id) ?? throw new ArgumentException("Unknown fish");
            return Trace(f, f.cell, c => { var other = fish.Find(o => o.id != id && o.cell == c); return other; }, cells);
        }
        private MoveResult Trace(FishData piece, Vector2Int start, Func<Vector2Int, FishData> pieceAt, List<Vector2Int> cells)
        {
            int id = piece.id;
            var next = start;
            cells?.Add(next);
            var step = Direction(piece.facing);
            for (int i = 0; i <= Math.Max(Level.width, Level.height); i++)
            {
                var candidate = next + step;
                if (Level.IsChannel(candidate) || !Inside(candidate))
                {
                    cells?.Add(candidate);
                    return new MoveResult(id, start, candidate, true, false);
                }
                // A piece ahead stops it; a crab ahead (or a crab doing the moving) takes a bump.
                var other = pieceAt(candidate);
                if (other != null)
                    return new MoveResult(id, start, next, false, other.crab, false, other.id, piece.crab);
                if (Level.obstacles.Exists(o => o.cell == candidate))
                    return new MoveResult(id, start, next, false, false, true, -1, piece.crab);
                next = candidate;
                cells?.Add(next);
            }
            throw new InvalidOperationException("Board traversal did not terminate");
        }
        public MoveResult Move(int id)
        {
            if (IsFailed || IsClear) throw new InvalidOperationException("Stage has ended");
            var result = TraceForward(id, null);
            if (result.hitsCrab) Bump(result.struckId);
            if (result.crabStops) Bump(id);
            if (result.exits) { fish.Remove(Find(id)); crabLeft.Remove(id); }
            else Find(id).cell = result.to;
            Reassess();
            return result;
        }
        private void Bump(int crab) { if (crabLeft.ContainsKey(crab)) crabLeft[crab] = Math.Max(0, crabLeft[crab] - 1); }
        // Touching fish facing the same way form one line. Once its front fish can escape, the
        // whole line goes, front first. Empty when the line cannot escape.
        public List<int> EscapingLine(int id)
        {
            var line = new List<int>();
            var tapped = Find(id) ?? throw new ArgumentException("Unknown fish");
            var step = Direction(tapped.facing);
            var head = tapped;
            for (var ahead = FishAt(head.cell + step, tapped.facing); ahead != null; ahead = FishAt(head.cell + step, tapped.facing)) head = ahead;
            if (!Preview(head.id).exits) return line;
            for (var member = head; member != null; member = FishAt(member.cell - step, tapped.facing)) line.Add(member.id);
            return line;
        }
        private FishData FishAt(Vector2Int cell, Facing facing) => fish.Find(f => f.cell == cell && f.facing == facing);
        // The piece directly ahead, if that is what is holding this one in place.
        public FishData BlockerOf(int id)
        {
            var f = Find(id);
            return f == null ? null : fish.Find(other => other.cell == f.cell + Direction(f.facing));
        }
        public int FindEscapingFish()
        {
            foreach (var f in fish) if (Preview(f.id).exits) return f.id;
            return -1;
        }
        public int UseHint()
        {
            if (HintUsed || IsFailed || IsClear) return -1;
            // Spec: only a piece that can leave now; otherwise the hint is not used up.
            var id = FindEscapingFish();
            if (id >= 0) HintUsed = true;
            return id;
        }
        public bool Rescue(int id)
        {
            if (RescueUsed || IsFailed || Find(id) == null) return false;
            fish.Remove(Find(id));
            crabLeft.Remove(id);
            RescueUsed = true;
            Reassess();
            return true;
        }
        public bool IsDeadlocked
        {
            get
            {
                if (IsClear || IsFailed) return false;
                // Spec: deadlocked when no piece can move or escape (bumping into something moves nothing).
                foreach (var f in fish) if (Preview(f.id).ChangesBoard) return false;
                return true;
            }
        }

        // Small boards: a depth-first search over piece positions and crab numbers. A move that would bring a crab to 0
        // is never taken, and taps that move nothing are skipped. If the search runs out of budget the board counts as
        // still solvable, so a player is never failed by mistake.
        private const int SearchBudget = 6000;
        private void Reassess() { NoWayOut = fish.Count > 0 && !CrabExhausted && !CanClear(); }
        public bool CanClear()
        {
            var n = fish.Count;
            var cells = new Vector2Int?[n];
            var left = new int[n];
            for (int i = 0; i < n; i++) { cells[i] = fish[i].cell; left[i] = fish[i].crab ? CrabLeft(fish[i].id) : 0; }
            var seen = new HashSet<string>();
            bool gaveUp = false;
            bool found = Search(cells, left, !RescueUsed, seen, ref gaveUp);
            return found || gaveUp;
        }
        private bool Search(Vector2Int?[] cells, int[] left, bool rescue, HashSet<string> seen, ref bool gaveUp)
        {
            int alive = 0;
            foreach (var c in cells) if (c.HasValue) alive++;
            if (alive == 0 || (rescue && alive == 1)) return true;
            var key = new System.Text.StringBuilder(rescue ? "R" : "-");
            for (int i = 0; i < cells.Length; i++) key.Append(cells[i].HasValue ? cells[i].Value.x + "," + cells[i].Value.y + "," + left[i] + ";" : "x;");
            if (!seen.Add(key.ToString())) return false;
            if (seen.Count > SearchBudget) { gaveUp = true; return true; }
            var at = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < cells.Length; i++) if (cells[i].HasValue) at[cells[i].Value] = i;
            var moves = new List<KeyValuePair<int, MoveResult>>();
            for (int i = 0; i < cells.Length; i++)
            {
                if (!cells[i].HasValue) continue;
                var self = cells[i].Value;
                var r = Trace(fish[i], self, c => c != self && at.TryGetValue(c, out var j) ? fish[j] : null, null);
                if (!r.ChangesBoard) continue;
                if (r.hitsCrab && left[at[r.to + Direction(fish[i].facing)]] <= 1) continue;
                if (r.crabStops && left[i] <= 1) continue;
                moves.Add(new KeyValuePair<int, MoveResult>(i, r));
            }
            // Escapes first: they usually lead to a solution fastest.
            foreach (var m in moves.OrderBy(m => m.Value.exits ? 0 : 1))
            {
                var next = (Vector2Int?[])cells.Clone();
                var nextLeft = (int[])left.Clone();
                next[m.Key] = m.Value.exits ? (Vector2Int?)null : m.Value.to;
                if (m.Value.hitsCrab) nextLeft[at[m.Value.to + Direction(fish[m.Key].facing)]]--;
                if (m.Value.crabStops) nextLeft[m.Key]--;
                if (Search(next, nextLeft, rescue, seen, ref gaveUp)) return true;
            }
            if (rescue)
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!cells[i].HasValue) continue;
                    var next = (Vector2Int?[])cells.Clone(); next[i] = null;
                    if (Search(next, left, false, seen, ref gaveUp)) return true;
                }
            return false;
        }
    }
}
