using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// TerrainHoleCutter v6
///
/// New approach: boundary triangles are subdivided into a barycentric grid.
/// Each sub-triangle is classified solid/hole. Solid sub-tris are kept,
/// naturally forming a contour that follows the hole texture boundary.
/// Adjacent "up + down" sub-triangle pairs are merged into quads for
/// clean topology.
///
/// GPU preview is unchanged — HoleCutPreview.shader overlay.
/// </summary>
public class TerrainHoleCutter : EditorWindow
{
    List<GameObject> targets = new List<GameObject>();
    Texture2D holeTexture;
    float threshold = 0.5f;
    bool createBackup = true;
    bool useUV0 = true;
    Vector2 holeTiling = Vector2.one;
    Vector2 holeOffset = Vector2.zero;

    float holePadding = 0f;
    int maxSubdivLevel = 5;

    bool showPreview = false;
    Material previewMat;

    Vector2 scrollTargets, scrollLog;
    string logText = "";

    Texture2D cachedReadable, cachedSource;

    [MenuItem("Tools/CleanRender/Terrain Hole Cutter")]
    static void Open()
    {
        var win = GetWindow<TerrainHoleCutter>("Terrain Hole Cutter");
        win.minSize = new Vector2(440, 560);
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        foreach (var go in Selection.gameObjects)
            if (go.GetComponent<MeshFilter>()?.sharedMesh != null) targets.Add(go);
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        if (previewMat != null) DestroyImmediate(previewMat);
        if (cachedReadable != null && cachedReadable != cachedSource)
            DestroyImmediate(cachedReadable);
    }

    Material GetPreviewMat()
    {
        if (previewMat != null) return previewMat;
        var sh = Shader.Find("Hidden/HoleCutPreview");
        if (sh == null) return null;
        previewMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
        return previewMat;
    }

    void SyncPreview()
    {
        var m = GetPreviewMat();
        if (m == null || holeTexture == null) return;
        m.SetTexture("_HoleTex", holeTexture);
        m.SetFloat("_Threshold", threshold);
        m.SetFloat("_Padding", holePadding);
        m.SetFloat("_UseUV1", useUV0 ? 0f : 1f);
        m.SetVector("_HoleTex_ST", new Vector4(holeTiling.x, holeTiling.y, holeOffset.x, holeOffset.y));
    }

    void OnSceneGUI(SceneView sv)
    {
        if (!showPreview || holeTexture == null) return;
        var m = GetPreviewMat();
        if (m == null) return;
        foreach (var go in targets)
        {
            if (go == null) continue;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                Graphics.DrawMesh(mf.sharedMesh, go.transform.localToWorldMatrix, m, 0, sv.camera, s);
        }
    }

