using UnityEngine;

namespace FishOut
{
    public static class FishOutLevels
    {
        public const int Count = 9;
        public static LevelData Get(int number)
        {
            if (number >= 6) return ShowcaseLevels.Get(Mathf.Clamp(number,6,Count));
            var l = new LevelData { number = Mathf.Clamp(number, 1, Count) };
            switch (l.number)
            {
                case 1:
                    Add(l, new[,] { {1,2,0}, {3,4,1}, {4,6,2} });
                    break;
                case 2:
                    // Fish 0 can advance to (3,2), and stays there until fish 1 leaves.
                    Add(l, new[,] { {0,2,1}, {4,2,0}, {4,5,1}, {2,1,0}, {2,4,3},
                        {0,6,0}, {1,0,2}, {5,7,1}, {3,6,3}, {5,0,2} });
                    break;
                case 3:
                    Rocks(l, new[,] {{2,3},{3,4},{1,5}});
                    Add(l, new[,] {{4,1,0},{4,5,1},{3,0,3},{3,2,3},{5,4,2},{3,6,1},{0,1,0},{5,7,1},
                        {0,0,3},{2,0,2},{1,2,3},{0,3,3},{1,7,0},{5,3,1},{1,0,2},{0,7,3},{2,4,0},{5,5,1},{3,7,0},{4,0,2}});
                    break;
                case 4:
                    Crabs(l, new[,] {{3,4}});
                    Add(l, new[,] {{3,3,0},{0,6,1},{1,3,3},{0,4,2},{4,2,3},{2,6,1},{0,7,3},{5,2,0},
                        {1,0,2},{4,6,1},{3,5,0},{1,5,0},{5,6,0},{4,1,1},{3,2,2},{1,7,1},{4,7,1},{5,1,2},{3,6,0},{0,1,3}});
                    break;
                default:
                    Rocks(l, new[,] {{2,3},{3,4},{1,5}});
                    Crabs(l, new[,] {{4,5},{1,2}});
                    Add(l, new[,] {{4,4,0},{5,6,0},{0,7,1},{0,5,3},{3,1,1},{3,6,3},{3,3,1},{4,7,0},
                        {1,0,2},{1,4,3},{0,1,2},{4,0,1},{1,6,3},{3,2,1},{0,0,2},{2,5,0},{0,3,3},{4,2,1},{1,7,0},{0,4,3}});
                    break;
            }
            return l;
        }
        private static void Add(LevelData l, int[,] entries)
        {
            for (var i = 0; i < entries.GetLength(0); i++) l.fish.Add(new FishData
            { id = i, cell = new Vector2Int(entries[i,0], entries[i,1]), facing = (Facing)entries[i,2], appearance = i % 9 });
        }
        private static void Rocks(LevelData l, int[,] cells) => Obstacles(l, cells, ObstacleKind.Rock);
        private static void Crabs(LevelData l, int[,] cells) => Obstacles(l, cells, ObstacleKind.Crab);
        private static void Obstacles(LevelData l, int[,] cells, ObstacleKind kind)
        {
            for (var i = 0; i < cells.GetLength(0); i++) l.obstacles.Add(new ObstacleData
                { cell = new Vector2Int(cells[i,0], cells[i,1]), kind = kind });
        }
    }
}
