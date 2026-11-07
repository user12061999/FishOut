using System.Collections.Generic;
using UnityEngine;
namespace FishOut
{
    // The client's painted seabed replaces the procedural 3D reef. The painting is drawn in perspective and its
    // board is a tilted quad, so the fish grid is mapped onto that quad with a projective transform
    // (grid → painting), and every board position goes through it. The painting lies flat under an
    // orthographic camera, which keeps it undistorted; the 3D pieces are scaled with the local cell size.
    public static class PaintedBackdrop
    {
        public static bool Enabled = true;
        private const string Resource = "Backdrop/SeaBackdrop";
        public static readonly Vector2 ImageSize = new Vector2(941, 1672);
        private static float ImageAspect => ImageSize.y / ImageSize.x;
        // Hand-tuned fit (corners, rounding, road, cave), editable in the Fish Out menu; defaults if the asset is missing.
        private static BoardCalibration calibration;
        public static BoardCalibration Calibration
        {
            get
            {
                if (calibration) return calibration;
                calibration = Resources.Load<BoardCalibration>(BoardCalibration.Resource);
                if (!calibration) calibration = ScriptableObject.CreateInstance<BoardCalibration>();
                return calibration;
            }
        }
        private static Vector2 UV(Vector2 pixel) => new Vector2(pixel.x / ImageSize.x, pixel.y / ImageSize.y);
        private static Vector2 BoardTL => UV(Calibration.topLeft);
        private static Vector2 BoardTR => UV(Calibration.topRight);
        private static Vector2 BoardBR => UV(Calibration.bottomRight);
        private static Vector2 BoardBL => UV(Calibration.bottomLeft);
        // Call after the calibration changes.
        public static void Reload()
        {
            forward.Clear(); inverse.Clear(); calibration = null;
            Release(topTexture);
            topTexture = null; topKey = null;
        }
        private const float PaintingHeight = -.2f;
        private const float Pitch = 50, ScaleExponent = .85f;
        private static Texture2D texture;

        public static Texture2D Texture => texture ? texture : texture = Resources.Load<Texture2D>(Resource);
        public static bool Available => Enabled && Texture;
        public static LevelData Current { get; private set; }
        public static bool Warps(LevelData level) => Enabled && level != null && level.showcase;

        // The playable cells in grid units, half a cell of margin around their centres.
        public static Rect Cells(LevelData level) => Rect.MinMaxRect(.52f, .52f, level.width - 1.52f, level.height - 1.52f);

