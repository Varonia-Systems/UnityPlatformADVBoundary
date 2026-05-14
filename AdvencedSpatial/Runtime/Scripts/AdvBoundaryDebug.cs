// AdvBoundaryDebug — overlay debug pour AdvBoundary.
//
// DEUX IMPLÉMENTATIONS COEXISTENT dans CE FICHIER UNIQUE (même MonoScript guid =
// les prefab refs survivent au toggle) :
//   • Par défaut → IMGUI
//   • Avec define VBO_UITOOLKIT_OVERLAYS → UI Toolkit (zero alloc)
//
// Toggle : Project Settings → Varonia Back Office → Debug Overlays Rendering.

using System.Collections.Generic;
using UnityEngine;

#if VBO_UITOOLKIT_OVERLAYS
using UnityEngine.UIElements;
#endif

namespace VaroniaBackOffice
{
    public class AdvBoundaryDebug : MonoBehaviour
    {
        // ─── Config (partagée) ──────────────────────────────────────────────────

        public enum DisplayCorner { TopLeft, TopRight, BottomLeft, BottomRight }

        [Header("Display")]
        [SerializeField] private DisplayCorner corner = DisplayCorner.BottomRight;
        [SerializeField] private Vector2       margin = new Vector2(12f, 12f);
        [SerializeField] private Vector2       size   = new Vector2(280f, 100f);
        [SerializeField] private bool          show   = true;

        [Header("UI Scale")]
        public float scaleFactor = 1f;

        // ─── Palette partagée ───────────────────────────────────────────────────
        static readonly Color ColBg      = new Color(0.11f, 0.11f, 0.14f, 0.92f);
        static readonly Color ColGood    = new Color(0.30f, 0.85f, 0.65f, 1f);
        static readonly Color ColWarn    = new Color(1.00f, 0.75f, 0.30f, 1f);
        static readonly Color ColBad     = new Color(1.00f, 0.40f, 0.40f, 1f);
        static readonly Color ColMutedFg = new Color(0.55f, 0.55f, 0.62f, 1f);
        static readonly Color ColValue   = new Color(0.92f, 0.92f, 0.95f, 1f);
        static readonly Color ColDivider = new Color(1f, 1f, 1f, 0.06f);

        // ─── Runtime data (rempli par AdvBoundary chaque frame) ────────────────
        [System.NonSerialized] public bool  IsInsideBoundary = true;
        [System.NonSerialized] public float DistanceToWall   = float.MaxValue;
        [System.NonSerialized] public float ProximityFade    = 0f;

        // ─── Lifecycle ─────────────────────────────────────────────────────────

        private void OnEnable()
        {
            BackOfficeVaronia.OnMovieChanged += OnMovieChanged;
#if VBO_UITOOLKIT_OVERLAYS
            BuildOverlay_UITK();
#endif
        }

        private void OnDisable()
        {
            BackOfficeVaronia.OnMovieChanged -= OnMovieChanged;
#if VBO_UITOOLKIT_OVERLAYS
            if (_panelSettings != null) Destroy(_panelSettings);
            if (_doc != null && _doc.gameObject != null) Destroy(_doc.gameObject);
#endif
        }

#if !VBO_UITOOLKIT_OVERLAYS
        private void OnDestroy()
        {
            if (_texBg)        Destroy(_texBg);
            if (_texDivider)   Destroy(_texDivider);
            if (_texAccent)    Destroy(_texAccent);
            if (_texPillGood)  Destroy(_texPillGood);
            if (_texPillBad)   Destroy(_texPillBad);
            if (_texPillWarn)  Destroy(_texPillWarn);
        }
#endif

