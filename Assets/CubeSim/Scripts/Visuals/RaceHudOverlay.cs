using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CubeSim.Racers;

namespace CubeSim.Visuals
{
    /// <summary>
    /// HUD C, the race HUD: a START-to-GOAL progress bar across the top with one marker per cube,
    /// a "X TAKES THE LEAD!" banner when the leader changes, and a top-three ticker with rank
    /// arrows. Positions come from the walking-distance field (Racer.GoalDistance), so the bar
    /// shows who is really ahead on the course, not who has bounced around the most.
    ///
    /// Presentation only: reads racer state, never writes it. Legacy uGUI like the other overlays.
    /// Text stays inside the Shorts safe zones (clear of the right-hand button column and the
    /// bottom title band).
    /// </summary>
    public sealed class RaceHudOverlay : MonoBehaviour
    {
        private sealed class Marker
        {
            public Racer Racer;
            public RectTransform Rect;
            public Image Fill;
            public Text Label;
            public float Shown = -1f;
        }

        private sealed class Chip
        {
            public RectTransform Rect;
            public Text Text;
        }

        private const float LeadHoldSeconds = 0.6f;
        private const float BannerSeconds = 1.6f;
        private const float ArrowSeconds = 1.5f;

        private readonly List<Marker> _markers = new List<Marker>();
        private readonly List<Chip> _chips = new List<Chip>();
        private readonly Dictionary<Racer, int> _lastRank = new Dictionary<Racer, int>();
        private readonly Dictionary<Racer, float> _arrowUntil = new Dictionary<Racer, float>();
        private readonly Dictionary<Racer, int> _arrowDir = new Dictionary<Racer, int>();

        private Racer[] _racers;
        private Racer[] _sorted;
        private bool _portrait;
        private Font _body;
        private Font _display;
        private CanvasScaler _scaler;
        private RectTransform _barPanel;
        private RectTransform _track;
        private Text _centreLabel;
        private RectTransform _banner;
        private Text _bannerText;
        private RectTransform _ticker;
        private System.Func<string> _counter;
        private string _rule = "FIRST TO GOAL WINS";

        private float _startDistance;
        private Racer _leader;
        private Racer _pendingLeader;
        private float _pendingSince;
        private float _bannerUntil;
        private float _clock;

        public static RaceHudOverlay Create(Transform parent)
        {
            var go = new GameObject("RaceHud");
            go.transform.SetParent(parent, false);
            return go.AddComponent<RaceHudOverlay>();
        }

        public void Bind(Racer[] racers, bool portrait, string rule, System.Func<string> counter)
        {
            _racers = racers;
            _sorted = racers != null ? (Racer[])racers.Clone() : null;
            _portrait = portrait;
            _counter = counter;
            if (!string.IsNullOrEmpty(rule)) _rule = rule.ToUpperInvariant();
            _startDistance = 0f;
            _leader = null;
            _pendingLeader = null;
            _bannerUntil = 0f;
            _clock = 0f;
            _lastRank.Clear();
            _arrowUntil.Clear();
            _arrowDir.Clear();

            if (_barPanel == null) BuildCanvas();
            ApplyLayout();

            foreach (Marker m in _markers) Destroy(m.Rect.gameObject);
            _markers.Clear();
            if (racers == null) return;
            for (int i = 0; i < racers.Length; i++) _markers.Add(BuildMarker(racers[i]));
        }

        private void Update()
        {
            if (_racers == null) return;
            _clock += Time.deltaTime;

            UpdateStartDistance();
            UpdateMarkers();
            UpdateStandings();
            UpdateBanner();

            if (_centreLabel != null)
            {
                // Only the live count fits between START and GOAL; the rule itself is the title card's job.
                string counter = _counter != null ? _counter() : "";
                _centreLabel.text = string.IsNullOrEmpty(counter) ? "FIRST TO THE GOAL" : counter.ToUpperInvariant();
            }
        }