        // ---- Projective map between the grid and the painting -------------------------------------------
        private static readonly Dictionary<Vector2Int, float[]> forward = new Dictionary<Vector2Int, float[]>();
        private static readonly Dictionary<Vector2Int, float[]> inverse = new Dictionary<Vector2Int, float[]>();
        private static float[] Forward(LevelData level)
        {
            var key = new Vector2Int(level.width, level.height);
            if (forward.TryGetValue(key, out var h)) return h;
            var c = Cells(level);
            h = Solve(new[] { new Vector2(c.xMin, c.yMax), new Vector2(c.xMax, c.yMax), new Vector2(c.xMax, c.yMin), new Vector2(c.xMin, c.yMin) },
                      new[] { BoardTL, BoardTR, BoardBR, BoardBL });
            forward[key] = h; inverse[key] = Invert(h); return h;
        }
        private static float[] Inverse(LevelData level) { Forward(level); return inverse[new Vector2Int(level.width, level.height)]; }
        private static Vector2 Apply(float[] h, Vector2 p)
        {
            float w = h[6] * p.x + h[7] * p.y + h[8];
            return new Vector2((h[0] * p.x + h[1] * p.y + h[2]) / w, (h[3] * p.x + h[4] * p.y + h[5]) / w);
        }
        public static Vector2 GridToImage(LevelData level, Vector2 p) => Apply(Forward(level), p);
        public static Vector2 ImageToGrid(LevelData level, Vector2 uv) => Apply(Inverse(level), uv);
        // Homography from four point pairs (8 × 8 linear system, h8 = 1).
        private static float[] Solve(Vector2[] from, Vector2[] to)
        {
            var a = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double x = from[i].x, y = from[i].y, u = to[i].x, v = to[i].y;
                double[] r0 = { x, y, 1, 0, 0, 0, -u * x, -u * y, u }, r1 = { 0, 0, 0, x, y, 1, -v * x, -v * y, v };
                for (int j = 0; j < 9; j++) { a[2 * i, j] = r0[j]; a[2 * i + 1, j] = r1[j]; }
            }
            for (int col = 0; col < 8; col++)
            {
                int pivot = col; for (int r = col + 1; r < 8; r++) if (System.Math.Abs(a[r, col]) > System.Math.Abs(a[pivot, col])) pivot = r;
                for (int j = 0; j < 9; j++) { var t = a[col, j]; a[col, j] = a[pivot, j]; a[pivot, j] = t; }
                for (int r = 0; r < 8; r++)
                {
                    if (r == col) continue; double f = a[r, col] / a[col, col];
                    for (int j = col; j < 9; j++) a[r, j] -= f * a[col, j];
                }
            }
            var h = new float[9]; for (int i = 0; i < 8; i++) h[i] = (float)(a[i, 8] / a[i, i]); h[8] = 1; return h;
        }
        private static float[] Invert(float[] m)
        {
            float a = m[0], b = m[1], c = m[2], d = m[3], e = m[4], f = m[5], g = m[6], h = m[7], i = m[8];
            float A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g;
            float det = a * A + b * B + c * C;
            return new[] { A / det, -(b * i - c * h) / det, (b * f - c * e) / det, B / det, (a * i - c * g) / det, -(a * f - c * d) / det, C / det, -(a * h - b * g) / det, (a * e - b * d) / det };
        }

        // ---- Painting ↔ world (the painting is a flat quad under an orthographic camera) --------------------
        public struct Layout { public float pitch, width, depth; public Vector3 center; }
        public static Layout Compute(LevelData level)
        {
            // Board cells keep roughly their screen size; the painting's own scale sets the world units.
            var c = Cells(level);
            float width = c.width / (Vector2.Distance(BoardBL, BoardBR) * .5f + Vector2.Distance(BoardTL, BoardTR) * .5f);
            float depth = width * ImageAspect / Mathf.Sin(Pitch * Mathf.Deg2Rad);
            return new Layout { pitch = Pitch, width = width, depth = depth, center = Vector3.zero };
        }
        private static Vector3 ImageToWorld(LevelData level, Vector2 uv, float y)
        {
            var l = Compute(level);
            return new Vector3((uv.x - .5f) * l.width, y, (.5f - uv.y) * l.depth);
        }
        private static Vector2 WorldToImage(LevelData level, Vector3 p)
        {
            var l = Compute(level);
            return new Vector2(p.x / l.width + .5f, .5f - p.z / l.depth);
        }
        public static Vector3 GridToWorld(LevelData level, Vector2 p, float y) => ImageToWorld(level, GridToImage(level, p), y);

