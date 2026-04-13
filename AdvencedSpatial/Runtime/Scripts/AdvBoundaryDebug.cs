using System.Collections.Generic;
using UnityEngine;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Overlay de debug pour AdvBoundary — même style graphique que VaroniaLatencyChart.
    /// Ajouté automatiquement par AdvBoundary sur le même GameObject.
    /// Affiche : statut inside/outside, distance au mur le plus proche, segments actifs, son.
    /// </summary>
    public class AdvBoundaryDebug : MonoBehaviour
    {
        // ─── Config ───────────────────────────────────────────────────────────────

        public enum DisplayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Display")]
        [SerializeField] private DisplayCorner corner  = DisplayCorner.BottomRight;
        [SerializeField] private Vector2       margin  = new Vector2(12f, 12f);
        [SerializeField] private Vector2       size    = new Vector2(280f, 100f);
        [SerializeField] private bool          show    = true;

        /// <summary>Facteur d'échelle manuel (1 = 1080p).</summary>
        [Header("UI Scale")]
        public float scaleFactor = 1f;

        // ─── Colors ───────────────────────────────────────────────────────────────

        static readonly Color ColBg      = new Color(0.11f, 0.11f, 0.14f, 0.92f);
        static readonly Color ColGood    = new Color(0.30f, 0.85f, 0.65f, 1f);
        static readonly Color ColWarn    = new Color(1.00f, 0.75f, 0.30f, 1f);
        static readonly Color ColBad     = new Color(1.00f, 0.40f, 0.40f, 1f);
        static readonly Color ColMutedFg = new Color(0.55f, 0.55f, 0.62f, 1f);
        static readonly Color ColValue   = new Color(0.92f, 0.92f, 0.95f, 1f);
        static readonly Color ColDivider = new Color(1f,    1f,    1f,    0.06f);

        // ─── Runtime data (rempli par AdvBoundary chaque frame) ──────────────────

        [System.NonSerialized] public bool  IsInsideBoundary = true;
        [System.NonSerialized] public float DistanceToWall    = float.MaxValue;
        [System.NonSerialized] public float ProximityFade     = 0f;

        // ─── Styles ───────────────────────────────────────────────────────────────

        private bool      _stylesBuilt;
        private float     _lastScale = 1f;
        private GUIStyle  _labelStyle;
        private GUIStyle  _pillStyle;
        private GUIStyle  _statLabelStyle;
        private GUIStyle  _statValueStyle;
        private Texture2D _texBg, _texDivider, _texAccent;
        private Texture2D _texPillGood, _texPillBad, _texPillWarn;

        // ─────────────────────────────────────────────────────────────────────────

        private void OnEnable()
        {
            BackOfficeVaronia.OnMovieChanged += OnMovieChanged;
        }

        private void OnDisable()
        {
            BackOfficeVaronia.OnMovieChanged -= OnMovieChanged;
        }

        private void OnMovieChanged()
        {
            if (BackOfficeVaronia.Instance != null)
                show = BackOfficeVaronia.Instance.config.hideMode == 0;
        }

        private void OnDestroy()
        {
            if (_texBg)        Destroy(_texBg);
            if (_texDivider)   Destroy(_texDivider);
            if (_texAccent)    Destroy(_texAccent);
            if (_texPillGood)  Destroy(_texPillGood);
            if (_texPillBad)   Destroy(_texPillBad);
            if (_texPillWarn)  Destroy(_texPillWarn);
        }

        private void OnGUI()
        {
            if (!show) return;

            float scale = (Screen.height / 1080f) * scaleFactor;
            EnsureStyles(scale);

            Rect panel = GetPanelRect(scale);

            // ── Background ──
            GUI.DrawTexture(panel, _texBg);

            // ── Left accent bar (couleur selon statut) ──
            Color accent = IsInsideBoundary
                ? (ProximityFade > 0.01f ? ColWarn : ColGood)
                : ColBad;
            _texAccent.SetPixel(0, 0, accent);
            _texAccent.Apply();
            GUI.DrawTexture(new Rect(panel.x, panel.y, 3f * scale, panel.height), _texAccent);

            float HeaderH = 22f * scale;
            float RowH    = 20f * scale;
            float PadX    = 12f * scale;
            float PadY    =  4f * scale;

            // ── Header ──
            GUI.Label(
                new Rect(panel.x + PadX, panel.y + PadY, 120f * scale, HeaderH),
                "ADV BOUNDARY", _labelStyle
            );

            // Pill statut inside/outside
            string pillTxt = IsInsideBoundary ? "● INSIDE" : "● OUTSIDE";
            _pillStyle.normal.textColor  = IsInsideBoundary ? ColGood : ColBad;
            _pillStyle.normal.background = IsInsideBoundary ? _texPillGood : _texPillBad;
            GUI.Label(
                new Rect(panel.x + panel.width - 90f * scale, panel.y + PadY + 1f * scale, 86f * scale, HeaderH - 4f * scale),
                pillTxt, _pillStyle
            );

            // Divider 1
            float y = panel.y + HeaderH;
            GUI.DrawTexture(new Rect(panel.x + 8f * scale, y, panel.width - 16f * scale, 1f * scale), _texDivider);
            y += 2f * scale;

            // ── Rows ──
            y = DrawRow(panel, y, RowH, PadX, "DIST TO WALL",
                DistanceToWall >= 9999f ? "—" : $"{DistanceToWall:F2} m",
                DistanceToWall < 0.5f ? ColBad : DistanceToWall < 2f ? ColWarn : ColGood);

            DrawRow(panel, y, RowH, PadX, "PROXIMITY",
                $"{ProximityFade * 100f:F0} %",
                ProximityFade > 0.8f ? ColBad : ProximityFade > 0.3f ? ColWarn : ColGood);
        }

        // ─── Row helper ───────────────────────────────────────────────────────────

        private float DrawRow(Rect panel, float y, float rowH, float padX,
                              string label, string value, Color valueColor)
        {
            float halfH = rowH * 0.48f;
            float labelW = panel.width * 0.5f - padX;
            float valueX = panel.x + panel.width * 0.5f;
            float valueW = panel.width * 0.5f - padX;

            GUI.Label(new Rect(panel.x + padX, y + 1f, labelW, halfH * 2f), label, _statLabelStyle);

            _statValueStyle.normal.textColor = valueColor;
            GUI.Label(new Rect(valueX, y + 1f, valueW, halfH * 2f), value, _statValueStyle);

            return y + rowH;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private Rect GetPanelRect(float scale)
        {
            float w = size.x * scale, h = size.y * scale;
            float mx = margin.x * scale, my = margin.y * scale;
            float x, y;
            switch (corner)
            {
                case DisplayCorner.TopLeft:
                    x = mx; y = my; break;
                case DisplayCorner.TopRight:
                    x = Screen.width - w - mx; y = my; break;
                case DisplayCorner.BottomLeft:
                    x = mx; y = Screen.height - h - my; break;
                default:
                    x = Screen.width - w - mx; y = Screen.height - h - my; break;
            }
            return new Rect(x, y, w, h);
        }

        private void EnsureStyles(float scale)
        {
            if (_stylesBuilt && Mathf.Approximately(scale, _lastScale)) return;
            _stylesBuilt = true;
            _lastScale   = scale;

            if (_texBg == null)      _texBg      = MakeTex(ColBg);
            if (_texDivider == null) _texDivider = MakeTex(ColDivider);
            if (_texAccent == null)  _texAccent  = MakeTex(ColGood);
            if (_texPillGood == null) _texPillGood = MakeTex(new Color(ColGood.r, ColGood.g, ColGood.b, 0.15f));
            if (_texPillBad == null)  _texPillBad  = MakeTex(new Color(ColBad.r,  ColBad.g,  ColBad.b,  0.15f));
            if (_texPillWarn == null) _texPillWarn = MakeTex(new Color(ColWarn.r, ColWarn.g, ColWarn.b, 0.15f));

            _labelStyle = new GUIStyle
            {
                fontSize  = Mathf.RoundToInt(9 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = ColMutedFg },
            };

            _pillStyle = new GUIStyle
            {
                fontSize  = Mathf.RoundToInt(9 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal    = { textColor = ColGood, background = _texPillGood },
                padding   = new RectOffset(Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(2 * scale), Mathf.RoundToInt(2 * scale)),
            };

            _statLabelStyle = new GUIStyle
            {
                fontSize  = Mathf.RoundToInt(8 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal    = { textColor = ColMutedFg },
            };

            _statValueStyle = new GUIStyle
            {
                fontSize  = Mathf.RoundToInt(10 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight,
                normal    = { textColor = ColValue },
            };
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, col);
            t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }
}
