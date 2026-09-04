using UnityEngine;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The Hot Potato bomb as the viewer sees it: a black sphere with a lit fuse riding above the
    /// holder, and the countdown in big digits that go red and jitter in the last three seconds.
    /// Cosmetic only; it follows whatever the BombSystem says.
    /// </summary>
    public sealed class BombVisual
    {
        private readonly Transform _root;
        private readonly Transform _ball;
        private readonly TextMesh _digits;
        private readonly Material _ballMaterial;
        private readonly Material _fuseMaterial;
        private float _pulse;

        public BombVisual(Transform parent)
        {
            _root = new GameObject("BombVisual").transform;
            _root.SetParent(parent, false);

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            _ballMaterial = new Material(lit) { color = new Color(0.08f, 0.08f, 0.1f) };
            _ballMaterial.SetFloat("_Smoothness", 0.6f);
            _fuseMaterial = new Material(lit) { color = new Color(1f, 0.5f, 0.1f) };
            _fuseMaterial.EnableKeyword("_EMISSION");
            _fuseMaterial.SetColor("_EmissionColor", new Color(3f, 1.2f, 0.2f));

            _ball = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Object.Destroy(_ball.GetComponent<Collider>());
            _ball.name = "Ball";
            _ball.SetParent(_root, false);
            _ball.localScale = Vector3.one * 1.3f;
            _ball.GetComponent<MeshRenderer>().sharedMaterial = _ballMaterial;

            var fuse = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            Object.Destroy(fuse.GetComponent<Collider>());
            fuse.name = "Fuse";
            fuse.SetParent(_ball, false);
            fuse.localPosition = new Vector3(0.3f, 0.55f, 0f);
            fuse.localScale = Vector3.one * 0.35f;
            fuse.GetComponent<MeshRenderer>().sharedMaterial = _fuseMaterial;

            var textGo = new GameObject("Digits");
            textGo.transform.SetParent(_root, false);
            textGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            textGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _digits = textGo.AddComponent<TextMesh>();
            _digits.anchor = TextAnchor.MiddleCenter;
            _digits.alignment = TextAlignment.Center;
            _digits.fontSize = 64;
            _digits.characterSize = 0.06f;
            _digits.fontStyle = FontStyle.Bold;
            _digits.color = Color.white;

            _root.gameObject.SetActive(false);
        }

        public void Attach(Racer holder)
        {
            if (holder == null) { Detach(); return; }
            _root.SetParent(holder.Transform, false);
            _root.localPosition = new Vector3(0f, holder.HalfExtent * 2f + 1.2f, 0f);
            _root.localRotation = Quaternion.identity;
            _root.gameObject.SetActive(true);
        }

        public void Detach()
        {
            _root.gameObject.SetActive(false);
        }

        public void Tick(float fuse, float deltaTime)
        {
            if (!_root.gameObject.activeSelf) return;

            // Keep the bomb upright and unspun regardless of how the holder faces.
            _root.rotation = Quaternion.identity;

            _digits.text = fuse.ToString(fuse < 3f ? "0.0" : "0");
            bool hot = fuse < 3f;
            _digits.color = hot ? new Color(1f, 0.25f, 0.2f) : Color.white;

            _pulse += deltaTime * (hot ? 14f : 6f);
            float s = 1.3f + Mathf.Sin(_pulse) * (hot ? 0.18f : 0.06f);
            _ball.localScale = Vector3.one * s;
            if (hot) _root.localPosition += new Vector3(Mathf.Sin(_pulse * 3.1f) * 0.04f, 0f, Mathf.Cos(_pulse * 2.3f) * 0.04f);
        }
    }
}
