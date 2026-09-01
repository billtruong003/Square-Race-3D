using System.Collections.Generic;
using UnityEngine;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The little broken-heart that pops off a pet when it takes a hit: rises, hangs a beat and
    /// fades. World-space TextMesh, pooled, ticked on the render clock - the standard cosmetic
    /// contract, nothing here touches the simulation.
    /// </summary>
    public sealed class DamagePopupSystem
    {
        private sealed class Popup
        {
            public TextMesh Text;
            public float Age;
            public Vector3 Origin;
        }

        private const float Lifetime = 0.8f;
        private const float RiseHeight = 2.2f;

        private readonly List<Popup> _live = new List<Popup>();
        private readonly Stack<Popup> _idle = new Stack<Popup>();
        private readonly Transform _root;
        private readonly Font _font;

        public DamagePopupSystem(Transform parent)
        {
            _root = new GameObject("DamagePopups").transform;
            _root.SetParent(parent, false);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public void Show(Vector3 position, Color color)
        {
            Popup popup = _idle.Count > 0 ? _idle.Pop() : Build();

            popup.Age = 0f;
            popup.Origin = position + new Vector3(0f, 2.6f, 0f);
            popup.Text.color = color;
            popup.Text.transform.position = popup.Origin;
            popup.Text.gameObject.SetActive(true);
            _live.Add(popup);
        }

        public void Tick(float deltaTime)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Popup popup = _live[i];
                popup.Age += deltaTime;

                float t = Mathf.Clamp01(popup.Age / Lifetime);
                popup.Text.transform.position = popup.Origin + new Vector3(0f, 0f, RiseHeight * t);

                Color color = popup.Text.color;
                color.a = 1f - t * t;
                popup.Text.color = color;

                // A quick pop at birth, then settle.
                float scale = 1f + 0.6f * Mathf.Exp(-8f * popup.Age);
                popup.Text.transform.localScale = Vector3.one * scale;

                if (popup.Age < Lifetime) continue;

                popup.Text.gameObject.SetActive(false);
                _live.RemoveAt(i);
                _idle.Push(popup);
            }
        }

        private Popup Build()
        {
            var go = new GameObject("Popup");
            go.transform.SetParent(_root, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // flat, facing the top-down camera

            var text = go.AddComponent<TextMesh>();
            text.font = _font;
            go.GetComponent<MeshRenderer>().sharedMaterial = _font.material;

            text.text = "-♥";
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.fontSize = 64;
            text.characterSize = 0.028f;

            go.SetActive(false);
            return new Popup { Text = text };
        }
    }
}
