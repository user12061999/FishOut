using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FishOut.Editor
{
    // Drag the board's corners, rounding, road and cave onto the painted background; changes show live in Play mode.
    public sealed class BoardCalibrationWindow : EditorWindow
    {
        private const string AssetPath = "Assets/FishOut/Resources/Backdrop/BoardCalibration.asset";
        private const float PanelWidth = 300, HandleRadius = 9;
        private static readonly string[] PointNames = { "Góc trên trái", "Góc trên phải", "Góc dưới phải", "Góc dưới trái", "Hang 1 (sát đường)", "Hang 2 (miệng hang)", "Hang 3 (trong hang)", "Hang cuối (khuất sau vòm)" };
        private static string PointName(int i) => i < BoardCalibration.FixedPoints ? PointNames[i] : i == BoardCalibration.FixedPoints ? "Đường 0 (rẽ vào hang)" : "Đường " + (i - BoardCalibration.FixedPoints);
        private static readonly Color RoadColor = new Color(1, .55f, .1f);

        private BoardCalibration data;
        private LevelData level;
        private float zoom = 1;
        private Vector2 pan, panelScroll;
        private int selected = -1, dragging = -1;
        private bool showGrid = true, showRoad = true;
        private double applyAt = -1;

        [MenuItem("Fish Out/Căn bàn chơi trên nền vẽ")]
        public static void Open()
        {
            var window = GetWindow<BoardCalibrationWindow>("Căn bàn chơi");
            window.minSize = new Vector2(760, 620); window.Show();
        }

        private void OnEnable()
        {
            data = LoadOrCreate(); PaintedBackdrop.Reload();
            level = new LevelData { width = ShowcaseLevels.SpecWidth, height = ShowcaseLevels.SpecHeight, showcase = true };
            wantsMouseMove = true; Undo.undoRedoPerformed += OnUndo;
        }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; Flush(); }
        private void OnUndo() { Changed(); }

        private static BoardCalibration LoadOrCreate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BoardCalibration>(AssetPath);
            if (asset) return asset;
            asset = CreateInstance<BoardCalibration>();
            AssetDatabase.CreateAsset(asset, AssetPath); AssetDatabase.SaveAssets();
            return asset;
        }

        // Saving and rebuilding the level waits until the hand stops, so dragging stays smooth in Play mode.
        private void Changed()
        {
            PaintedBackdrop.Reload(); EditorUtility.SetDirty(data);
            applyAt = EditorApplication.timeSinceStartup + .35; Repaint();
        }
        private void Update()
        {
            if (applyAt < 0 || dragging >= 0 || EditorApplication.timeSinceStartup < applyAt) return;
            Flush();
        }
        private void Flush()
        {
            if (applyAt < 0 || !data) return;
            applyAt = -1; AssetDatabase.SaveAssetIfDirty(data); PaintedBackdrop.Reload();
            if (!EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<FishOutGame>();
            if (game && game.CurrentLevel > 0) game.StartLevel(game.CurrentLevel);
        }

        // ---- Image ↔ window ------------------------------------------------------------------------
        private Rect Canvas => new Rect(0, 0, position.width - PanelWidth, position.height);
        private float Scale => Mathf.Min(Canvas.width / PaintedBackdrop.ImageSize.x, Canvas.height / PaintedBackdrop.ImageSize.y) * .92f * zoom;
        private Vector2 Origin => Canvas.center - PaintedBackdrop.ImageSize * Scale * .5f + pan;
        private Vector2 ToScreen(Vector2 pixel) => Origin + pixel * Scale;
        private Vector2 ToPixel(Vector2 screen) => (screen - Origin) / Scale;
        private Vector3 FromGrid(Vector2 grid)
        {
            var uv = PaintedBackdrop.GridToImage(level, grid);
            return ToScreen(new Vector2(uv.x * PaintedBackdrop.ImageSize.x, uv.y * PaintedBackdrop.ImageSize.y));
        }

        private void OnGUI()
        {
            if (!data) data = LoadOrCreate();
            var texture = PaintedBackdrop.Texture;
            if (!texture) { EditorGUILayout.HelpBox("Không tìm thấy ảnh nền Resources/Backdrop/SeaBackdrop.", MessageType.Error); return; }
            var e = Event.current;
            HandleCanvasInput(e);
            if (e.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(Canvas, new Color(.13f, .13f, .14f));
                GUI.DrawTexture(new Rect(Origin, PaintedBackdrop.ImageSize * Scale), texture, ScaleMode.StretchToFill, false);
                DrawOverlay();
            }
            DrawPanel();
        }

        private void DrawOverlay()
        {
            var c = PaintedBackdrop.Cells(level);
            if (showGrid)
            {
                int columns = level.width - 2, rows = level.height - 2;
                Handles.color = new Color(1, 1, 1, .45f);
                for (int i = 0; i <= columns; i++) { float x = Mathf.Lerp(c.xMin, c.xMax, i / (float)columns); Handles.DrawAAPolyLine(1.5f, FromGrid(new Vector2(x, c.yMin)), FromGrid(new Vector2(x, c.yMax))); }
                for (int j = 0; j <= rows; j++) { float y = Mathf.Lerp(c.yMin, c.yMax, j / (float)rows); Handles.DrawAAPolyLine(1.5f, FromGrid(new Vector2(c.xMin, y)), FromGrid(new Vector2(c.xMax, y))); }
                // The straight edges, extended to the corner handles.
                Handles.color = new Color(1, .9f, .2f, .9f);
                Handles.DrawAAPolyLine(2, FromGrid(new Vector2(c.xMin, c.yMax)), FromGrid(new Vector2(c.xMax, c.yMax)), FromGrid(new Vector2(c.xMax, c.yMin)), FromGrid(new Vector2(c.xMin, c.yMin)), FromGrid(new Vector2(c.xMin, c.yMax)));
            }
            var outline = PaintedBackdrop.OutlineGrid(level);
            var cut = new Vector3[outline.Count + 1];
            for (int k = 0; k < outline.Count; k++) cut[k] = FromGrid(outline[k]);
            cut[outline.Count] = cut[0];
            Handles.color = new Color(1, .1f, .1f, 1); Handles.DrawAAPolyLine(4, cut);
            if (showRoad)
            {
                PaintedBackdrop.Roads(level, out var ring, out var gate, out _);
                Handles.color = new Color(.1f, .9f, 1, .95f);
                Handles.DrawAAPolyLine(3, Map(ring, false)); Handles.DrawAAPolyLine(3, Map(gate, false));
                // Swimming direction, so the order of hand-drawn points is clear.
                for (int k = 8; k + 1 < ring.Length; k += 16)
                {
                    Vector2 a = FromGrid(ring[k]), b = FromGrid(ring[k + 1]); var d = (b - a).normalized; var n = new Vector2(-d.y, d.x);
                    Handles.DrawAAPolyLine(3, a - d * 6 + n * 5, a + d * 4, a - d * 6 - n * 5);
                }
                if (data.customRoad && data.roadPoints.Length > 0)
                {
                    Handles.color = new Color(RoadColor.r, RoadColor.g, RoadColor.b, .6f);
                    var control = new Vector3[data.roadPoints.Length + 1];
                    for (int k = 0; k < data.roadPoints.Length; k++) control[k] = ToScreen(data.roadPoints[k]);
                    control[data.roadPoints.Length] = control[0];
                    Handles.DrawDottedLines(Pairs(control), 3);
                }
            }
            for (int i = 0; i < data.PointCount; i++)
            {
                if (!showRoad && i >= 4) continue;
                var p = ToScreen(data.GetPoint(i)); bool corner = i < 4, active = i == selected, road = i >= BoardCalibration.FixedPoints;
                var box = new Rect(p.x - HandleRadius * .7f, p.y - HandleRadius * .7f, HandleRadius * 1.4f, HandleRadius * 1.4f);
                EditorGUI.DrawRect(new Rect(box.x - 2, box.y - 2, box.width + 4, box.height + 4), Color.black);
                EditorGUI.DrawRect(box, active ? new Color(1, .85f, .1f) : corner ? Color.white : road ? RoadColor : new Color(.1f, .9f, 1));
                GUI.Label(new Rect(p.x + 10, p.y - 18, 180, 18), PointName(i), EditorStyles.whiteBoldLabel);
            }
        }
        private static Vector3[] Pairs(Vector3[] line)
        {
            var pairs = new Vector3[(line.Length - 1) * 2];
            for (int k = 0; k + 1 < line.Length; k++) { pairs[2 * k] = line[k]; pairs[2 * k + 1] = line[k + 1]; }
            return pairs;
        }
        private Vector3[] Map(Vector2[] line, bool closed)
        {
            var points = new List<Vector3>(); foreach (var p in line) points.Add(FromGrid(p));
            if (closed && points.Count > 0) points.Add(points[0]);
            return points.ToArray();
        }

        private void HandleCanvasInput(Event e)
        {
            bool inCanvas = Canvas.Contains(e.mousePosition);
            switch (e.type)
            {
                case EventType.MouseDown when inCanvas && e.button == 0:
                    selected = dragging = Pick(e.mousePosition);
                    if (dragging >= 0) Undo.RecordObject(data, "Căn bàn chơi");
                    GUIUtility.keyboardControl = 0; e.Use(); Repaint(); break;
                case EventType.MouseDrag when dragging >= 0 && e.button == 0:
                    var pixel = ToPixel(e.mousePosition);
                    data.SetPoint(dragging, new Vector2(Mathf.Round(pixel.x * 10) / 10, Mathf.Round(pixel.y * 10) / 10));
                    Changed(); e.Use(); break;
                case EventType.MouseUp when dragging >= 0:
                    dragging = -1; e.Use(); break;
                case EventType.MouseDrag when inCanvas && (e.button == 1 || e.button == 2):
                    pan += e.delta; e.Use(); Repaint(); break;
                case EventType.ScrollWheel when inCanvas:
                    var under = ToPixel(e.mousePosition);
                    zoom = Mathf.Clamp(zoom * (e.delta.y < 0 ? 1.15f : 1 / 1.15f), .5f, 12);
                    pan += e.mousePosition - ToScreen(under); e.Use(); Repaint(); break;
                case EventType.KeyDown when selected >= 0 && GUIUtility.keyboardControl == 0:
                    var step = e.keyCode == KeyCode.LeftArrow ? Vector2.left : e.keyCode == KeyCode.RightArrow ? Vector2.right
                             : e.keyCode == KeyCode.UpArrow ? Vector2.down : e.keyCode == KeyCode.DownArrow ? Vector2.up : Vector2.zero;
                    if (step == Vector2.zero) break;
                    Undo.RecordObject(data, "Nhích điểm");
                    data.SetPoint(selected, data.GetPoint(selected) + step * (e.shift ? 10 : e.alt ? .25f : 1));
                    Changed(); e.Use(); break;
            }
        }
        private int Pick(Vector2 mouse)
        {
            int best = -1; float bestDistance = HandleRadius * 1.6f;
            for (int i = 0; i < data.PointCount; i++)
            {
                if (!showRoad && i >= 4) continue;
                float d = Vector2.Distance(mouse, ToScreen(data.GetPoint(i)));
                if (d < bestDistance) { bestDistance = d; best = i; }
            }
            return best;
        }

        private void DrawPanel()
        {
            var area = new Rect(position.width - PanelWidth, 0, PanelWidth, position.height);
            EditorGUI.DrawRect(area, EditorGUIUtility.isProSkin ? new Color(.22f, .22f, .22f) : new Color(.76f, .76f, .76f));
            GUILayout.BeginArea(new Rect(area.x + 8, 8, area.width - 16, area.height - 16));
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            EditorGUILayout.LabelField("Căn bàn chơi trên nền vẽ", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "• Kéo 4 ô trắng tới chỗ hai mép thẳng của bàn vẽ kéo dài gặp nhau (khung vàng phải nằm đè lên mép bàn).\n" +
                "• Viền đỏ là chỗ cắt ô cờ: chỉnh bằng độ bo từng góc bên dưới.\n" +
                "• Ô xanh là cửa hang, đường xanh là lối cá bơi (mũi tên chỉ chiều bơi). Muốn uốn đường kỹ hơn thì bật \"Tự vẽ đường\" ở dưới.\n" +
                "• Lăn chuột: phóng to. Giữ chuột phải: kéo ảnh.\n" +
                "• Chọn một điểm rồi bấm phím mũi tên: nhích 1 px (Shift 10 px, Alt 0,25 px).\n" +
                "• Ctrl+Z để hoàn tác. Đang Play thì game tự cập nhật.", MessageType.None);

            if (selected >= data.PointCount) selected = -1;
            if (selected >= 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Đang chọn: " + PointName(selected), EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Vector2Field("Toạ độ (px)", data.GetPoint(selected));
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(data, "Sửa toạ độ"); data.SetPoint(selected, value); Changed(); }
            }

            var so = new SerializedObject(data); so.Update();
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("4 góc bàn (px)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(so.FindProperty("topLeft"), new GUIContent("Trên trái"));
            EditorGUILayout.PropertyField(so.FindProperty("topRight"), new GUIContent("Trên phải"));
            EditorGUILayout.PropertyField(so.FindProperty("bottomRight"), new GUIContent("Dưới phải"));
            EditorGUILayout.PropertyField(so.FindProperty("bottomLeft"), new GUIContent("Dưới trái"));
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Độ bo góc (ô)", EditorStyles.boldLabel);
            EditorGUILayout.Slider(so.FindProperty("radiusTopLeft"), 0, 3, "Trên trái");
            EditorGUILayout.Slider(so.FindProperty("radiusTopRight"), 0, 3, "Trên phải");
            EditorGUILayout.Slider(so.FindProperty("radiusBottomRight"), 0, 3, "Dưới phải");
            EditorGUILayout.Slider(so.FindProperty("radiusBottomLeft"), 0, 3, "Dưới trái");
            EditorGUILayout.Slider(so.FindProperty("edgeOffset"), -.5f, .5f, new GUIContent("Nới / thu viền cắt", "Dương: viền cắt lùi ra ngoài; âm: thu vào trong"));
            EditorGUILayout.PropertyField(so.FindProperty("checker"), new GUIContent("Hiện ô cờ", "Tắt: mặt bàn cát trơn của nền (lưới ẩn theo spec), vẫn giữ gờ và rãnh quanh bàn"));
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Đường cá bơi", EditorStyles.boldLabel);
            if (!data.customRoad)
            {
                EditorGUILayout.Slider(so.FindProperty("roadSide"), 0, 2, "Cách mép trái/phải (ô)");
                EditorGUILayout.Slider(so.FindProperty("roadBottom"), 0, 2, "Cách mép dưới (ô)");
                EditorGUILayout.Slider(so.FindProperty("roadTop"), 0, 2, "Cách mép trên (ô)");
            }
            else EditorGUILayout.Slider(so.FindProperty("roadRounding"), 0, 3, "Độ bo khúc cua (ô)");
            if (so.ApplyModifiedProperties()) Changed();
            bool custom = EditorGUILayout.ToggleLeft("Tự vẽ đường (kéo từng điểm cam)", data.customRoad);
            if (custom != data.customRoad)
            {
                Undo.RecordObject(data, "Tự vẽ đường");
                if (custom && (data.roadPoints == null || data.roadPoints.Length < 3)) data.roadPoints = PaintedBackdrop.RoadCornersInPixels(level);
                data.customRoad = custom; Changed();
            }
            if (data.customRoad)
            {
                int road = selected - BoardCalibration.FixedPoints;
                EditorGUILayout.HelpBox("Điểm cam nối vòng theo thứ tự số, mũi tên là chiều cá bơi. Đường 0 là chỗ rẽ lên hang. Chọn một điểm cam rồi thêm điểm để uốn kỹ hơn.", MessageType.None);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUI.enabled = road >= 0;
                    if (GUILayout.Button("Thêm điểm sau"))
                    {
                        Undo.RecordObject(data, "Thêm điểm đường");
                        var list = new List<Vector2>(data.roadPoints);
                        list.Insert(road + 1, (list[road] + list[(road + 1) % list.Count]) * .5f);
                        data.roadPoints = list.ToArray(); selected++; Changed();
                    }
                    GUI.enabled = road > 0 && data.roadPoints.Length > 3;
                    if (GUILayout.Button("Xoá điểm"))
                    {
                        Undo.RecordObject(data, "Xoá điểm đường");
                        var list = new List<Vector2>(data.roadPoints); list.RemoveAt(road);
                        data.roadPoints = list.ToArray(); selected = -1; Changed();
                    }
                    GUI.enabled = true;
                }
                if (GUILayout.Button("Vẽ lại theo đường tự động"))
                {
                    Undo.RecordObject(data, "Vẽ lại đường");
                    data.customRoad = false; PaintedBackdrop.Reload();
                    data.roadPoints = PaintedBackdrop.RoadCornersInPixels(level); data.customRoad = true; Changed();
                }
            }

            EditorGUILayout.Space(8);
            showGrid = EditorGUILayout.Toggle("Hiện lưới ô", showGrid);
            showRoad = EditorGUILayout.Toggle("Hiện đường + hang", showRoad);
            if (GUILayout.Button("Vừa cửa sổ")) { zoom = 1; pan = Vector2.zero; }
            if (GUILayout.Button("Về số mặc định"))
            {
                Undo.RecordObject(data, "Về mặc định");
                var fresh = CreateInstance<BoardCalibration>(); EditorUtility.CopySerialized(fresh, data); DestroyImmediate(fresh);
                Changed();
            }
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(EditorApplication.isPlaying ? "Đang Play: thả tay là màn chơi dựng lại theo số mới." : "Bấm Play để xem cùng cá. Số được lưu tự động vào " + AssetPath + ".", MessageType.Info);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
