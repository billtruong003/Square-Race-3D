using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The tournament bracket strip across the top of the frame: one box per group with a coloured
    /// tile per cube, a FINAL box that fills with each group's winner, and a crown on the champion.
    /// Losers grey out with a red cross. It is the thing that keeps a viewer until the last round,
    /// so it stays on screen for the whole episode. Bangers font when the project ships it.
    /// </summary>
    public sealed class BracketOverlay : MonoBehaviour
    {
        private sealed class Tile
        {
            public int Slot;
            public Image Fill;
            public Image Frame;
            public Text Cross;
            public Text Crown;
        }

        private const float TileSize = 34f;
        private const float TileGap = 6f;
        private const float GroupGap = 26f;
        private const float StripTop = 14f;

        private Font _font;
        private RectTransform _strip;
        private readonly List<List<Tile>> _groups = new List<List<Tile>>();
        private readonly List<Tile> _final = new List<Tile>();
        private readonly List<Text> _labels = new List<Text>();
        private RectTransform _finalBox;

        public static BracketOverlay Create(Transform parent)
        {
            var go = new GameObject("Bracket");
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<BracketOverlay>();
            overlay.BuildCanvas();
            return overlay;
        }

        private void BuildCanvas()
        {
            _font = Resources.Load<Font>("Bangers-Regular") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("BracketCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 55;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var stripGo = new GameObject("Strip");
            stripGo.transform.SetParent(canvasGo.transform, false);
            _strip = stripGo.AddComponent<RectTransform>();
            _strip.anchorMin = _strip.anchorMax = new Vector2(0.5f, 1f);
            _strip.pivot = new Vector2(0.5f, 1f);
            _strip.anchoredPosition = new Vector2(0f, -StripTop);
        }

        /// <summary>Lay the bracket out: one box per group, then the final box sized for the winners.</summary>
        public void Setup(IReadOnlyList<IReadOnlyList<(int slot, Color color)>> groups, int finalists)
        {
            foreach (Transform child in _strip) Destroy(child.gameObject);
            _groups.Clear(); _final.Clear(); _labels.Clear();

            float x = 0f;
            var boxes = new List<(RectTransform rect, float width)>();
            for (int g = 0; g < groups.Count; g++)
            {
                float width = groups[g].Count * TileSize + (groups[g].Count - 1) * TileGap + 20f;
                RectTransform box = Box($"GROUP {g + 1}", width, new Color(0.05f, 0.05f, 0.08f, 0.72f));
                var tiles = new List<Tile>();
                for (int i = 0; i < groups[g].Count; i++)
                    tiles.Add(MakeTile(box, groups[g][i].slot, groups[g][i].color, 10f + i * (TileSize + TileGap)));
                _groups.Add(tiles);
                boxes.Add((box, width));
                x += width + GroupGap;
            }

            float finalWidth = finalists * TileSize + Mathf.Max(0, finalists - 1) * TileGap + 20f;
            _finalBox = Box("FINAL", finalWidth, new Color(0.35f, 0.25f, 0.02f, 0.85f));
            for (int i = 0; i < finalists; i++)
                _final.Add(MakeTile(_finalBox, -1, new Color(0.25f, 0.25f, 0.28f, 1f), 10f + i * (TileSize + TileGap), true));
            boxes.Add((_finalBox, finalWidth));
            x += finalWidth;

            // Centre the row.
            float cursor = -x * 0.5f;
            for (int i = 0; i < boxes.Count; i++)
            {
                boxes[i].rect.anchoredPosition = new Vector2(cursor, 0f);
                cursor += boxes[i].width + GroupGap;
            }
        }

        /// <summary>A group is decided: the winner gets a gold frame and moves into the final box; the rest are crossed out.</summary>
        public void MarkGroup(int group, int winnerSlot)
        {
            if (group < 0 || group >= _groups.Count) return;
            int finalIndex = 0;
            for (int g = 0; g < group; g++) if (GroupWinner(g) != null) finalIndex++;

            foreach (Tile tile in _groups[group])
            {
                if (tile.Slot == winnerSlot)
                {
                    tile.Frame.color = new Color(1f, 0.85f, 0.2f, 1f);
                    tile.Frame.rectTransform.sizeDelta = new Vector2(TileSize + 8f, TileSize + 8f);
                    if (finalIndex < _final.Count)
                    {
                        _final[finalIndex].Slot = winnerSlot;
                        _final[finalIndex].Fill.color = tile.Fill.color;
                        _final[finalIndex].Cross.gameObject.SetActive(false);
                    }
                }
                else Eliminate(tile);
            }
        }

        /// <summary>The final is decided: losers crossed, the champion crowned.</summary>
        public void Crown(int championSlot)
        {
            foreach (Tile tile in _final)
            {
                if (tile.Slot == championSlot)
                {
                    tile.Frame.color = new Color(1f, 0.85f, 0.2f, 1f);
                    tile.Frame.rectTransform.sizeDelta = new Vector2(TileSize + 10f, TileSize + 10f);
                    tile.Crown.gameObject.SetActive(true);
                }
                else if (tile.Slot >= 0) Eliminate(tile);
            }
            foreach (var group in _groups) foreach (Tile tile in group) if (tile.Slot == championSlot) tile.Crown.gameObject.SetActive(true);
        }

        public void SetVisible(bool visible) => _strip.gameObject.SetActive(visible);

        private Tile GroupWinner(int g)
        {
            foreach (Tile t in _groups[g]) if (t.Frame.color.r > 0.9f && t.Frame.color.g > 0.8f) return t;
            return null;
        }

        private static void Eliminate(Tile tile)
        {
            Color c = tile.Fill.color;
            tile.Fill.color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 1f);
            tile.Cross.gameObject.SetActive(true);
        }

        private RectTransform Box(string label, float width, Color back)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_strip, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, TileSize + 44f);
            var image = go.AddComponent<Image>();
            image.color = back;

            var text = MakeText(go.transform, label, 22, new Color(1f, 0.9f, 0.5f));
            text.rectTransform.anchorMin = new Vector2(0f, 1f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 1f);
            text.rectTransform.anchoredPosition = new Vector2(0f, -3f);
            text.rectTransform.sizeDelta = new Vector2(0f, 26f);
            _labels.Add(text);
            return rect;
        }

        private Tile MakeTile(RectTransform box, int slot, Color color, float x, bool placeholder = false)
        {
            var frameGo = new GameObject("Frame");
            frameGo.transform.SetParent(box, false);
            var frame = frameGo.AddComponent<Image>();
            frame.color = placeholder ? new Color(0.5f, 0.45f, 0.3f, 0.6f) : new Color(0f, 0f, 0f, 0.9f);
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0f, 0f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchoredPosition = new Vector2(x + TileSize * 0.5f, 8f + TileSize * 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(TileSize + 4f, TileSize + 4f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(frameGo.transform, false);
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(3f, 3f);
            fill.rectTransform.offsetMax = new Vector2(-3f, -3f);

            Text cross = MakeText(frameGo.transform, placeholder ? "?" : "✕", placeholder ? 26 : 30, placeholder ? new Color(1f, 0.9f, 0.5f) : new Color(1f, 0.15f, 0.1f));
            cross.rectTransform.anchorMin = Vector2.zero; cross.rectTransform.anchorMax = Vector2.one;
            cross.rectTransform.offsetMin = cross.rectTransform.offsetMax = Vector2.zero;
            cross.gameObject.SetActive(placeholder);

            Text crown = MakeText(frameGo.transform, "♛", 30, new Color(1f, 0.85f, 0.2f));
            crown.rectTransform.anchorMin = crown.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            crown.rectTransform.pivot = new Vector2(0.5f, 0f);
            crown.rectTransform.anchoredPosition = new Vector2(0f, 0f);
            crown.rectTransform.sizeDelta = new Vector2(40f, 30f);
            crown.gameObject.SetActive(false);

            return new Tile { Slot = slot, Fill = fill, Frame = frame, Cross = cross, Crown = crown };
        }

        private Text MakeText(Transform parent, string value, int size, Color color)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }
    }
}