    void OnGUI()
    {
        EditorGUILayout.Space(6);
        DrawHeader("TERRAIN HOLE CUTTER", new Color(0.95f, 0.55f, 0.45f));
        EditorGUILayout.Space(4);

        bool changed = false;

        EditorGUILayout.LabelField("Hole Texture", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        holeTexture = (Texture2D)EditorGUILayout.ObjectField("Hole Map", holeTexture, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck()) changed = true;
        if (holeTexture != null)
            EditorGUILayout.LabelField($"{holeTexture.width}×{holeTexture.height}", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.BeginChangeCheck();
        holeTiling = EditorGUILayout.Vector2Field("Tiling", holeTiling);
        holeOffset = EditorGUILayout.Vector2Field("Offset", holeOffset);
        useUV0 = EditorGUILayout.Toggle("Use UV0", useUV0);
        if (EditorGUI.EndChangeCheck()) changed = true;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Cut Control", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUI.BeginChangeCheck();
        threshold = EditorGUILayout.Slider("Threshold", threshold, 0f, 1f);
        if (EditorGUI.EndChangeCheck()) changed = true;
        EditorGUILayout.LabelField("Black (0) = hole  ·  White (1) = solid", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.BeginChangeCheck();
        holePadding = EditorGUILayout.Slider("Hole Padding", holePadding, 0f, 8f);
        maxSubdivLevel = EditorGUILayout.IntSlider(
            new GUIContent("Max Subdivision",
                "How finely boundary triangles are subdivided.\n" +
                "Higher = smoother contour, more tris.\n" +
                "3 = fast, 5 = good, 8 = very precise"),
            maxSubdivLevel, 2, 8);
        if (EditorGUI.EndChangeCheck()) changed = true;
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        createBackup = EditorGUILayout.Toggle("Create Backup", createBackup);
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(4);

        EditorGUILayout.LabelField($"Targets ({targets.Count})", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        scrollTargets = EditorGUILayout.BeginScrollView(scrollTargets, GUILayout.Height(Mathf.Min(targets.Count * 22 + 30, 120)));
        for (int i = 0; i < targets.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            targets[i] = (GameObject)EditorGUILayout.ObjectField(targets[i], typeof(GameObject), true);
            if (GUILayout.Button("×", GUILayout.Width(20))) { targets.RemoveAt(i); i--; }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Selected"))
            foreach (var go in Selection.gameObjects)
                if (go.GetComponent<MeshFilter>() != null && !targets.Contains(go)) targets.Add(go);
        if (GUILayout.Button("Clear")) targets.Clear();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(6);

        bool canWork = holeTexture != null && targets.Count > 0;
        EditorGUILayout.BeginHorizontal();
        GUI.enabled = canWork;
        EditorGUI.BeginChangeCheck();
        bool np = GUILayout.Toggle(showPreview, showPreview ? "■ Preview ON" : "Preview", "Button", GUILayout.Height(30));
        if (EditorGUI.EndChangeCheck()) { showPreview = np; if (showPreview) SyncPreview(); SceneView.RepaintAll(); }
        Color ob = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.4f, 0.3f);
        if (GUILayout.Button("Cut All", GUILayout.Height(30))) CutAll();
        GUI.backgroundColor = ob;
        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (holeTexture == null) EditorGUILayout.HelpBox("Assign a Hole Map texture.", MessageType.Info);

        if (!string.IsNullOrEmpty(logText))
        {
            EditorGUILayout.Space(4);
            scrollLog = EditorGUILayout.BeginScrollView(scrollLog, GUILayout.Height(80));
            EditorGUILayout.HelpBox(logText, MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        if (changed && showPreview) { SyncPreview(); SceneView.RepaintAll(); }
    }

    float SamplePad(Texture2D tex, Vector2 uv, Vector4 st)
    {
        float val = SampleBL(tex, uv, st);
        if (holePadding <= 0f) return val;
        float pu = holePadding / (tex.width * Mathf.Max(Mathf.Abs(st.x), 0.001f));
        float pv = holePadding / (tex.height * Mathf.Max(Mathf.Abs(st.y), 0.001f));
        float v1 = SampleBL(tex, uv + new Vector2(pu, 0), st);
        float v2 = SampleBL(tex, uv - new Vector2(pu, 0), st);
        float v3 = SampleBL(tex, uv + new Vector2(0, pv), st);
        float v4 = SampleBL(tex, uv - new Vector2(0, pv), st);
        return Mathf.Min(val, Mathf.Min(Mathf.Min(v1, v2), Mathf.Min(v3, v4)));
    }

    bool IsSolid(Texture2D tex, Vector2 uv, Vector4 st)
    {
        return SamplePad(tex, uv, st) >= threshold;
    }

    static float SampleBL(Texture2D tex, Vector2 uv, Vector4 st)
    {
        float u = uv.x * st.x + st.z;
        float v = uv.y * st.y + st.w;
        u -= Mathf.Floor(u);
        v -= Mathf.Floor(v);
        return tex.GetPixelBilinear(u, v).r;
    }

    void CutAll()
    {
        Texture2D readable = GetReadable(holeTexture);
        if (readable == null) { logText = "ERROR: Cannot read texture."; return; }

        Vector4 st = new Vector4(holeTiling.x, holeTiling.y, holeOffset.x, holeOffset.y);
        Undo.SetCurrentGroupName("Cut Terrain Holes");
        int undoGroup = Undo.GetCurrentGroup();
        logText = "";

        for (int i = 0; i < targets.Count; i++)
        {
            var go = targets[i];
            if (go == null) continue;
            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            EditorUtility.DisplayProgressBar("Cutting", $"{go.name} ({i + 1}/{targets.Count})", (float)i / targets.Count);
            logText += CutSingle(go, mf, readable, st) + "\n";
        }

        EditorUtility.ClearProgressBar();
        Undo.CollapseUndoOperations(undoGroup);
        AssetDatabase.SaveAssets();
        logText += "Done!";
    }

    string CutSingle(GameObject go, MeshFilter mf, Texture2D tex, Vector4 st)
    {
        Mesh src = mf.sharedMesh;
        Undo.RecordObject(mf, "Cut: " + go.name);

        if (createBackup)
            AssetDatabase.CreateAsset(Instantiate(src),
                AssetDatabase.GenerateUniqueAssetPath($"Assets/{src.name}_backup.asset"));

        Vector3[] sV = src.vertices;
        Vector3[] sN = src.normals;
        Vector4[] sT = src.tangents;
        Vector2[] sU0 = src.uv;
        Vector2[] sU1 = src.uv2;
        Color[] sC = src.colors;
        int[] sTri = src.triangles;

        Vector2[] smpUV = (useUV0 || sU1 == null || sU1.Length != sV.Length) ? sU0 : sU1;
        if (smpUV == null || smpUV.Length != sV.Length)
            return $"{go.name}: SKIP — no UVs";

        bool hN = sN != null && sN.Length == sV.Length;
        bool hT = sT != null && sT.Length == sV.Length;
        bool hU1 = sU1 != null && sU1.Length == sV.Length;
        bool hC = sC != null && sC.Length == sV.Length;

        var mb = new MeshBuilder(sV.Length);
        int kept = 0, removed = 0, subdivided = 0;

        for (int t = 0; t < sTri.Length / 3; t++)
        {
            int i0 = sTri[t * 3], i1 = sTri[t * 3 + 1], i2 = sTri[t * 3 + 2];

            bool sv0 = IsSolid(tex, smpUV[i0], st);
            bool sv1 = IsSolid(tex, smpUV[i1], st);
            bool sv2 = IsSolid(tex, smpUV[i2], st);
            Vector2 cen = (smpUV[i0] + smpUV[i1] + smpUV[i2]) / 3f;
            bool svC = IsSolid(tex, cen, st);

            int solidCount = (sv0 ? 1 : 0) + (sv1 ? 1 : 0) + (sv2 ? 1 : 0) + (svC ? 1 : 0);

            if (solidCount == 4)
            {
                mb.AddTri(
                    mb.AddVert(sV[i0], hN ? sN[i0] : Vector3.up, hT ? sT[i0] : Vector4.zero,
                        sU0[i0], hU1 ? sU1[i0] : Vector2.zero, hC ? sC[i0] : Color.white,
                        hN, hT, hU1, hC, i0),
                    mb.AddVert(sV[i1], hN ? sN[i1] : Vector3.up, hT ? sT[i1] : Vector4.zero,
                        sU0[i1], hU1 ? sU1[i1] : Vector2.zero, hC ? sC[i1] : Color.white,
                        hN, hT, hU1, hC, i1),
                    mb.AddVert(sV[i2], hN ? sN[i2] : Vector3.up, hT ? sT[i2] : Vector4.zero,
                        sU0[i2], hU1 ? sU1[i2] : Vector2.zero, hC ? sC[i2] : Color.white,
                        hN, hT, hU1, hC, i2)
                );
                kept++;
                continue;
            }

            if (solidCount == 0)
            {
                removed++;
                continue;
            }

            subdivided++;

            float uvArea = Mathf.Abs(
                (smpUV[i1].x - smpUV[i0].x) * (smpUV[i2].y - smpUV[i0].y) -
                (smpUV[i2].x - smpUV[i0].x) * (smpUV[i1].y - smpUV[i0].y)) * 0.5f;
            float texelArea = uvArea * tex.width * tex.height * Mathf.Abs(st.x * st.y);
            int N = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(texelArea) * 0.5f), 2, maxSubdivLevel);

            int gridW = N + 1;
            int[,] gridIdx = new int[gridW, gridW];

            for (int gi = 0; gi <= N; gi++)
            {
                for (int gj = 0; gj <= N - gi; gj++)
                {
                    float a = (float)gi / N;
                    float b = (float)gj / N;
                    float c = 1f - a - b;

                    Vector3 pos = sV[i0] * a + sV[i1] * b + sV[i2] * c;
                    Vector2 u0 = sU0[i0] * a + sU0[i1] * b + sU0[i2] * c;

                    Vector3 nrm = Vector3.up;
                    if (hN) nrm = (sN[i0] * a + sN[i1] * b + sN[i2] * c).normalized;

                    Vector4 tan = Vector4.zero;
                    if (hT) tan = sT[i0] * a + sT[i1] * b + sT[i2] * c;

                    Vector2 u1v = Vector2.zero;
                    if (hU1) u1v = sU1[i0] * a + sU1[i1] * b + sU1[i2] * c;

                    Color col = Color.white;
                    if (hC) col = sC[i0] * a + sC[i1] * b + sC[i2] * c;

                    gridIdx[gi, gj] = mb.AddNewVert(pos, nrm, tan, u0, u1v, col, hN, hT, hU1, hC);
                }
            }

            for (int gi = 0; gi < N; gi++)
            {
                for (int gj = 0; gj < N - gi; gj++)
                {
                    int va = gridIdx[gi, gj];
                    int vb = gridIdx[gi + 1, gj];
                    int vc = gridIdx[gi, gj + 1];

                    Vector2 uvA = mb.GetUV0(va);
                    Vector2 uvB = mb.GetUV0(vb);
                    Vector2 uvC = mb.GetUV0(vc);
                    Vector2 upCen = (uvA + uvB + uvC) / 3f;
                    bool upSolid = IsSolid(tex, upCen, st);

                    bool hasDown = (gi + 1 + gj + 1) <= N;
                    bool downSolid = false;
                    int vd = -1, ve = -1, vf = -1;

                    if (hasDown)
                    {
                        vd = gridIdx[gi + 1, gj];
                        ve = gridIdx[gi + 1, gj + 1];
                        vf = gridIdx[gi, gj + 1];

                        Vector2 uvD = mb.GetUV0(vd);
                        Vector2 uvE = mb.GetUV0(ve);
                        Vector2 uvF = mb.GetUV0(vf);
                        Vector2 downCen = (uvD + uvE + uvF) / 3f;
                        downSolid = IsSolid(tex, downCen, st);
                    }

                    if (upSolid && hasDown && downSolid)
                    {
                        mb.AddTri(va, vb, ve);
                        mb.AddTri(va, ve, vc);
                    }
                    else
                    {
                        if (upSolid) mb.AddTri(va, vb, vc);
                        if (hasDown && downSolid) mb.AddTri(vd, ve, vf);
                    }
                }
            }
        }

        Mesh nm = mb.Build(src.name + "_HoleCut", hN, hT, hU1, hC);
        string path = AssetDatabase.GenerateUniqueAssetPath($"Assets/{nm.name}.asset");
        AssetDatabase.CreateAsset(nm, path);
        mf.sharedMesh = nm;

        GameObjectUtility.SetStaticEditorFlags(go,
            GameObjectUtility.GetStaticEditorFlags(go) | StaticEditorFlags.ContributeGI);

        return $"{go.name}: kept {kept}, subdiv {subdivided}, removed {removed}" +
               $" → {nm.triangles.Length / 3} tris, {nm.vertexCount} verts";
    }

    class MeshBuilder
    {
        public List<Vector3> verts;
        public List<Vector3> normals;
        public List<Vector4> tangents;
        public List<Vector2> uv0;
        public List<Vector2> uv1;
        public List<Color> colors;
        public List<int> tris;

        Dictionary<int, int> remap = new Dictionary<int, int>();

        public MeshBuilder(int hint)
        {
            verts = new List<Vector3>(hint);
            normals = new List<Vector3>(hint);
            tangents = new List<Vector4>(hint);
            uv0 = new List<Vector2>(hint);
            uv1 = new List<Vector2>(hint);
            colors = new List<Color>(hint);
            tris = new List<int>(hint * 3);
        }

        /// <summary>Add vertex, reuse if same original index</summary>
        public int AddVert(Vector3 v, Vector3 n, Vector4 t, Vector2 u0, Vector2 u1v,
            Color c, bool hN, bool hT, bool hU1, bool hC, int origIdx)
        {
            if (remap.TryGetValue(origIdx, out int existing)) return existing;
            int idx = AddNewVert(v, n, t, u0, u1v, c, hN, hT, hU1, hC);
            remap[origIdx] = idx;
            return idx;
        }

        /// <summary>Add new vertex (no reuse)</summary>
        public int AddNewVert(Vector3 v, Vector3 n, Vector4 t, Vector2 u0v, Vector2 u1v,
            Color c, bool hN, bool hT, bool hU1, bool hC)
        {
            int idx = verts.Count;
            verts.Add(v);
            uv0.Add(u0v);
            if (hN) normals.Add(n);
            if (hT) tangents.Add(t);
            if (hU1) uv1.Add(u1v);
            if (hC) colors.Add(c);
            return idx;
        }

        public Vector2 GetUV0(int idx) => uv0[idx];

        public void AddTri(int a, int b, int c)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        public Mesh Build(string name, bool hN, bool hT, bool hU1, bool hC)
        {
            var m = new Mesh();
            m.name = name;
            m.indexFormat = verts.Count > 65535
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(verts);
            if (hN) m.SetNormals(normals);
            if (hT) m.SetTangents(tangents);
            m.SetUVs(0, uv0);
            if (hU1) m.SetUVs(1, uv1);
            if (hC) m.SetColors(colors);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            if (!hN) m.RecalculateNormals();
            return m;
        }
    }

    Texture2D GetReadable(Texture2D src)
    {
        if (src == cachedSource && cachedReadable != null) return cachedReadable;
        if (cachedReadable != null && cachedReadable != cachedSource)
            DestroyImmediate(cachedReadable);
        cachedSource = src;
        cachedReadable = MakeReadable(src);
        return cachedReadable;
    }

    static Texture2D MakeReadable(Texture2D src)
    {
        if (src == null) return null;
        if (src.isReadable) return src;
        string path = AssetDatabase.GetAssetPath(src);
        if (!string.IsNullOrEmpty(path))
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null && !imp.isReadable)
            {
                imp.isReadable = true; imp.SaveAndReimport();
                src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (src != null && src.isReadable) return src;
            }
        }
        RenderTexture tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, tmp);
        var prev = RenderTexture.active; RenderTexture.active = tmp;
        Texture2D r = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        r.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); r.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
        return r;
    }

    static void DrawHeader(string text, Color color)
    {
        Rect r = GUILayoutUtility.GetRect(1f, 28f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.8f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2f), color);
        GUIStyle s = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = color } };
        EditorGUI.LabelField(r, text, s);
    }
}