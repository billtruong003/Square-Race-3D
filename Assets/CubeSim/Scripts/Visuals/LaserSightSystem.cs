using System.Collections.Generic;
using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Thin red lines and floor marks that telegraph a shot: the sniper's laser that follows its
    /// target while it aims, the tracer left behind by a hitscan shot, and the red ring where a
    /// lobbed shell will land. Cosmetic only; it reads racer positions and never writes them.
    /// </summary>
    public sealed class LaserSightSystem
    {
        private sealed class Line
        {
            public LineRenderer Renderer;
            public Racer From, To;
            public Vector3 A, B;
            public float Left, Total;
            public bool Tracks;
        }

        private sealed class Mark
        {
            public Transform Root;
            public float Left, Total;
        }

        private readonly Transform _root;
        private readonly float _groundY;
        private readonly Material _laser;
        private readonly Material _mark;
        private readonly List<Line> _lines = new List<Line>();
        private readonly List<Mark> _marks = new List<Mark>();
        private readonly Stack<LineRenderer> _idleLines = new Stack<LineRenderer>();
        private readonly Stack<Transform> _idleMarks = new Stack<Transform>();

        public LaserSightSystem(Transform parent, float groundY)
        {
            _root = new GameObject("LaserSights").transform;
            _root.SetParent(parent, false);
            _groundY = groundY;
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            _laser = new Material(unlit) { color = new Color(1f, 0.15f, 0.1f, 0.9f) };
            _laser.SetColor("_BaseColor", new Color(1f, 0.15f, 0.1f, 0.9f));
            _mark = new Material(unlit) { color = new Color(1f, 0.2f, 0.1f, 0.55f) };
            _mark.SetColor("_BaseColor", new Color(1f, 0.2f, 0.1f, 0.55f));
            SetTransparent(_mark);
        }

        private static void SetTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.renderQueue = 3000;
        }

        /// <summary>A laser from the shooter to the target that follows both for the aim time.</summary>
        public void Aim(Racer from, Racer to, float seconds)
        {
            Line line = Take();
            line.From = from; line.To = to; line.Tracks = true;
            line.Left = line.Total = seconds;
            line.Renderer.startWidth = 0.12f; line.Renderer.endWidth = 0.12f;
            Place(line);
        }

        /// <summary>A fat tracer left in the air after a hitscan shot.</summary>
        public void Beam(Vector3 a, Vector3 b, float seconds)
        {
            Line line = Take();
            line.From = null; line.To = null; line.Tracks = false;
            line.A = a; line.B = b;
            line.Left = line.Total = seconds;
            line.Renderer.startWidth = 0.45f; line.Renderer.endWidth = 0.3f;
            Place(line);
        }

        /// <summary>A pulsing red ring on the floor where a shell will land.</summary>
        public void MarkFloor(Vector3 at, float radius, float seconds)
        {
            Transform t;
            if (_idleMarks.Count > 0) t = _idleMarks.Pop();
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.name = "LobMark";
                Object.Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = _mark;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.transform.SetParent(_root, false);
                t = go.transform;
            }
            t.gameObject.SetActive(true);
            t.position = new Vector3(at.x, _groundY + 0.05f, at.z);
            t.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            _marks.Add(new Mark { Root = t, Left = seconds, Total = seconds });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                Line line = _lines[i];
                line.Left -= deltaTime;
                if (line.Left <= 0f || (line.Tracks && (line.From == null || line.To == null || !line.To.IsActive || !line.From.IsActive)))
                {
                    line.Renderer.gameObject.SetActive(false);
                    _idleLines.Push(line.Renderer);
                    _lines.RemoveAt(i);
                    continue;
                }
                Place(line);
                if (!line.Tracks)
                {
                    float t = line.Left / Mathf.Max(0.01f, line.Total);
                    line.Renderer.startWidth = 0.45f * t; line.Renderer.endWidth = 0.3f * t;
                }
            }

            for (int i = _marks.Count - 1; i >= 0; i--)
            {
                Mark mark = _marks[i];
                mark.Left -= deltaTime;
                if (mark.Left <= 0f)
                {
                    mark.Root.gameObject.SetActive(false);
                    _idleMarks.Push(mark.Root);
                    _marks.RemoveAt(i);
                    continue;
                }
                float pulse = 1f + 0.15f * Mathf.Sin((mark.Total - mark.Left) * 18f);
                Vector3 s = mark.Root.localScale;
                float baseR = s.x / (1f + 0.15f * Mathf.Sin((mark.Total - mark.Left + deltaTime) * 18f));
                mark.Root.localScale = new Vector3(baseR * pulse, s.y, baseR * pulse);
            }
        }

        private Line Take()
        {
            LineRenderer r;
            if (_idleLines.Count > 0) r = _idleLines.Pop();
            else
            {
                var go = new GameObject("Laser");
                go.transform.SetParent(_root, false);
                r = go.AddComponent<LineRenderer>();
                r.sharedMaterial = _laser;
                r.positionCount = 2;
                r.useWorldSpace = true;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.numCapVertices = 3;
            }
            r.gameObject.SetActive(true);
            var line = new Line { Renderer = r };
            _lines.Add(line);
            return line;
        }

        private void Place(Line line)
        {
            Vector3 a = line.Tracks ? line.From.Position : line.A;
            Vector3 b = line.Tracks ? line.To.Position : line.B;
            a.y = _groundY + 1.6f; b.y = _groundY + 1.6f;
            line.Renderer.SetPosition(0, a);
            line.Renderer.SetPosition(1, b);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
        }
    }
}
