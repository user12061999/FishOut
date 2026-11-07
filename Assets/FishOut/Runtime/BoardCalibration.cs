using UnityEngine;
namespace FishOut
{
    // Hand-tuned fit of the board onto the painted background (Fish Out > Căn bàn chơi trên nền vẽ).
    // Points are image pixels from the top-left corner of SeaBackdrop.png; lengths are in board cells.
    public sealed class BoardCalibration : ScriptableObject
    {
        public const string Resource = "Backdrop/BoardCalibration";
        [Tooltip("Where the painted board's straight edges meet (extended past the rounded corners), in pixels.")]
        public Vector2 topLeft = new Vector2(269.1f, 354.3f), topRight = new Vector2(880.1f, 422.7f),
                       bottomRight = new Vector2(842.3f, 1426.0f), bottomLeft = new Vector2(-8.5f, 1277.6f);
        [Tooltip("Rounding of each painted corner, in cells.")]
        [Range(0, 3)] public float radiusTopLeft = 1.725f, radiusTopRight = 1.175f, radiusBottomRight = 1.175f, radiusBottomLeft = 1.5f;
        [Tooltip("Draw the board's checker cells; off leaves the painted sand plain (spec: hidden grid), keeping the raised rim.")]
        public bool checker = true;
        [Tooltip("Nudges every fish, crab and rock inside its cell (cells; x right, y towards the top of the board).")]
        public Vector2 pieceOffset;
        [Tooltip("Size of the fish, crabs and rocks.")]
        [Range(.6f, 1.4f)] public float pieceScale = 1f;
        [Tooltip("Moves the whole cut edge out (+) or in (−), in cells.")]
        [Range(-.5f, .5f)] public float edgeOffset;
        [Tooltip("How far the fish road runs outside the board, in cells.")]
        [Range(0, 2)] public float roadSide = .3f, roadBottom = .45f, roadTop = .6f;
        [Tooltip("Path from the road up into the cave, in pixels (first point at the road, last inside the cave).")]
        public Vector2[] caveNeck = { new Vector2(625.8f, 284.2f), new Vector2(635.2f, 200.6f), new Vector2(639.9f, 142.1f) };
        [Tooltip("Where a fish ends, deep in the cave behind the arch (pixels). The second cave point is the cave mouth.")]
        public Vector2 caveHidden = new Vector2(643f, 40f);

        [Tooltip("Draw the fish road by hand instead of keeping it a fixed distance around the board.")]
        public bool customRoad;
        [Tooltip("Hand-drawn road loop in pixels, in swimming order. Point 0 is where the road turns up into the cave.")]
        public Vector2[] roadPoints = new Vector2[0];
        [Tooltip("Rounding of the hand-drawn road's bends, in cells.")]
        [Range(0, 3)] public float roadRounding = 1.1f;

        // Draggable points: 4 board corners, 3 cave points, the hidden end, then the hand-drawn road.
        public const int FixedPoints = 8;
        public int PointCount => FixedPoints + (customRoad ? roadPoints.Length : 0);
        public Vector2 GetPoint(int i)
        {
            switch (i) { case 0: return topLeft; case 1: return topRight; case 2: return bottomRight; case 3: return bottomLeft; }
            if (i == 7) return caveHidden;
            return i < FixedPoints ? caveNeck[i - 4] : roadPoints[i - FixedPoints];
        }
        public void SetPoint(int i, Vector2 p)
        {
            switch (i) { case 0: topLeft = p; return; case 1: topRight = p; return; case 2: bottomRight = p; return; case 3: bottomLeft = p; return; }
            if (i == 7) caveHidden = p; else if (i < FixedPoints) caveNeck[i - 4] = p; else roadPoints[i - FixedPoints] = p;
        }
    }
}
