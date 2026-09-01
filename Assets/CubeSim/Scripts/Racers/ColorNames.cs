using UnityEngine;

namespace CubeSim.Racers
{
    /// <summary>
    /// Turns a racer tint into a shoutable name for the UI - RED, TEAL, PINK - by nearest match
    /// against a small set of anchor colours. Deterministic: same palette, same names.
    /// </summary>
    public static class ColorNames
    {
        private struct Anchor
        {
            public string Name;
            public Color Color;

            public Anchor(string name, float r, float g, float b)
            {
                Name = name;
                Color = new Color(r, g, b);
            }
        }

        private static readonly Anchor[] Anchors =
        {
            new Anchor("RED",     1.00f, 0.15f, 0.15f),
            new Anchor("ORANGE",  1.00f, 0.55f, 0.10f),
            new Anchor("YELLOW",  1.00f, 0.95f, 0.15f),
            new Anchor("LIME",    0.65f, 1.00f, 0.15f),
            new Anchor("GREEN",   0.10f, 0.85f, 0.25f),
            new Anchor("TEAL",    0.10f, 0.90f, 0.70f),
            new Anchor("CYAN",    0.15f, 0.90f, 1.00f),
            new Anchor("BLUE",    0.20f, 0.40f, 1.00f),
            new Anchor("NAVY",    0.10f, 0.10f, 0.60f),
            new Anchor("PURPLE",  0.60f, 0.25f, 1.00f),
            new Anchor("MAGENTA", 1.00f, 0.20f, 0.95f),
            new Anchor("PINK",    1.00f, 0.55f, 0.80f),
            new Anchor("BROWN",   0.55f, 0.35f, 0.18f),
            new Anchor("WHITE",   0.95f, 0.95f, 0.95f),
            new Anchor("GRAY",    0.50f, 0.50f, 0.50f),
            new Anchor("BLACK",   0.12f, 0.12f, 0.12f),
        };

        public static string NameFor(Color color)
        {
            string best = "CUBE";
            float bestDistance = float.MaxValue;

            foreach (Anchor anchor in Anchors)
            {
                float dr = color.r - anchor.Color.r;
                float dg = color.g - anchor.Color.g;
                float db = color.b - anchor.Color.b;
                float distance = dr * dr + dg * dg + db * db;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = anchor.Name;
                }
            }

            return best;
        }
    }
}