        // ---------------------------------------------------------------- state

        private void UpdateStartDistance()
        {
            // The course length is the farthest any cube stood from the goal when it first had a
            // reading, so the bar starts with everyone near the left edge, not wherever they spawned.
            for (int i = 0; i < _racers.Length; i++)
            {
                float d = _racers[i].GoalDistance;
                if (d < float.MaxValue * 0.5f && d > _startDistance) _startDistance = d;
            }
        }

        private float Progress(Racer racer)
        {
            if (racer.ReachedGoal) return 1f;
            if (_startDistance <= 0.01f || racer.GoalDistance >= float.MaxValue * 0.5f) return 0f;
            return Mathf.Clamp01(1f - racer.GoalDistance / _startDistance);
        }

        private void UpdateMarkers()
        {
            float width = _track.rect.width;
            float ease = 1f - Mathf.Exp(-Time.deltaTime * 10f);

            for (int i = 0; i < _markers.Count; i++)
            {
                Marker m = _markers[i];
                bool visible = m.Racer.Alive || m.Racer.ReachedGoal;
                if (m.Rect.gameObject.activeSelf != visible) m.Rect.gameObject.SetActive(visible);
                if (!visible) continue;

                float target = Progress(m.Racer);
                m.Shown = m.Shown < 0f ? target : Mathf.Lerp(m.Shown, target, ease);
                m.Rect.anchoredPosition = new Vector2(m.Shown * width, 0f);
                m.Label.text = m.Racer.ReachedGoal && m.Racer.Placement > 0 ? m.Racer.Placement.ToString() : "";
            }

            // Leaders drawn last, so the front of the pack is never hidden behind the back.
            _markers.Sort((a, b) => a.Shown.CompareTo(b.Shown));
            for (int i = 0; i < _markers.Count; i++) _markers[i].Rect.SetSiblingIndex(i);
        }

        private void UpdateStandings()
        {
            System.Array.Sort(_sorted, Compare);

            for (int i = 0; i < _sorted.Length; i++)
            {
                Racer r = _sorted[i];
                if (_lastRank.TryGetValue(r, out int was) && was != i && r.IsActive)
                {
                    _arrowDir[r] = i < was ? 1 : -1;
                    _arrowUntil[r] = _clock + ArrowSeconds;
                }

                _lastRank[r] = i;
            }

            for (int c = 0; c < _chips.Count; c++)
            {
                Chip chip = _chips[c];
                bool show = c < _sorted.Length && (_sorted[c].Alive || _sorted[c].ReachedGoal);
                chip.Rect.gameObject.SetActive(show);
                if (!show) continue;

                Racer r = _sorted[c];
                string name = string.IsNullOrEmpty(r.DisplayName) ? r.Id : r.DisplayName;
                string hex = ColorUtility.ToHtmlStringRGB(Color.Lerp(r.Color, Color.white, 0.15f));
                string arrow = "";
                if (_arrowUntil.TryGetValue(r, out float until) && until > _clock)
                {
                    arrow = _arrowDir[r] > 0 ? " <color=#6BFF7A>▲</color>" : " <color=#FF6B6B>▼</color>";
                }

                chip.Text.text = $"{c + 1} <color=#{hex}>{name}</color>{arrow}";
            }

            // Lead changes only count once the new leader has held it a moment and the race has
            // properly started; otherwise two cubes trading places every frame spam the banner.
            Racer front = null;
            for (int i = 0; i < _sorted.Length; i++)
            {
                if (_sorted[i].IsActive || _sorted[i].ReachedGoal) { front = _sorted[i]; break; }
            }

            if (front == null || front == _leader) { _pendingLeader = null; return; }
            if (front != _pendingLeader) { _pendingLeader = front; _pendingSince = _clock; return; }
            if (_clock - _pendingSince < LeadHoldSeconds) return;

            bool first = _leader == null;
            _leader = front;
            _pendingLeader = null;
            if (!first && _clock > 3f && !front.ReachedGoal) ShowBanner(front);
        }

