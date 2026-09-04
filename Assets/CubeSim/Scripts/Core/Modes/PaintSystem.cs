using UnityEngine;
using CubeSim.Arena;
using CubeSim.Racers;

namespace CubeSim.Core.Modes
{
    /// <summary>
    /// Paint War: the playable floor is a grid of tiles; a racer claims the tile under its centre
    /// every step, overwriting whoever held it. Racer.Score is its tile count. One mesh, one
    /// colour array, rebuilt only on the steps that changed something.
    /// </summary>
    public sealed class PaintSystem
    {
        private readonly int _columns;
        private readonly int _rows;
        private readonly float _tile;
        private readonly Vector2 _origin;
        private readonly int[] _owner;          // racer index per tile, -1 = unpainted
        private readonly bool[] _walkable;
        private readonly Mesh _mesh;
        private readonly Color[] _colors;
        private bool _dirty;

        public int Columns => _columns;
        public int Rows => _rows;
        public int PaintedTiles { get; private set; }

        public PaintSystem(ModeConfig config, ArenaRuntime arena, Transform parent, float groundY)
        {
            Rect rect = arena.PlayableRect;
            _tile = Mathf.Max(0.5f, config.paintTileSize);
            _columns = Mathf.Max(1, Mathf.RoundToInt(rect.width / _tile));
            _rows = Mathf.Max(1, Mathf.RoundToInt(rect.height / _tile));
            _tile = rect.width / _columns;   // exact fit across; rows use the same size
            _origin = new Vector2(rect.xMin, rect.yMin);

            int count = _columns * _rows;
            _owner = new int[count];
            _walkable = new bool[count];
            for (int i = 0; i < count; i++) _owner[i] = -1;

            // Tiles under walls never count; a wall rect that covers the centre owns the cell.
            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _columns; c++)
            {
                var centre = new Vector2(_origin.x + (c + 0.5f) * _tile, _origin.y + (r + 0.5f) * _tile);
                _walkable[r * _columns + c] = !arena.OverlapsWall(centre, _tile * 0.25f);
            }

            _mesh = BuildMesh(groundY);
            _colors = new Color[_mesh.vertexCount];
            var baseTint = new Color(0.16f, 0.16f, 0.18f, 1f);
            for (int i = 0; i < _colors.Length; i++) _colors[i] = baseTint;
            _mesh.colors = _colors;

            var go = new GameObject("PaintFloor");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            Shader shader = Shader.Find("CubeSim/VertexColorUnlit");
            renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Unlit"));
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public void Step(Racer[] racers)
        {
            for (int i = 0; i < racers.Length; i++)
            {
                Racer racer = racers[i];
                if (!racer.IsActive) continue;

                int c = Mathf.FloorToInt((racer.Position.x - _origin.x) / _tile);
                int r = Mathf.FloorToInt((racer.Position.z - _origin.y) / _tile);
                if (c < 0 || c >= _columns || r < 0 || r >= _rows) continue;

                int cell = r * _columns + c;
                if (!_walkable[cell] || _owner[cell] == i) continue;

                if (_owner[cell] >= 0) racers[_owner[cell]].Score--;
                else PaintedTiles++;
                _owner[cell] = i;
                racer.Score++;
                Tint(cell, racer.Color);
                _dirty = true;
            }

            if (_dirty)
            {
                _mesh.colors = _colors;
                _dirty = false;
            }
        }

        private void Tint(int cell, Color color)
        {
            var c = new Color(color.r * 0.85f, color.g * 0.85f, color.b * 0.85f, 1f);
            int v = cell * 4;
            _colors[v] = c; _colors[v + 1] = c; _colors[v + 2] = c; _colors[v + 3] = c;
        }

        private Mesh BuildMesh(float groundY)
        {
            int count = _columns * _rows;
            var vertices = new Vector3[count * 4];
            var triangles = new int[count * 6];
            float y = groundY + 0.03f;
            float inset = 0.06f;   // a hairline gap so the grid reads as tiles

            for (int r = 0; r < _rows; r++)
            for (int c = 0; c < _columns; c++)
            {
                int cell = r * _columns + c;
                float x0 = _origin.x + c * _tile + inset, x1 = _origin.x + (c + 1) * _tile - inset;
                float z0 = _origin.y + r * _tile + inset, z1 = _origin.y + (r + 1) * _tile - inset;
                int v = cell * 4;
                vertices[v] = new Vector3(x0, y, z0);
                vertices[v + 1] = new Vector3(x0, y, z1);
                vertices[v + 2] = new Vector3(x1, y, z1);
                vertices[v + 3] = new Vector3(x1, y, z0);
                int t = cell * 6;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
            }

            var mesh = new Mesh { name = "PaintFloor", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