        private void OnMovieChanged()
        {
            if (BackOfficeVaronia.Instance != null)
                show = BackOfficeVaronia.Instance.config.HideMode == 0;

#if VBO_UITOOLKIT_OVERLAYS
            if (_panel != null)
                _panel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
#endif
        }

#if VBO_UITOOLKIT_OVERLAYS
        // ════════════════════════════════════════════════════════════════════════
        //  UI Toolkit
        // ════════════════════════════════════════════════════════════════════════

        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private VisualElement _root, _panel, _accent, _pill;
        private Label _pillLabel, _distValue, _proxValue;
        private Color _lastAccent;
        private bool _lastInside;

        // Cache pour limiter les allocs de string : on ne rebuild les .text que
        // quand la valeur arrondie à la précision affichée change réellement.
        private float _lastDistShown = float.NaN;
        private int _lastProxShownInt = int.MinValue;
        private Color _lastDistColor, _lastProxColor;

        private void Update()
        {
            if (_panel == null) return;

            // Accent bar (gauche)
            Color accent = IsInsideBoundary
                ? (ProximityFade > 0.01f ? ColWarn : ColGood)
                : ColBad;
            if (accent != _lastAccent)
            {
                _lastAccent = accent;
                _accent.style.backgroundColor = accent;
            }

            // Pill INSIDE / OUTSIDE
            if (IsInsideBoundary != _lastInside || _pillLabel.text.Length == 0)
            {
                _lastInside = IsInsideBoundary;
                _pillLabel.text = IsInsideBoundary ? "● INSIDE" : "● OUTSIDE";
                _pillLabel.style.color = IsInsideBoundary ? ColGood : ColBad;
                _pill.style.backgroundColor = new Color(
                    IsInsideBoundary ? ColGood.r : ColBad.r,
                    IsInsideBoundary ? ColGood.g : ColBad.g,
                    IsInsideBoundary ? ColGood.b : ColBad.b,
                    0.15f);
            }

            // Distance to wall — round à 0.01m, rebuild seulement si changement visible
            float distRounded = DistanceToWall >= 9999f ? -1f : Mathf.Round(DistanceToWall * 100f) / 100f;
            if (distRounded != _lastDistShown)
            {
                _lastDistShown = distRounded;
                _distValue.text = distRounded < 0f ? "—" : distRounded.ToString("F2") + " m";
            }
            Color distColor = DistanceToWall < 0.5f ? ColBad : DistanceToWall < 2f ? ColWarn : ColGood;
            if (distColor != _lastDistColor)
            {
                _lastDistColor = distColor;
                _distValue.style.color = distColor;
            }

            // Proximity — round à 1%, rebuild seulement si changement
            int proxInt = Mathf.RoundToInt(ProximityFade * 100f);
            if (proxInt != _lastProxShownInt)
            {
                _lastProxShownInt = proxInt;
                _proxValue.text = proxInt + " %";
            }
            Color proxColor = ProximityFade > 0.8f ? ColBad : ProximityFade > 0.3f ? ColWarn : ColGood;
            if (proxColor != _lastProxColor)
            {
                _lastProxColor = proxColor;
                _proxValue.style.color = proxColor;
            }
        }

        private void BuildOverlay_UITK()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            _panelSettings.sortingOrder = 100;

            var uiGo = new GameObject("[AdvBoundaryDebugUI]");
            uiGo.transform.SetParent(transform, false);
            _doc = uiGo.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            _root = _doc.rootVisualElement;
            _root.style.flexGrow = 1;
            _root.pickingMode = PickingMode.Ignore;
            if (font != null) _root.style.unityFont = font;