        private static int Compare(Racer a, Racer b)
        {
            if (a.ReachedGoal != b.ReachedGoal) return a.ReachedGoal ? -1 : 1;
            if (a.ReachedGoal) return a.Placement.CompareTo(b.Placement);
            if (a.Alive != b.Alive) return a.Alive ? -1 : 1;
            if (!a.Alive) return b.DeathTime.CompareTo(a.DeathTime);
            int d = a.GoalDistance.CompareTo(b.GoalDistance);
            return d != 0 ? d : a.Index.CompareTo(b.Index);
        }

        private void ShowBanner(Racer leader)
        {
            string name = string.IsNullOrEmpty(leader.DisplayName) ? leader.Id : leader.DisplayName;
            _bannerText.text = $"{name} TAKES THE LEAD!";
            _bannerUntil = _clock + BannerSeconds;
        }

        private void UpdateBanner()
        {
            float left = _bannerUntil - _clock;
            bool show = left > 0f;
            if (_banner.gameObject.activeSelf != show) _banner.gameObject.SetActive(show);
            if (!show) return;

            // Pop in, hold, shrink away.
            float age = BannerSeconds - left;
            float scale = age < 0.15f ? Mathf.Lerp(0.6f, 1.08f, age / 0.15f)
                : age < 0.25f ? Mathf.Lerp(1.08f, 1f, (age - 0.15f) / 0.1f)
                : left < 0.2f ? Mathf.Lerp(0f, 1f, left / 0.2f)
                : 1f;
            _banner.localScale = Vector3.one * scale;
        }

        // ---------------------------------------------------------------- construction

        private void BuildCanvas()
        {
            _body = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _display = Resources.Load<Font>("Bangers-Regular");
            if (_display == null) _display = _body;

            var canvasGo = new GameObject("RaceHudCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 41;
            _scaler = canvasGo.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // ---- progress bar panel
            _barPanel = Panel(canvasGo.transform, "Bar", new Color(0.08f, 0.09f, 0.11f, 0.92f));
            _centreLabel = Label(_barPanel, "Rule", _body, 26, new Color(1f, 0.82f, 0.29f), TextAnchor.UpperCenter);
            Stretch(_centreLabel.rectTransform, new Vector2(0.22f, 1f), new Vector2(0.78f, 1f), new Vector2(0f, -14f), new Vector2(0f, 34f));
            Text start = Label(_barPanel, "Start", _body, 24, Color.white, TextAnchor.UpperLeft);
            start.text = "START";
            Stretch(start.rectTransform, new Vector2(0f, 1f), new Vector2(0.3f, 1f), new Vector2(24f, -12f), new Vector2(0f, 34f));
            Text goal = Label(_barPanel, "Goal", _body, 24, Color.white, TextAnchor.UpperRight);
            goal.text = "GOAL";
            Stretch(goal.rectTransform, new Vector2(0.7f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(0f, 34f));

            RectTransform trackBack = Panel(_barPanel, "TrackBack", new Color(0.18f, 0.2f, 0.24f, 1f));
            Stretch(trackBack, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 24f), new Vector2(0f, 44f));
            trackBack.offsetMin = new Vector2(30f, 24f);
            trackBack.offsetMax = new Vector2(-30f, 68f);

            RectTransform goalDisc = Panel(trackBack, "GoalCap", new Color(0.55f, 0.89f, 0.56f, 1f));
            goalDisc.anchorMin = goalDisc.anchorMax = new Vector2(1f, 0.5f);
            goalDisc.pivot = new Vector2(1f, 0.5f);
            goalDisc.sizeDelta = new Vector2(44f, 44f);
            goalDisc.anchoredPosition = Vector2.zero;

            var trackGo = new GameObject("Track");
            trackGo.transform.SetParent(trackBack, false);
            _track = trackGo.AddComponent<RectTransform>();
            _track.anchorMin = new Vector2(0f, 0f);
            _track.anchorMax = new Vector2(1f, 1f);
            _track.offsetMin = new Vector2(22f, 0f);
            _track.offsetMax = new Vector2(-22f, 0f);

            // ---- lead banner
            _banner = Panel(canvasGo.transform, "LeadBanner", new Color(1f, 0.82f, 0.29f, 1f));
            var bannerOutline = _banner.gameObject.AddComponent<Outline>();
            bannerOutline.effectColor = new Color(0.08f, 0.09f, 0.11f, 1f);
            bannerOutline.effectDistance = new Vector2(5f, -5f);
            _bannerText = Label(_banner, "Text", _display, 64, new Color(0.08f, 0.09f, 0.11f), TextAnchor.MiddleCenter);
            Stretch(_bannerText.rectTransform, Vector2.zero, Vector2.one, new Vector2(20f, 0f), new Vector2(-20f, 0f));
            _bannerText.rectTransform.offsetMin = new Vector2(20f, 0f);
            _bannerText.rectTransform.offsetMax = new Vector2(-20f, 0f);
            _banner.gameObject.SetActive(false);

            // ---- top-three ticker
            var tickerGo = new GameObject("Ticker");
            tickerGo.transform.SetParent(canvasGo.transform, false);
            _ticker = tickerGo.AddComponent<RectTransform>();
            var layout = tickerGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = tickerGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < 3; i++)
            {
                RectTransform chipRect = Panel(_ticker, "Chip" + i, new Color(0.08f, 0.09f, 0.11f, 0.9f));
                var pad = chipRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                pad.padding = new RectOffset(18, 18, 8, 8);
                pad.childControlWidth = true;
                pad.childControlHeight = true;
                Text t = Label(chipRect, "Text", _body, 34, Color.white, TextAnchor.MiddleLeft);
                t.supportRichText = true;
                _chips.Add(new Chip { Rect = chipRect, Text = t });
            }
        }

