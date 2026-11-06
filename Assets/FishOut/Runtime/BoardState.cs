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
        public readonly bool exits, hitsCrab, hitsRock;
        public bool ChangesBoard => exits || from != to;
        public MoveResult(int id, Vector2Int start, Vector2Int end, bool escape, bool crab, bool rock = false)
        { fishId = id; from = start; to = end; exits = escape; hitsCrab = crab; hitsRock = rock; }
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
        public int Lives { get; private set; } = 3;
        public bool HintUsed { get; private set; }
        public bool RescueUsed { get; private set; }
        public bool IsClear => fish.Count == 0;
        public bool IsFailed => Lives == 0;
        public IReadOnlyList<FishData> Fish => fish;
        private readonly List<FishData> fish;
        // Spec: rocks and crabs never move or break. Kept for the presentation code that asks.
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
                if (!Inside(o.cell) || !occupied.Add(o.cell)) throw new ArgumentException("Invalid obstacle placement");
            if (fish.Select(f => f.id).Distinct().Count() != fish.Count) throw new ArgumentException("Duplicate fish ID");
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
            var next = f.cell;
            cells?.Add(next);
            var step = Direction(f.facing);
            for (int i = 0; i <= Math.Max(Level.width, Level.height); i++)
            {
                var candidate = next + step;
                if (Level.IsChannel(candidate))
                {
                    cells?.Add(candidate);
                    return new MoveResult(id, f.cell, candidate, true, false);
                }
                if (!Inside(candidate))
                {
                    cells?.Add(candidate);
                    return new MoveResult(id, f.cell, candidate, true, false);
                }
                if (fish.Any(other => other.id != id && other.cell == candidate))
                    return new MoveResult(id, f.cell, next, false, false);
                // A crab ahead always stops the fish and costs a life, every time; a rock just stops it.
                var obstacle = Level.obstacles.Find(o => o.cell == candidate);
                if (obstacle != null)
                    return obstacle.kind == ObstacleKind.Crab
                        ? new MoveResult(id, f.cell, next, false, true)
                        : new MoveResult(id, f.cell, next, false, false, true);
                next = candidate;
                cells?.Add(next);
            }
            throw new InvalidOperationException("Board traversal did not terminate");
        }
        public MoveResult Move(int id)
        {
            if (IsFailed || IsClear) throw new InvalidOperationException("Stage has ended");
            var result = TraceForward(id, null);
            if (result.exits) fish.Remove(Find(id));
            else Find(id).cell = result.to;
            if (result.hitsCrab) Lives = Math.Max(0, Lives - 1);
            return result;
        }
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
        // The fish directly ahead, if that is what is holding this one in place.
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
            // Spec: only a fish that can leave now; otherwise the hint is not used up.
            var id = FindEscapingFish();
            if (id >= 0) HintUsed = true;
            return id;
        }
        public bool Rescue(int id)
        {
            if (RescueUsed || IsFailed || Find(id) == null) return false;
            fish.Remove(Find(id));
            RescueUsed = true;
            return true;
        }
        public bool IsDeadlocked
        {
            get
            {
                if (IsClear || IsFailed) return false;
                // Spec: deadlocked when no fish can move or escape (bumping into a crab or rock moves nothing).
                foreach (var f in fish) if (Preview(f.id).ChangesBoard) return false;
                return true;
            }
        }
    }
}
