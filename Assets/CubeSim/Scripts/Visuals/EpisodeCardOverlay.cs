using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CubeSim.Visuals
{
    /// <summary>
    /// The full-screen story cards an episode is narrated with: the "who will win?" opener, a round
    /// card before each arena, a winner card after it, and the podium at the end. These are the
    /// retention devices of the reference channels - the simulation itself never sees them.
    ///
    /// Legacy uGUI on its own overlay canvas, sorted above the leaderboard.
    /// </summary>
    public sealed class EpisodeCardOverlay : MonoBehaviour
    {
        private static readonly (string name, Color color)[] ColorNames =
        {
            ("RED", new Color(0.95f, 0.16f, 0.16f)),
            ("GREEN", new Color(0.16f, 0.85f, 0.24f)),
            ("BLUE", new Color(0.18f, 0.36f, 0.98f)),
            ("YELLOW", new Color(0.98f, 0.86f, 0.14f)),
            ("ORANGE", new Color(0.98f, 0.45f, 0.10f)),
            ("MAGENTA", new Color(0.85f, 0.20f, 0.90f)),
            ("CYAN", new Color(0.20f, 0.90f, 0.92f)),
            ("VIOLET", new Color(0.55f, 0.22f, 0.95f)),
            ("PINK", new Color(0.95f, 0.55f, 0.72f)),
            ("LIME", new Color(0.72f, 0.95f, 0.30f)),
            ("TEAL", new Color(0.10f, 0.90f, 0.70f)),
            ("WHITE", new Color(0.95f, 0.95f, 0.95f)),
        };

        private Font _font;
        private RectTransform _card;
        private Image _backdrop;
        private Text _title;
        private Text _subtitle;
        private RectTransform _swatchRow;
        private CanvasScaler _scaler;
        private readonly List<Image> _swatches = new List<Image>();

        public static EpisodeCardOverlay Create(Transform parent)
        {
            var go = new GameObject("EpisodeCards");
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<EpisodeCardOverlay>();
            overlay.Build();
            return overlay;
        }

        /// <summary>The audience-bet opener: every contestant's colour and a question.</summary>
        public void ShowIntro(IReadOnlyList<Color> racerColors)
        {
            SetCard("WHO WILL WIN?", "place your bets", new Color(0.05f, 0.05f, 0.08f, 0.92f));
            SetSwatches(racerColors);
        }

        public void ShowRound(int round, int totalRounds, string arenaName, string rule = null)
        {
            string where = totalRounds > 1 ? $"of {totalRounds}  ·  {arenaName}" : arenaName;
            if (!string.IsNullOrEmpty(rule)) where += "\n" + rule;
            SetCard($"ROUND {round}", where,
                new Color(0.05f, 0.05f, 0.08f, 0.85f));
            SetSwatches(null);
        }

        public void ShowWinner(Color color, string cause, string name = null)
        {
            string who = string.IsNullOrEmpty(name) ? NameOf(color) : name;
            SetCard($"{who} WINS!", cause, new Color(0.05f, 0.05f, 0.08f, 0.85f));
            SetSwatches(new[] { color });
        }

        /// <summary>The closing podium: the champion big, the earlier round winners beneath.</summary>
        /// <summary>Knockout: who just went out, and how many are left.</summary>
        public void ShowEliminated(IReadOnlyList<string> names, IReadOnlyList<Color> colors, int remaining)
        {
            string who = string.Join("  ·  ", names);
            SetCard("ELIMINATED", $"{who}\n{remaining} left", new Color(0.25f, 0.03f, 0.05f, 0.9f));
            SetSwatches(colors);
        }

        /// <summary>Grand Prix: the points table after a round, top five.</summary>
        public void ShowStandings(IReadOnlyList<(string name, Color color, int points)> standings, int round, int totalRounds)
        {
            var sb = new System.Text.StringBuilder();
            var colors = new List<Color>();
            for (int i = 0; i < standings.Count && i < 5; i++)
            {
                if (i > 0) sb.Append("   ·   ");
                sb.Append(standings[i].name).Append(' ').Append(standings[i].points);
                colors.Add(standings[i].color);
            }
            SetCard($"STANDINGS  {round}/{totalRounds}", sb.ToString(), new Color(0.05f, 0.05f, 0.08f, 0.9f));
            SetSwatches(colors);
        }

        public void ShowPodium(Color champion, IReadOnlyList<Color> roundWinners)
        {
            SetCard($"{NameOf(champion)} IS THE CHAMPION!", "round winners", new Color(0.03f, 0.03f, 0.06f, 0.95f));
            SetSwatches(roundWinners);
        }

        public void Hide() => _card.gameObject.SetActive(false);

        /// <summary>
        /// 9:16 layout: scale by width instead of height (a height-matched 1920-wide layout is
        /// almost twice too wide for a 1080 frame) and let the title wrap inside the frame.
        /// </summary>
        public void SetPortrait(bool portrait)
        {
            if (_scaler == null) return;
            _scaler.referenceResolution = portrait ? new Vector2(1080f, 1920f) : new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = portrait ? 0f : 1f;

            float width = portrait ? 980f : 1800f;
            foreach (Text text in new[] { _title, _subtitle })
            {
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.rectTransform.sizeDelta = new Vector2(width, portrait ? 420f : 300f);
            }

            _title.fontSize = portrait ? 92 : 110;
            _subtitle.fontSize = portrait ? 40 : 38;
            _title.rectTransform.anchoredPosition = new Vector2(0f, portrait ? 140f : 80f);
            _subtitle.rectTransform.anchoredPosition = new Vector2(0f, portrait ? -60f : -20f);
            _swatchRow.anchoredPosition = new Vector2(0f, portrait ? -180f : -140f);
        }

        /// <summary>Nearest palette name, so a winner can be shouted without racer name plumbing.</summary>
        public static string NameOf(Color color)
        {
            string best = "COLOR";
            float bestDistance = float.MaxValue;

            foreach ((string name, Color reference) in ColorNames)
            {
                float distance = (color.r - reference.r) * (color.r - reference.r)
                               + (color.g - reference.g) * (color.g - reference.g)
                               + (color.b - reference.b) * (color.b - reference.b);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = name;
            }

            return best;
        }

        // ---------------------------------------------------------------- internals

        private void SetCard(string title, string subtitle, Color backdrop)
        {
            _card.gameObject.SetActive(true);
            _backdrop.color = backdrop;
            _title.text = title;
            _subtitle.text = subtitle;
        }

        private void SetSwatches(IReadOnlyList<Color> colors)
        {
            for (int i = 0; i < _swatches.Count; i++)
            {
                _swatches[i].gameObject.SetActive(colors != null && i < colors.Count);
                if (colors != null && i < colors.Count)
                {
                    Color c = colors[i];
                    c.a = 1f;
                    _swatches[i].color = c;
                }
            }

            if (colors == null) return;

            // Re-centre the row for however many swatches are showing.
            const float size = 64f, gap = 18f;
            float total = colors.Count * size + (colors.Count - 1) * gap;
            for (int i = 0; i < colors.Count && i < _swatches.Count; i++)
            {
                _swatches[i].rectTransform.anchoredPosition =
                    new Vector2(-total * 0.5f + size * 0.5f + i * (size + gap), 0f);
            }
        }

        private void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("CardCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            _scaler = scaler;

            var cardGo = new GameObject("Card");
            cardGo.transform.SetParent(canvasGo.transform, false);
            _card = cardGo.AddComponent<RectTransform>();
            _card.anchorMin = Vector2.zero;
            _card.anchorMax = Vector2.one;
            _card.offsetMin = Vector2.zero;
            _card.offsetMax = Vector2.zero;
            _backdrop = cardGo.AddComponent<Image>();

            _title = BuildText(cardGo.transform, "Title", 110, new Vector2(0f, 80f));
            _subtitle = BuildText(cardGo.transform, "Subtitle", 38, new Vector2(0f, -20f));
            _subtitle.color = new Color(1f, 1f, 1f, 0.75f);

            var rowGo = new GameObject("Swatches");
            rowGo.transform.SetParent(cardGo.transform, false);
            _swatchRow = rowGo.AddComponent<RectTransform>();
            _swatchRow.anchoredPosition = new Vector2(0f, -140f);

            for (int i = 0; i < 12; i++)
            {
                var swatchGo = new GameObject("Swatch_" + i);
                swatchGo.transform.SetParent(rowGo.transform, false);
                var image = swatchGo.AddComponent<Image>();
                image.rectTransform.sizeDelta = new Vector2(64f, 64f);
                image.gameObject.SetActive(false);
                _swatches.Add(image);
            }

            _card.gameObject.SetActive(false);
        }

        private Text BuildText(Transform parent, string name, int size, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.rectTransform.sizeDelta = new Vector2(1800f, 300f);
            text.rectTransform.anchoredPosition = offset;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);

            return text;
        }
    }
}