            _panel = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    width = size.x,
                    minHeight = size.y,
                    flexDirection = FlexDirection.Column,
                    backgroundColor = ColBg,
                    paddingLeft = 12, paddingRight = 12, paddingTop = 6, paddingBottom = 6,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                }
            };
            PositionPanel_UITK();
            _root.Add(_panel);

            // Accent bar
            _accent = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    position = Position.Absolute,
                    left = 0, top = 0, bottom = 0, width = 3,
                    backgroundColor = ColGood,
                }
            };
            _panel.Add(_accent);

            // Header row : title + pill
            var headerRow = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { flexDirection = FlexDirection.Row, height = 22, alignItems = Align.Center }
            };
            _panel.Add(headerRow);

            var title = MakeLabel("ADV BOUNDARY", 9, FontStyle.Bold, ColMutedFg, TextAnchor.MiddleLeft);
            title.style.flexGrow = 1;
            headerRow.Add(title);

            _pill = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 86, height = 18,
                    paddingLeft = 4, paddingRight = 4,
                    backgroundColor = new Color(ColGood.r, ColGood.g, ColGood.b, 0.15f),
                    alignItems = Align.Center, justifyContent = Justify.Center,
                }
            };
            _pillLabel = MakeLabel("", 9, FontStyle.Bold, ColGood, TextAnchor.MiddleCenter);
            _pill.Add(_pillLabel);
            headerRow.Add(_pill);

            // Divider
            var div = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { height = 1, backgroundColor = ColDivider, marginTop = 2, marginBottom = 4 }
            };
            _panel.Add(div);

            // Rows
            _panel.Add(MakeRow("DIST TO WALL", out _distValue));
            _panel.Add(MakeRow("PROXIMITY",    out _proxValue));
        }

        private VisualElement MakeRow(string labelText, out Label valueLabel)
        {
            var row = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { flexDirection = FlexDirection.Row, height = 20, alignItems = Align.Center }
            };
            var l = MakeLabel(labelText, 8, FontStyle.Bold, ColMutedFg, TextAnchor.MiddleLeft);
            l.style.flexGrow = 1;
            row.Add(l);

            valueLabel = MakeLabel("—", 10, FontStyle.Bold, ColValue, TextAnchor.MiddleRight);
            valueLabel.style.minWidth = 80;
            row.Add(valueLabel);
            return row;
        }

        private static Label MakeLabel(string text, int fontSize, FontStyle fStyle, Color color, TextAnchor anchor)
        {
            return new Label(text)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    fontSize = fontSize, color = color,
                    unityFontStyleAndWeight = fStyle, unityTextAlign = anchor,
                }
            };
        }

        private void PositionPanel_UITK()
        {
            if (_panel == null) return;
            float mx = margin.x, my = margin.y;
            _panel.style.width = size.x * scaleFactor;
            switch (corner)
            {
                case DisplayCorner.TopLeft:
                    _panel.style.left = mx; _panel.style.top = my;
                    _panel.style.right = StyleKeyword.Auto; _panel.style.bottom = StyleKeyword.Auto;
                    break;
                case DisplayCorner.TopRight:
                    _panel.style.right = mx; _panel.style.top = my;
                    _panel.style.left = StyleKeyword.Auto; _panel.style.bottom = StyleKeyword.Auto;
                    break;
                case DisplayCorner.BottomLeft:
                    _panel.style.left = mx; _panel.style.bottom = my;
                    _panel.style.right = StyleKeyword.Auto; _panel.style.top = StyleKeyword.Auto;
                    break;
                default:
                    _panel.style.right = mx; _panel.style.bottom = my;
                    _panel.style.left = StyleKeyword.Auto; _panel.style.top = StyleKeyword.Auto;
                    break;
            }
        }
#endif // VBO_UITOOLKIT_OVERLAYS

