using UnityEngine;
using UnityEngine.UI;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Affiche un message d'avertissement en World Space devant la caméra
    /// quand le joueur sort des limites de la boundary principale.
    /// La langue est lue depuis BackOfficeVaronia.Instance.config.Language (FR/ES/EN).
    /// </summary>
    [DefaultExecutionOrder(110)]
    public class BoundaryOutOfBoundsUI : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────

        [Header("Canvas World Space")]
        [Tooltip("Distance devant la caméra (en mètres).")]
        [SerializeField] private float canvasDistance   = 1.5f;
        [Tooltip("Largeur du canvas en unités monde.")]
        [SerializeField] private float canvasWorldWidth = 3.6f;
        [Tooltip("Offset vertical par rapport au centre de la caméra.")]
        [SerializeField] private float verticalOffset   = 0f;
        [Tooltip("Vitesse de suivi de la caméra (lerp). 0 = instantané.")]
        [SerializeField] private float followSpeed      = 3f;
        [Tooltip("Si true, le canvas s'affiche toujours au-dessus de la géométrie 3D.")]
        [SerializeField] private bool  alwaysOnTop      = true;

        [Header("Apparence")]
        [Tooltip("Vitesse de clignotement du titre (0 = pas de clignotement).")]
        [SerializeField] private float blinkSpeed = 2.5f;

        // ─── Localization ─────────────────────────────────────────────────────────

        // FR
        private const string TitleFR    = "STOP !";
        private const string SubtitleFR = "Retournez dans la zone de jeu immédiatement";
        private const string WarnFR     = "⚠  ZONE INTERDITE  ⚠";

        // ES
        private const string TitleES    = "¡ALTO !";
        private const string SubtitleES = "Regrese a la zona de juego inmediatamente";
        private const string WarnES     = "⚠  ZONA PROHIBIDA  ⚠";

        // EN
        private const string TitleEN    = "STOP !";
        private const string SubtitleEN = "Return to the play area immediately";
        private const string WarnEN     = "⚠  OUT OF BOUNDS  ⚠";

        // ─── Colors ───────────────────────────────────────────────────────────────

        static readonly Color ColBlurBg     = new Color(0.00f, 0.00f, 0.05f, 0.87f); // bleu nuit semi-transparent
        static readonly Color ColPanelBg    = new Color(0.08f, 0.02f, 0.02f, 0.97f); // rouge très sombre
        static readonly Color ColAccent     = new Color(1.00f, 0.18f, 0.18f, 1.00f); // rouge vif
        static readonly Color ColTitle      = new Color(1.00f, 0.08f, 0.08f, 1.00f); // rouge vif
        static readonly Color ColSubtitle   = new Color(1.00f, 0.80f, 0.80f, 1.00f); // blanc rosé
        static readonly Color ColWarnBadge  = new Color(1.00f, 0.85f, 0.00f, 1.00f); // jaune alerte
        static readonly Color ColWarnBg     = new Color(0.60f, 0.30f, 0.00f, 0.85f); // fond badge

        // ─── Runtime ──────────────────────────────────────────────────────────────

        private Canvas    _canvas;
        private Transform _canvasTransform;
        private Camera    _cam;
        private bool      _initialized;

        private AdvBoundary _boundary;

        private Text  _titleLabel;
        private Text  _subtitleLabel;
        private Text  _warnLabel;
        private Image _blurOverlay;
        private Material _alwaysOnTopMat;

        private string _currentLang = "EN";

        // ─── Fade ─────────────────────────────────────────────────────────────────
        private const float FadeDelay    = 0.25f;
        private const float FadeInSpeed  = 3.0f;
        private const float FadeOutSpeed = 5.0f;

        private CanvasGroup _canvasGroup;
        private float       _outsideTimer = 0f;  // temps passé dehors

        // ─────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            _cam      = Camera.main;
            _boundary = FindObjectOfType<AdvBoundary>();

            RefreshLanguage();
            BuildCanvas();
            _initialized = true;

            _canvasGroup = _canvasTransform.gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha          = 0f;
            _canvasGroup.interactable   = false;
            _canvasGroup.blocksRaycasts = false;
            _canvasTransform.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (!_initialized) return;

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            if (_boundary == null)
                _boundary = FindObjectOfType<AdvBoundary>();

            bool outside = _boundary != null && _boundary.IsOutside && !_boundary.IsNolimit;

            // ── Gestion du timer de délai et du fade ──────────────────────────
            if (outside)
            {
                _outsideTimer += Time.deltaTime;
                if (_outsideTimer >= FadeDelay)
                    _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 1f, FadeInSpeed * Time.deltaTime);
            }
            else
            {
                _outsideTimer = 0f;
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, 0f, FadeOutSpeed * Time.deltaTime);
            }

            if (_canvasGroup.alpha <= 0f) return;

            FollowCamera();

            // Clignotement du titre
            if (blinkSpeed > 0f)
            {
                float alpha = Mathf.Abs(Mathf.Sin(Time.time * blinkSpeed * Mathf.PI));
                _titleLabel.color = new Color(ColTitle.r, ColTitle.g, ColTitle.b, Mathf.Lerp(0.3f, 1f, alpha));
            }

            // Pulsation de la barre d'accent (badge warn)
            float pulse = (Mathf.Sin(Time.time * 4f) + 1f) * 0.5f;
            _warnLabel.color = Color.Lerp(ColWarnBadge, Color.white, pulse * 0.4f);
        }

        // ─── Language ─────────────────────────────────────────────────────────────

        private void RefreshLanguage()
        {
            string lang = "EN";
            if (BackOfficeVaronia.Instance != null && BackOfficeVaronia.Instance.config != null)
            {
                // Le ?? est OK en C# 7.3
                lang = BackOfficeVaronia.Instance.config.Language ?? "EN";
            }

            // Remplacement de la Switch Expression par un Switch Statement
            switch (lang.ToUpperInvariant())
            {
                case "FR":
                    _currentLang = "FR";
                    break;
                case "ES":
                    _currentLang = "ES";
                    break;
                default:
                    _currentLang = "EN";
                    break;
            }
        }

        private (string title, string subtitle, string warn) GetTexts()
        {
            // Remplacement de la Switch Expression par un Switch Statement
            // Note : Les tuples (string, string, string) sont supportés en 2019.4 (C# 7.0+)
            switch (_currentLang)
            {
                case "FR":
                    return (TitleFR, SubtitleFR, WarnFR);
                case "ES":
                    return (TitleES, SubtitleES, WarnES);
                default:
                    return (TitleEN, SubtitleEN, WarnEN);
            }
        }

        // ─── Canvas build ─────────────────────────────────────────────────────────

        private void BuildCanvas()
        {
            // Résolution interne du canvas
            const int resX = 1600;
            const int resY = 900;

            var go = new GameObject("BoundaryOutOfBoundsCanvas");
            go.hideFlags = HideFlags.DontSave;

            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;

            go.AddComponent<GraphicRaycaster>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(resX, resY);

            float scale = canvasWorldWidth / resX;
            go.transform.localScale = new Vector3(scale, scale, scale);

            _canvasTransform = go.transform;

            if (_cam != null) SnapToCamera();

            // ── Fond overlay plein écran ──────────────────────────────────────
            _blurOverlay = MakeImage(go.transform, new Rect(0, 0, 128000, 128000), ColBlurBg, "BlurOverlay");

            // ── Panneau central ───────────────────────────────────────────────────
            float panW = resX * 0.88f;
            float panH = resY * 0.80f;
            float panX = (resX - panW) * 0.5f;
            float panY = (resY - panH) * 0.5f;

            var panel = MakeImage(go.transform, new Rect(panX, panY, panW, panH), ColPanelBg, "Panel");

            // Barre d'accent gauche (8px)
            MakeImage(go.transform, new Rect(panX, panY, 8f, panH), ColAccent, "AccentLeft");
            // Barre d'accent droite (8px)
            MakeImage(go.transform, new Rect(panX + panW - 8f, panY, 8f, panH), ColAccent, "AccentRight");
            // Barre d'accent haut (4px)
            MakeImage(go.transform, new Rect(panX, panY + panH - 4f, panW, 4f), ColAccent, "AccentTop");
            // Barre d'accent bas (4px)
            MakeImage(go.transform, new Rect(panX, panY, panW, 4f), ColAccent, "AccentBottom");

            // ── Badge avertissement (haut du panneau) ─────────────────────────────
            float badgeH = panH * 0.22f;
            float badgeY = panY + panH - badgeH - 18f;
            MakeImage(go.transform, new Rect(panX + 8f, badgeY, panW - 16f, badgeH), ColWarnBg, "WarnBg");

            var (title, subtitle, warn) = GetTexts();

            _warnLabel = MakeLabel(go.transform,
                new Rect(panX + 8f, badgeY, panW - 16f, badgeH),
                warn, 55, ColWarnBadge, TextAnchor.MiddleCenter);
            _warnLabel.fontStyle = FontStyle.Bold;

            // ── Titre principal ───────────────────────────────────────────────────
            float titleH = panH * 0.40f;
            float titleY = panY + panH * 0.28f;
            _titleLabel = MakeLabel(go.transform,
                new Rect(panX + 20f, titleY, panW - 40f, titleH),
                title, 420, ColTitle, TextAnchor.MiddleCenter);
            _titleLabel.fontStyle = FontStyle.Bold;

            // ── Sous-titre ────────────────────────────────────────────────────────
            float subH = panH * 0.20f;
            float subY = panY + 18f;
            _subtitleLabel = MakeLabel(go.transform,
                new Rect(panX + 30f, subY, panW - 60f, subH),
                subtitle, 62, ColSubtitle, TextAnchor.MiddleCenter);

            if (alwaysOnTop) ApplyAlwaysOnTop();
        }

        // ─── Always On Top ────────────────────────────────────────────────────────

        private void ApplyAlwaysOnTop()
        {
            if (_alwaysOnTopMat == null)
            {
                var shader = Shader.Find("UI/AlwaysOnTop");
                if (shader == null) shader = Shader.Find("UI/Default");
                if (shader == null) return;
                _alwaysOnTopMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            foreach (var graphic in _canvas.GetComponentsInChildren<Graphic>(true))
                graphic.material = _alwaysOnTopMat;
        }

        // ─── Camera follow ────────────────────────────────────────────────────────

        private void FollowCamera()
        {
            Vector3 targetPos = _cam.transform.position
                + _cam.transform.forward * canvasDistance
                + Vector3.up * verticalOffset;

            _canvasTransform.position = Vector3.Lerp(
                _canvasTransform.position, targetPos,
                followSpeed > 0f ? followSpeed * Time.deltaTime : 1f);

            Quaternion targetRot = Quaternion.LookRotation(
                _canvasTransform.position - _cam.transform.position);
            _canvasTransform.rotation = Quaternion.Slerp(
                _canvasTransform.rotation, targetRot,
                followSpeed > 0f ? followSpeed * Time.deltaTime : 1f);
        }

        private void SnapToCamera()
        {
            _canvasTransform.position = _cam.transform.position
                + _cam.transform.forward * canvasDistance
                + Vector3.up * verticalOffset;
            _canvasTransform.rotation = Quaternion.LookRotation(
                _canvasTransform.position - _cam.transform.position);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static Image MakeImage(Transform parent, Rect r, Color col, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            // BlurOverlay utilise ancrage centré pour couvrir tout l'écran
            if (name == "BlurOverlay")
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta        = new Vector2(r.width, r.height);
            }
            else
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
                rt.pivot     = Vector2.zero;
                rt.anchoredPosition = new Vector2(r.x, r.y);
                rt.sizeDelta        = new Vector2(r.width, r.height);
            }
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        private static Text MakeLabel(Transform parent, Rect r, string txt, int fontSize, Color col, TextAnchor anchor)
        {
            var go = new GameObject("Label_" + txt);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.anchoredPosition = new Vector2(r.x, r.y);
            rt.sizeDelta        = new Vector2(r.width, r.height);
            var t = go.AddComponent<Text>();
            t.text           = txt;
            t.fontSize       = fontSize;
            t.color          = col;
            t.alignment      = anchor;
#if UNITY_2022_2_OR_NEWER
    t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            t.fontStyle      = FontStyle.Bold;
            t.resizeTextForBestFit = false;
            return t;
        }

        private void OnDestroy()
        {
            if (_canvas != null)
                Destroy(_canvas.gameObject);
            if (_alwaysOnTopMat != null)
                Destroy(_alwaysOnTopMat);
        }
    }
}