        // Pieces are scaled with the on-screen size of the cell they stand on (1 at the board centre).
        public static float ScaleAt(Vector3 world)
        {
            if (Current == null) return 1;
            var g = ImageToGrid(Current, WorldToImage(Current, world));
            float here = CellSize(Current, g), middle = CellSize(Current, Cells(Current).center);
            return Mathf.Pow(Mathf.Max(.2f, here / middle), ScaleExponent);
        }
        private static float CellSize(LevelData level, Vector2 g)
        {
            var o = GridToImage(level, g);
            return Mathf.Sqrt(Mathf.Abs(Cross(GridToImage(level, g + Vector2.right) - o, GridToImage(level, g + Vector2.up) - o)));
        }
        // World length of one cell step around a world point (the shorter of the two grid axes); 1 off the painting.
        public static float CellWorldSize(Vector3 world)
        {
            if (Current == null) return 1;
            var g = ImageToGrid(Current, WorldToImage(Current, world)); var o = GridToWorld(Current, g, 0);
            return Mathf.Min(Vector3.Distance(GridToWorld(Current, g + Vector2.right, 0), o), Vector3.Distance(GridToWorld(Current, g + Vector2.up, 0), o));
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        // Facing of a grid direction on the painted board, as a yaw angle in degrees.
        public static float Yaw(LevelData level, Vector2 cell, Vector2 direction)
        {
            var d = GridToWorld(level, cell + direction * .5f, 0) - GridToWorld(level, cell - direction * .5f, 0);
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        // ---- Roads -------------------------------------------------------------------------------------
        // A ring on the painted road around the board, and the neck up into the painted cave (grid units).
        public static void Roads(LevelData level, out Vector2[] ring, out Vector2[] gate, out Vector2Int gateCell)
        {
            var cal = Calibration;
            var cave = new List<Vector2>(); foreach (var pixel in cal.caveNeck) cave.Add(ImageToGrid(level, UV(pixel)));
            var loop = RoadCorners(level);
            // The loop starts and ends at the turn into the cave, which stays a sharp joint for the neck to leave from.
            var open = new List<Vector2>(loop) { loop[0] };
            ring = ScrewConveyorGeometry.Rounded(open.ToArray(), false, cal.customRoad ? cal.roadRounding : 1.1f);
            // On through the mouth to a point hidden behind the painted arch, so a fish swims out of sight.
            var hidden = ImageToGrid(level, UV(cal.caveHidden));
            var neck = new List<Vector2> { loop[0] }; neck.AddRange(cave); neck.Add(hidden);
            gate = ScrewConveyorGeometry.Resample(neck, .12f);
            gateCell = Vector2Int.RoundToInt(hidden);
        }
        // The cave mouth (second cave point) in world space, where a fish starts to sink into the dark.
        public static Vector3 CaveMouth(LevelData level, float y) => GridToWorld(level, ImageToGrid(level, UV(Calibration.caveNeck[1])), y);
        // Where a fish ends, hidden behind the painted arch.
        public static Vector3 CaveEnd(LevelData level, float y) => GridToWorld(level, ImageToGrid(level, UV(Calibration.caveHidden)), y);

        // Corners of the road loop in grid units, from the cave turn round the board: hand-drawn or kept a set distance off the board.
        public static Vector2[] RoadCorners(LevelData level)
        {
            var cal = Calibration;
            if (cal.customRoad && cal.roadPoints != null && cal.roadPoints.Length >= 3)
            {
                var drawn = new Vector2[cal.roadPoints.Length];
                for (int i = 0; i < drawn.Length; i++) drawn[i] = ImageToGrid(level, UV(cal.roadPoints[i]));
                return drawn;
            }
            var c = Cells(level);
            float left = c.xMin - cal.roadSide, right = c.xMax + cal.roadSide, bottom = c.yMin - cal.roadBottom, top = c.yMax + cal.roadTop;
            float neckX = Mathf.Clamp(ImageToGrid(level, UV(cal.caveNeck[0])).x, left + 1.3f, right - 1.3f);
            return new[] { new Vector2(neckX, top), new Vector2(left, top), new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top) };
        }
        // The automatic road's corners as image pixels, the starting point for drawing it by hand.
        public static Vector2[] RoadCornersInPixels(LevelData level)
        {
            var corners = RoadCorners(level); var pixels = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++) { var uv = GridToImage(level, corners[i]); pixels[i] = new Vector2(uv.x * ImageSize.x, uv.y * ImageSize.y); }
            return pixels;
        }