#if !VBO_UITOOLKIT_OVERLAYS
        // ════════════════════════════════════════════════════════════════════════
        //  IMGUI (fallback)
        // ════════════════════════════════════════════════════════════════════════

        private bool      _stylesBuilt;
        private float     _lastScale = 1f;
        private GUIStyle  _labelStyle, _pillStyle, _statLabelStyle, _statValueStyle;
        private Texture2D _texBg, _texDivider, _texAccent;
        private Texture2D _texPillGood, _texPillBad, _texPillWarn;

        private void OnGUI()
        {
            if (!show) return;
            if (Event.current.type != EventType.Repaint) return;

            float scale = (Screen.height / 1080f) * scaleFactor;
            EnsureStyles(scale);

            Rect panel = GetPanelRect(scale);

            GUI.DrawTexture(panel, _texBg);

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

            GUI.Label(new Rect(panel.x + PadX, panel.y + PadY, 120f * scale, HeaderH),
                "ADV BOUNDARY", _labelStyle);

            string pillTxt = IsInsideBoundary ? "● INSIDE" : "● OUTSIDE";
            _pillStyle.normal.textColor  = IsInsideBoundary ? ColGood : ColBad;
            _pillStyle.normal.background = IsInsideBoundary ? _texPillGood : _texPillBad;
            GUI.Label(
                new Rect(panel.x + panel.width - 90f * scale, panel.y + PadY + 1f * scale, 86f * scale, HeaderH - 4f * scale),
                pillTxt, _pillStyle
            );

            float y = panel.y + HeaderH;
            GUI.DrawTexture(new Rect(panel.x + 8f * scale, y, panel.width - 16f * scale, 1f * scale), _texDivider);
            y += 2f * scale;

            y = DrawRow(panel, y, RowH, PadX, "DIST TO WALL",
                DistanceToWall >= 9999f ? "—" : $"{DistanceToWall:F2} m",
                DistanceToWall < 0.5f ? ColBad : DistanceToWall < 2f ? ColWarn : ColGood);

            DrawRow(panel, y, RowH, PadX, "PROXIMITY",
                $"{ProximityFade * 100f:F0} %",
                ProximityFade > 0.8f ? ColBad : ProximityFade > 0.3f ? ColWarn : ColGood);
        }

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

        private Rect GetPanelRect(float scale)
        {
            float w = size.x * scale, h = size.y * scale;
            float mx = margin.x * scale, my = margin.y * scale;
            float x, y;
            switch (corner)
            {
                case DisplayCorner.TopLeft:     x = mx;                          y = my;                          break;
                case DisplayCorner.TopRight:    x = Screen.width - w - mx;       y = my;                          break;
                case DisplayCorner.BottomLeft:  x = mx;                          y = Screen.height - h - my;      break;
                default:                        x = Screen.width - w - mx;       y = Screen.height - h - my;      break;
            }
            return new Rect(x, y, w, h);
        }

        private void EnsureStyles(float scale)
        {
            if (_stylesBuilt && Mathf.Approximately(scale, _lastScale)) return;
            _stylesBuilt = true;
            _lastScale = scale;

            if (_texBg == null)       _texBg       = MakeTex(ColBg);
            if (_texDivider == null)  _texDivider  = MakeTex(ColDivider);
            if (_texAccent == null)   _texAccent   = MakeTex(ColGood);
            if (_texPillGood == null) _texPillGood = MakeTex(new Color(ColGood.r, ColGood.g, ColGood.b, 0.15f));
            if (_texPillBad == null)  _texPillBad  = MakeTex(new Color(ColBad.r,  ColBad.g,  ColBad.b,  0.15f));
            if (_texPillWarn == null) _texPillWarn = MakeTex(new Color(ColWarn.r, ColWarn.g, ColWarn.b, 0.15f));

            _labelStyle = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(9 * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, normal = { textColor = ColMutedFg },
            };
            _pillStyle = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(9 * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = ColGood, background = _texPillGood },
                padding = new RectOffset(Mathf.RoundToInt(4 * scale), Mathf.RoundToInt(4 * scale),
                                          Mathf.RoundToInt(2 * scale), Mathf.RoundToInt(2 * scale)),
            };
            _statLabelStyle = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(8 * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft, normal = { textColor = ColMutedFg },
            };
            _statValueStyle = new GUIStyle
            {
                fontSize = Mathf.RoundToInt(10 * scale), fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight, normal = { textColor = ColValue },
            };
        }

        private static Texture2D MakeTex(Color col)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, col); t.Apply();
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
#endif // !VBO_UITOOLKIT_OVERLAYS
    }
}