        private void ApplyLayout()
        {
            if (_portrait)
            {
                _scaler.referenceResolution = new Vector2(1080f, 1920f);
                _scaler.matchWidthOrHeight = 0f;
                Place(_barPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(1032f, 150f));
                Place(_banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -250f), new Vector2(820f, 110f));
                // Bottom-left, above the title band and clear of the right-hand buttons.
                Place(_ticker, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 690f), Vector2.zero);
            }
            else
            {
                _scaler.referenceResolution = new Vector2(1920f, 1080f);
                _scaler.matchWidthOrHeight = 1f;
                Place(_barPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1500f, 130f));
                Place(_banner, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -220f), new Vector2(900f, 110f));
                Place(_ticker, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), Vector2.zero);
            }

            _banner.localRotation = Quaternion.Euler(0f, 0f, 3f);
        }

        private Marker BuildMarker(Racer racer)
        {
            var go = new GameObject("Marker_" + racer.Index);
            go.transform.SetParent(_track, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(46f, 46f);

            var border = go.AddComponent<Image>();
            border.color = Color.white;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(go.transform, false);
            var fill = fillGo.AddComponent<Image>();
            Color c = racer.Color; c.a = 1f;
            fill.color = c;
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = new Vector2(5f, 5f);
            fill.rectTransform.offsetMax = new Vector2(-5f, -5f);

            Text label = Label(go.transform, "Place", _display, 30, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            return new Marker { Racer = racer, Rect = rect, Fill = fill, Label = label };
        }

        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image.rectTransform;
        }

        private static Text Label(Transform parent, string name, Font font, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.fontStyle = font != null && font.name.StartsWith("Bangers") ? FontStyle.Normal : FontStyle.Bold;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            if (size != Vector2.zero) rect.sizeDelta = size;
        }
    }
}