        // ---- Scene -------------------------------------------------------------------------------------
        public static GameObject Build(Transform parent, LevelData level, Material template, List<Mesh> meshes, List<Material> materials, List<Texture2D> textures)
        {
            Current = level;
            var layout = Compute(level);
            var quad = new GameObject("Painted seabed", typeof(MeshFilter), typeof(MeshRenderer));
            quad.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Painted seabed" };
            mesh.SetVertices(new List<Vector3> { ImageToWorld(level, new Vector2(0, 1), -.2f), ImageToWorld(level, new Vector2(0, 0), -.2f), ImageToWorld(level, new Vector2(1, 0), -.2f), ImageToWorld(level, new Vector2(1, 1), -.2f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh); quad.GetComponent<MeshFilter>().sharedMesh = mesh;
            var painting = new Material(template) { name = "Painted seabed", color = Color.white, mainTexture = Texture, renderQueue = 2050 };
            materials.Add(painting); Configure(quad, painting);
            // The painted checker is not a regular 6 × 8 grid, so the board's own cells are laid over it.
            var board = new GameObject("Board checker", typeof(MeshFilter), typeof(MeshRenderer));
            board.transform.SetParent(parent, false);
            var topShader = Resources.Load<Shader>(TopShader);
            CaveFront(parent, level, topShader, meshes, materials);
            if (topShader && topShader.isSupported)
            {
                // Sand-grained checker with a raised rim and its shadow, all baked into one alpha texture.
                var topMesh = BoardTopMesh(level); meshes.Add(topMesh); board.GetComponent<MeshFilter>().sharedMesh = topMesh;
                var top = new Material(topShader) { name = "Board top", mainTexture = BoardTopTexture(level), renderQueue = 2990 };
                materials.Add(top); Configure(board, top);
                return quad;
            }
            var boardMesh = Checkerboard(level); meshes.Add(boardMesh); board.GetComponent<MeshFilter>().sharedMesh = boardMesh;
            var checker = Checker(level.width - 2, level.height - 2); textures.Add(checker);
            var cellsMaterial = new Material(template) { name = "Board checker", color = Color.white, mainTexture = checker, renderQueue = 2040 };
            materials.Add(cellsMaterial); Configure(board, cellsMaterial);
            return quad;
        }
        private static void Configure(GameObject go, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        // The painted board's cut edge: the cell rect with each corner rounded as calibrated (grid units, convex).
        public static List<Vector2> OutlineGrid(LevelData level)
        {
            var cal = Calibration; var c = Cells(level); float o = cal.edgeOffset;
            var r = Rect.MinMaxRect(c.xMin - o, c.yMin - o, c.xMax + o, c.yMax + o);
            float limit = Mathf.Min(r.width, r.height) * .5f;
            var points = new List<Vector2>();
            void Corner(float radius, Vector2 corner, Vector2 inward, float from)
            {
                radius = Mathf.Clamp(radius, 0, limit);
                if (radius < .01f) { points.Add(corner); return; }
                var centre = corner + inward * radius;
                for (int k = 0; k <= 10; k++) { float a = (from + 90 * k / 10f) * Mathf.Deg2Rad; points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius); }
            }
            Corner(cal.radiusBottomRight, new Vector2(r.xMax, r.yMin), new Vector2(-1, 1), -90);
            Corner(cal.radiusTopRight, new Vector2(r.xMax, r.yMax), new Vector2(-1, -1), 0);
            Corner(cal.radiusTopLeft, new Vector2(r.xMin, r.yMax), new Vector2(1, -1), 90);
            Corner(cal.radiusBottomLeft, new Vector2(r.xMin, r.yMin), new Vector2(1, 1), 180);
            return points;
        }
        // True when a grid point lies inside the painted board's cut edge by at least margin cells
        // (a fish on a rounded corner cell would otherwise stand on the sand).
        public static bool InsideCut(LevelData level, Vector2 p, float margin)
        {
            var outline = OutlineGrid(level); float area = 0;
            for (int k = 0; k < outline.Count; k++) { var a = outline[k]; var b = outline[(k + 1) % outline.Count]; area += a.x * b.y - b.x * a.y; }
            float winding = Mathf.Sign(area);
            for (int k = 0; k < outline.Count; k++)
            {
                Vector2 a = outline[k], b = outline[(k + 1) % outline.Count]; var edge = b - a; float length = edge.magnitude;
                if (length < 1e-5f) continue;
                if (winding * (edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / length < margin) return false;
            }
            return true;
        }
        // Sutherland-Hodgman: keeps the part of a polygon inside the convex outline.
        private static List<Vector2> ClipToOutline(List<Vector2> polygon, Vector2[] outline, float winding)
        {
            for (int e = 0; e < outline.Length && polygon.Count > 0; e++)
            {
                Vector2 a = outline[e], b = outline[(e + 1) % outline.Length];
                float Side(Vector2 q) => winding * ((b.x - a.x) * (q.y - a.y) - (b.y - a.y) * (q.x - a.x));
                var kept = new List<Vector2>(polygon.Count + 2);
                for (int k = 0; k < polygon.Count; k++)
                {
                    Vector2 p = polygon[k], n = polygon[(k + 1) % polygon.Count]; float sp = Side(p), sn = Side(n);
                    if (sp >= 0) kept.Add(p);
                    if ((sp >= 0) != (sn >= 0)) kept.Add(Vector2.Lerp(p, n, sp / (sp - sn)));
                }
                polygon = kept;
            }
            return polygon;
        }
        // A fine grid through the projective map, so the checker follows the painted board's perspective,
        // cut exactly by the painted board's outline.
        private static Mesh Checkerboard(LevelData level)
        {
            var c = Cells(level); const int columns = 64, rows = 80;
            var outline = OutlineGrid(level).ToArray(); float area = 0;
            for (int k = 0; k < outline.Length; k++) { var a = outline[k]; var b = outline[(k + 1) % outline.Length]; area += a.x * b.y - b.x * a.y; }
            float winding = Mathf.Sign(area);
            // The checker squares are the grid cells, extended past the cell rect up to the outline.
            float x0 = c.xMin - 1, x1 = c.xMax + 1, y0 = c.yMin - 1, y1 = c.yMax + 1;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector2 Grid(int i, int j) => new Vector2(Mathf.Lerp(x0, x1, i / (float)columns), Mathf.Lerp(y0, y1, j / (float)rows));
            void Emit(List<Vector2> polygon)
            {
                if (polygon.Count < 3) return; int start = vertices.Count;
                foreach (var g in polygon) { vertices.Add(GridToWorld(level, g, -.03f)); uv.Add(new Vector2((g.x - c.xMin) / c.width, (g.y - c.yMin) / c.height)); }
                for (int k = 1; k < polygon.Count - 1; k++) { triangles.Add(start); triangles.Add(start + k); triangles.Add(start + k + 1); }
            }
            for (int j = 0; j < rows; j++) for (int i = 0; i < columns; i++)
            {
                // Wound to face the camera above the board (same order as the painting).
                Vector2 bl = Grid(i, j), br = Grid(i + 1, j), tl = Grid(i, j + 1), tr = Grid(i + 1, j + 1);
                Emit(ClipToOutline(new List<Vector2> { bl, tl, br }, outline, winding));
                Emit(ClipToOutline(new List<Vector2> { br, tl, tr }, outline, winding));
            }
            var mesh = new Mesh { name = "Board checker", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        // ---- Cave front: the painted arch drawn again over the fish --------------------------------------
        // CaveFront.png is the top strip of the painting (full width, from the top edge) with alpha only on the arch,
        // the rocks and corals around it, and a darkening ramp inside the tunnel. Drawn after everything, it hides
        // a fish behind the arch and lets it fade into the dark as it swims deeper.
        private const string CaveFrontResource = "Backdrop/CaveFront";
        private static void CaveFront(Transform parent, LevelData level, Shader shader, List<Mesh> meshes, List<Material> materials)
        {
            var texture = Resources.Load<Texture2D>(CaveFrontResource);
            if (!texture || !shader || !shader.isSupported) return;
            float bottom = texture.height / ImageSize.y * (ImageSize.x / texture.width);
            var go = new GameObject("Cave front", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Cave front" };
            mesh.SetVertices(new List<Vector3> { ImageToWorld(level, new Vector2(0, bottom), -.2f), ImageToWorld(level, new Vector2(0, 0), -.2f), ImageToWorld(level, new Vector2(1, 0), -.2f), ImageToWorld(level, new Vector2(1, bottom), -.2f) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            meshes.Add(mesh); go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(shader) { name = "Cave front", mainTexture = texture, renderQueue = 3100 };
            material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            materials.Add(material); Configure(go, material);
        }

        // ---- Board top: sand checker, raised rim, cast shadow -----------------------------------------
        private const string TopShader = "Backdrop/BoardTop";
        private const int TopTexels = 64;                      // texels per cell
        private const float TopMargin = 1f;                    // cells of shadow room around the cell rect
        // Matched to the painted board's own edge: a narrow dark crease on the road at the board's foot, a soft
        // shade around it, and a rounded lighter rim on the board side (all in cells).
        private const float Crease = .07f, ShadowReach = .24f, ShadowDrop = .05f, Lip = .02f, RimAt = .07f, RimWidth = .05f;
        private static Texture2D topTexture; private static string topKey;
        private static void Release(Object asset)
        {
            if (!asset) return;
            if (Application.isPlaying) Object.Destroy(asset); else Object.DestroyImmediate(asset);
        }
        private static Rect TopRect(LevelData level)
        {
            var c = Cells(level);
            return Rect.MinMaxRect(c.xMin - TopMargin, c.yMin - TopMargin, c.xMax + TopMargin, c.yMax + TopMargin);
        }
        // A plain warped grid over the board and its shadow; the texture's alpha cuts the rounded outline.
        private static Mesh BoardTopMesh(LevelData level)
        {
            var e = TopRect(level); const int columns = 40, rows = 50;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int j = 0; j <= rows; j++) for (int i = 0; i <= columns; i++)
            {
                var t = new Vector2(i / (float)columns, j / (float)rows);
                vertices.Add(GridToWorld(level, new Vector2(Mathf.Lerp(e.xMin, e.xMax, t.x), Mathf.Lerp(e.yMin, e.yMax, t.y)), -.03f)); uv.Add(t);
            }
            for (int j = 0; j < rows; j++) for (int i = 0; i < columns; i++)
            {
                int a = j * (columns + 1) + i, b = a + 1, d = a + columns + 1, f = d + 1;
                triangles.AddRange(new[] { a, d, b, b, d, f });
            }
            var mesh = new Mesh { name = "Board top", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        // Signed distance to the convex outline (negative inside) and the outward normal of the nearest edge.
        private static float OutlineDistance(Vector2[] a, Vector2[] normal, Vector2 p, out Vector2 nearest)
        {
            float best = float.NegativeInfinity; nearest = Vector2.up;
            for (int k = 0; k < a.Length; k++)
            {
                float d = normal[k].x * (p.x - a[k].x) + normal[k].y * (p.y - a[k].y);
                if (d > best) { best = d; nearest = normal[k]; }
            }
            return best;
        }
        // GLSL-style smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).
        private static float Smooth(float edge0, float edge1, float x) { float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0)); return t * t * (3 - 2 * t); }
        private static float Hash(int x, int y) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / 4294967295f; } }
        private static float Noise(Vector2 p)
        {
            int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y); float fx = p.x - x, fy = p.y - y;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(x, y), Hash(x + 1, y), fx), Mathf.Lerp(Hash(x, y + 1), Hash(x + 1, y + 1), fx), fy);
        }
        // Baked once per calibration: sand-grained checker, a lit top-left / shaded bottom-right bevel, a thin lip,
        // and a soft shadow on the painted sand that falls towards the camera, so the board seems to rise from the sand.
        private const string BoardMaskResource = "Backdrop/BoardMask";
        private static Texture2D BoardTopTexture(LevelData level)
        {
            var cal = Calibration;
            // When the painting ships a mask of its board surface, the checker fills exactly that surface and the
            // painting's own rim and crease are left to show; otherwise the rim and shadow are drawn here.
            var mask = Resources.Load<Texture2D>(BoardMaskResource);
            bool masked = mask && mask.isReadable;
            string key = level.width + "x" + level.height + "|" + cal.topLeft + cal.topRight + cal.bottomRight + cal.bottomLeft + "|" + cal.radiusTopLeft + "," + cal.radiusTopRight + "," + cal.radiusBottomRight + "," + cal.radiusBottomLeft + "," + cal.edgeOffset + "," + cal.checker + "," + (masked ? mask.GetInstanceID() + ":" + mask.updateCount : "none");
            if (topTexture && topKey == key) return topTexture;
            Release(topTexture);
            var outline = OutlineGrid(level).ToArray(); int n = outline.Length; float area = 0;
            for (int k = 0; k < n; k++) { var p0 = outline[k]; var p1 = outline[(k + 1) % n]; area += p0.x * p1.y - p1.x * p0.y; }
            var normals = new Vector2[n];
            for (int k = 0; k < n; k++)
            {
                var edge = outline[(k + 1) % n] - outline[k];
                var outward = area > 0 ? new Vector2(edge.y, -edge.x) : new Vector2(-edge.y, edge.x);
                normals[k] = outward.sqrMagnitude > 1e-10f ? outward.normalized : Vector2.up;
            }
            var c = Cells(level); var e = TopRect(level);
            float cellW = c.width / (level.width - 2), cellH = c.height / (level.height - 2);
            int w = Mathf.CeilToInt(e.width / cellW * TopTexels), h = Mathf.CeilToInt(e.height / cellH * TopTexels);
            float texel = e.width / w;
            // Tones measured on the painted checker (light 254,227,160 / dark 252,212,132).
            var light = new Color(254 / 255f, 227 / 255f, 160 / 255f); var dark = new Color(252 / 255f, 212 / 255f, 132 / 255f);
            var shadowColor = new Color(.94f, .59f, .20f);   // over the road sand this gives the painted crease tone
            var lightDirection = new Vector2(-.35f, 1).normalized;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                var g = new Vector2(e.xMin + (x + .5f) * e.width / w, e.yMin + (y + .5f) * e.height / h);
                if (masked)
                {
                    if (!cal.checker) { pixels[y * w + x] = default; continue; }
                    var uvImage = GridToImage(level, g);
                    float inMask = uvImage.x < 0 || uvImage.x > 1 || uvImage.y < 0 || uvImage.y > 1 ? 0 : mask.GetPixelBilinear(uvImage.x, 1 - uvImage.y).r;
                    if (inMask <= .002f) { pixels[y * w + x] = default; continue; }
                    int mx = Mathf.FloorToInt((g.x - c.xMin) / cellW), my = Mathf.FloorToInt((g.y - c.yMin) / cellH);
                    var cell = ((mx + my) & 1) == 0 ? light : dark;
                    float grainMasked = .55f * Noise(g * 9f) + .30f * Noise(g * 23f) + .15f * Noise(g * 57f);
                    cell *= 1 + (grainMasked - .5f) * .045f; cell.a = inMask;
                    pixels[y * w + x] = cell; continue;
                }
                float d = OutlineDistance(outline, normals, g, out var normal);
                if (d > ShadowReach + ShadowDrop + .02f) { pixels[y * w + x] = default; continue; }
                // Shadow on the sand: an even contact shade plus a drop towards the bottom of the screen.
                var outside = new Color(shadowColor.r, shadowColor.g, shadowColor.b, 0);
                if (d > -texel)
                {
                    float drop = OutlineDistance(outline, normals, g + new Vector2(0, ShadowDrop), out _);
                    outside.a = Mathf.Max(.5f * (1 - Smooth(0, Crease, d)), .14f * (1 - Smooth(0, ShadowReach, drop)));
                }
                Color top = default; float cover = Mathf.Clamp01(-d / texel + .5f);
                if (cover > 0 && !cal.checker)
                {
                    // Plain board: only the raised rim is drawn over the painted sand.
                    float inside = -d;
                    float rim = Mathf.Exp(-Mathf.Pow((inside - RimAt) / RimWidth, 2)) * (.6f + .4f * Mathf.Max(0, Vector2.Dot(normal, lightDirection)));
                    float lip = inside < Lip ? 1 - inside / Lip : 0;
                    var rimColour = lip > rim ? new Color(.62f, .40f, .14f, .35f * lip) : new Color(1, .98f, .9f, .32f * rim);
                    pixels[y * w + x] = Color.Lerp(outside, rimColour, cover);
                    continue;
                }
                if (cover > 0)
                {
                    int cx = Mathf.FloorToInt((g.x - c.xMin) / cellW), cy = Mathf.FloorToInt((g.y - c.yMin) / cellH);
                    top = ((cx + cy) & 1) == 0 ? light : dark;
                    // Fine sand grain and a few darker specks, like the painted sand around the board.
                    float grain = .55f * Noise(g * 9f) + .30f * Noise(g * 23f) + .15f * Noise(g * 57f);
                    float tone = 1 + (grain - .5f) * .045f;
                    if (Hash(Mathf.FloorToInt(g.x * 40), Mathf.FloorToInt(g.y * 40)) > .996f) tone *= .965f;
                    top *= tone;
                    float inside = -d;
                    // Rounded rim: a light band just inside the edge, a touch stronger where it faces the light.
                    float rim = Mathf.Exp(-Mathf.Pow((inside - RimAt) / RimWidth, 2));
                    top *= 1 + rim * (.09f + .05f * Vector2.Dot(normal, lightDirection));
                    if (inside < Lip) top *= Mathf.Lerp(.90f, 1, inside / Lip);
                    top.a = 1;
                }
                var colour = Color.Lerp(outside, top, cover);
                pixels[y * w + x] = colour;
            }
            topTexture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Board top", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            topTexture.SetPixels32(pixels); topTexture.Apply(false, true); topKey = key;
            return topTexture;
        }
        // Two sand tones sampled from the painting's checker.
        private static Texture2D Checker(int columns, int rows)
        {
            const int texel = 8;
            var light = new Color32(254, 229, 168, 255); var dark = new Color32(250, 210, 140, 255);
            var tex = new Texture2D(columns * texel, rows * texel, TextureFormat.RGBA32, false) { name = "Board checker", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[tex.width * tex.height];
            for (int y = 0; y < tex.height; y++) for (int x = 0; x < tex.width; x++) pixels[y * tex.width + x] = ((x / texel + y / texel) % 2 == 0) ? light : dark;
            tex.SetPixels32(pixels); tex.Apply(); return tex;
        }
        // Orthographic camera along the chosen tilt, sized so the painting fills the screen.
        public static void Frame(Camera camera, LevelData level, float aspect)
        {
            var layout = Compute(level);
            var rotation = Quaternion.Euler(layout.pitch, 0, 0);
            float screenDepth = layout.depth * Mathf.Sin(layout.pitch * Mathf.Deg2Rad);
            camera.rect = new Rect(0, 0, 1, 1);
            // Slightly inside the painting on both axes, so no clear colour shows at any edge.
            camera.orthographicSize = Mathf.Min(screenDepth * .5f, layout.width / (2 * aspect)) * .99f;
            camera.transform.rotation = rotation;
            // Aim at the painting's own plane (it lies below the board), otherwise the tilt shifts it down and
            // leaves a strip of background colour along the top of the screen.
            camera.transform.position = layout.center + Vector3.up * PaintingHeight - rotation * Vector3.forward * 40;
        }
    }
}
