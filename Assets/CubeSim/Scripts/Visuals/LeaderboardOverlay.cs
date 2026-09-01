using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// Live standings down the left edge, one row per pet: face shot on a colour swatch, name,
    /// three hearts, score. Alive racers sort by kills then food then distance, finishers show
    /// their placement, the dead sink to the bottom greyed out - the ranking board the reference
    /// channels run, with the pet's identity front and centre.
    ///
    /// Presentation only - it polls simulation state a few times a second and never writes any.
    /// Built from legacy uGUI primitives so it needs no TextMeshPro setup and survives batch runs.
    /// </summary>
    public sealed class LeaderboardOverlay : MonoBehaviour
    {
        private sealed class Row
        {
            public RectTransform Root;
            public Image Swatch;
            public Image Portrait;
            public Text Name;
            public Text Hearts;
            public Text Score;
            public CanvasGroup Group;
        }

        private const float RowHeight = 44f;
        private const float RowWidth = 316f;
        private const float RefreshInterval = 0.2f;

        private static readonly Color HeartFull = new Color(1f, 0.22f, 0.3f);
        private static readonly Color HeartLost = new Color(0.28f, 0.26f, 0.3f);

        private readonly List<Row> _rows = new List<Row>();
        private Racer[] _racers;
        private Racer[] _sorted;
        private Font _font;
        private RectTransform _panel;
        private float _refreshTimer;

        public static LeaderboardOverlay Create(Transform parent)
        {
            var go = new GameObject("Leaderboard");
            go.transform.SetParent(parent, false);
            return go.AddComponent<LeaderboardOverlay>();
        }

        /// <summary>Builds one row per racer. Called when an episode starts.</summary>
        public void Bind(Racer[] racers)
        {
            _racers = racers;
            _sorted = racers != null ? (Racer[])racers.Clone() : null;

            if (_panel == null) BuildCanvas();

            for (int i = _rows.Count - 1; i >= 0; i--) Destroy(_rows[i].Root.gameObject);
            _rows.Clear();

            if (racers == null) return;

            for (int i = 0; i < racers.Length; i++) _rows.Add(BuildRow(i));
            Refresh();
        }

        private void Update()
        {
            if (_racers == null || _rows.Count == 0) return;

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer > 0f) return;

            _refreshTimer = RefreshInterval;
            Refresh();
        }

        private void Refresh()
        {
            System.Array.Sort(_sorted, CompareStanding);

            for (int i = 0; i < _rows.Count && i < _sorted.Length; i++)
            {
                Racer racer = _sorted[i];
                Row row = _rows[i];

                Color color = racer.Color;
                color.a = 1f;
                row.Swatch.color = racer.Alive ? color : Color.Lerp(color, Color.black, 0.55f);

                if (racer.Portrait != null)
                {
                    row.Portrait.enabled = true;
                    row.Portrait.sprite = racer.Portrait;
                }
                else
                {
                    row.Portrait.enabled = false;
                }

                string state = !racer.Alive ? "✕"
                    : racer.ReachedGoal ? $"#{racer.Placement}"
                    : i == 0 ? "★"
                    : "";

                string name = string.IsNullOrEmpty(racer.DisplayName) ? racer.Id : racer.DisplayName;
                row.Name.text = state.Length > 0 ? $"{state} {name}" : name;

                // Hearts only make sense on the hearts scale; a 100 hp prototype hides them.
                if (racer.MaxHealth <= 6f)
                {
                    int full = racer.Alive ? Mathf.Clamp(Mathf.CeilToInt(racer.Health), 0, 6) : 0;
                    int total = Mathf.Clamp(Mathf.CeilToInt(racer.MaxHealth), 1, 6);
                    row.Hearts.text =
                        $"<color=#{ColorUtility.ToHtmlStringRGB(HeartFull)}>{new string('♥', full)}</color>" +
                        $"<color=#{ColorUtility.ToHtmlStringRGB(HeartLost)}>{new string('♥', total - full)}</color>";
                }
                else
                {
                    row.Hearts.text = "";
                }

                string score = "";
                if (racer.FoodEaten > 0) score = racer.FoodEaten.ToString();
                if (racer.Kills > 0) score += (score.Length > 0 ? " " : "") + racer.Kills + "KO";
                row.Score.text = score;

                row.Group.alpha = racer.Alive ? 1f : 0.45f;
            }
        }

        private static int CompareStanding(Racer a, Racer b)
        {
            int aliveOrder = (a.IsActive ? 0 : a.ReachedGoal ? 1 : 2)
                .CompareTo(b.IsActive ? 0 : b.ReachedGoal ? 1 : 2);
            if (aliveOrder != 0) return aliveOrder;

            if (a.IsActive)
            {
                if (a.Kills != b.Kills) return b.Kills.CompareTo(a.Kills);
                if (a.FoodEaten != b.FoodEaten) return b.FoodEaten.CompareTo(a.FoodEaten);
                if (!Mathf.Approximately(a.DistanceTravelled, b.DistanceTravelled))
                {
                    return b.DistanceTravelled.CompareTo(a.DistanceTravelled);
                }

                return a.Index.CompareTo(b.Index);
            }

            if (a.ReachedGoal) return a.Placement.CompareTo(b.Placement);

            // Dead: the most recent elimination is the freshest story beat, so it sits highest.
            return b.DeathTime.CompareTo(a.DeathTime);
        }

        // ---------------------------------------------------------------- construction

        private void BuildCanvas()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("LeaderboardCanvas");
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;

            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            _panel = panelGo.AddComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 0.5f);
            _panel.anchorMax = new Vector2(0f, 0.5f);
            _panel.pivot = new Vector2(0f, 0.5f);
            _panel.anchoredPosition = new Vector2(16f, 0f);
        }

        private Row BuildRow(int index)
        {
            var rowGo = new GameObject($"Row_{index:D2}");
            rowGo.transform.SetParent(_panel, false);

            var rect = rowGo.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(RowWidth, RowHeight - 6f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);

            float top = (_racers.Length - 1) * 0.5f * RowHeight;
            rect.anchoredPosition = new Vector2(0f, top - index * RowHeight);

            var group = rowGo.AddComponent<CanvasGroup>();

            Image backdrop = rowGo.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.42f);

            // The face sits on its colour swatch, so the team colour still reads at a glance.
            var swatchGo = new GameObject("Swatch");
            swatchGo.transform.SetParent(rowGo.transform, false);
            var swatchRect = swatchGo.AddComponent<RectTransform>();
            swatchRect.sizeDelta = new Vector2(34f, 34f);
            swatchRect.anchorMin = swatchRect.anchorMax = new Vector2(0f, 0.5f);
            swatchRect.anchoredPosition = new Vector2(23f, 0f);
            Image swatch = swatchGo.AddComponent<Image>();

            var portraitGo = new GameObject("Portrait");
            portraitGo.transform.SetParent(swatchGo.transform, false);
            var portraitRect = portraitGo.AddComponent<RectTransform>();
            portraitRect.sizeDelta = new Vector2(32f, 32f);
            portraitRect.anchoredPosition = Vector2.zero;
            Image portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true;

            Text name = BuildText(rowGo.transform, "Name", 19, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = name.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            name.rectTransform.sizeDelta = new Vector2(120f, RowHeight);
            name.rectTransform.anchoredPosition = new Vector2(106f, 0f);

            Text hearts = BuildText(rowGo.transform, "Hearts", 20, TextAnchor.MiddleLeft);
            hearts.rectTransform.anchorMin = hearts.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            hearts.rectTransform.sizeDelta = new Vector2(70f, RowHeight);
            hearts.rectTransform.anchoredPosition = new Vector2(210f, 0f);

            Text score = BuildText(rowGo.transform, "Score", 18, TextAnchor.MiddleRight);
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            score.rectTransform.sizeDelta = new Vector2(70f, RowHeight);
            score.rectTransform.anchoredPosition = new Vector2(-40f, 0f);

            return new Row
            {
                Root = rect, Swatch = swatch, Portrait = portrait,
                Name = name, Hearts = hearts, Score = score, Group = group
            };
        }

        private Text BuildText(Transform parent, string name, int size, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.3f, -1.3f);

            return text;
        }
    }
}
