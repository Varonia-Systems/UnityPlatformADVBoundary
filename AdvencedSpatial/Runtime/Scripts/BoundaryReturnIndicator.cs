using UnityEngine;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Quand le joueur est sorti de la boundary, affiche un repère « rentrez par ici » :
    ///  - un anneau au sol posé sur le point de rentrée le plus proche,
    ///  - une flèche flottante (face caméra) qui pointe vers cet anneau, avec un léger va-et-vient.
    /// Le tout est généré procéduralement (aucun prefab requis), apparaît/disparaît en fondu,
    /// et reste caché pour les spectateurs ou si AlertLimit=false (IsNolimit).
    /// </summary>
    [DefaultExecutionOrder(111)]
    public class BoundaryReturnIndicator : MonoBehaviour
    {
        [Header("Apparence")]
        [Tooltip("Couleur du repère (anneau + flèche). Volontairement engageante plutôt qu'alarmante.")]
        [SerializeField] private Color color = new Color(0.25f, 0.85f, 1f, 1f);
        [Tooltip("Rayon de l'anneau au sol (mètres).")]
        [SerializeField] private float ringRadius = 0.55f;
        [Tooltip("Hauteur de la flèche au-dessus de l'anneau (mètres).")]
        [SerializeField] private float arrowHeight = 1.25f;
        [Tooltip("Taille de la flèche.")]
        [SerializeField] private float arrowScale = 0.6f;

        [Header("Animation")]
        [Tooltip("Amplitude du va-et-vient vertical de la flèche (mètres).")]
        [SerializeField] private float bobAmplitude = 0.12f;
        [SerializeField] private float bobSpeed     = 2.5f;
        [SerializeField] private float fadeSpeed    = 4f;

        // ─── Runtime ─────────────────────────────────────────────────────────────
        private AdvBoundary _boundary;
        private Camera      _cam;

        private Transform   _root;      // placé au point de rentrée
        private LineRenderer _ring;
        private Transform   _arrow;     // billboard
        private Material    _ringMat, _arrowMat;
        private MeshRenderer _arrowRenderer;

        private float   _alpha;         // 0 = caché, 1 = plein
        private bool    _built;
        private bool    _hasExitPoint;  // un point de sortie a-t-il été figé ?
        private Vector3 _exitPoint;     // position (monde) où le joueur est sorti

        private void Start()
        {
            _cam      = Camera.main;
            _boundary = FindObjectOfType<AdvBoundary>();
            Build();
            SetAlpha(0f);
            _built = true;
        }

        private void OnDestroy()
        {
            if (_root != null)     Destroy(_root.gameObject);
            if (_ringMat != null)  Destroy(_ringMat);
            if (_arrowMat != null) Destroy(_arrowMat);
        }

        private void Update()
        {
            if (!_built) return;

            if (_cam == null)      { _cam = Camera.main; if (_cam == null) return; }
            if (_boundary == null) { _boundary = FindObjectOfType<AdvBoundary>(); if (_boundary == null) return; }

            bool outside = _boundary.IsOutside && !_boundary.IsNolimit && !IsSpectator();

            // On capture le point de sortie UNE seule fois (là où le joueur a franchi le bord), puis on le
            // FIGE tant qu'il est dehors → l'anneau conseille de rentrer par où il est sorti, sans suivre.
            // Au retour à l'intérieur, on ré-arme pour recapturer à la prochaine sortie.
            if (outside)
            {
                if (!_hasExitPoint)
                    _hasExitPoint = AdvBoundary.TryGetReturnTarget(_cam.transform.position, out _exitPoint, out _);
            }
            else
            {
                _hasExitPoint = false;
            }

            // Fondu.
            float wanted = (outside && _hasExitPoint) ? 1f : 0f;
            _alpha = Mathf.MoveTowards(_alpha, wanted, fadeSpeed * Time.deltaTime);
            SetAlpha(_alpha);
            if (_alpha <= 0.001f) return;

            // Anneau FIXE sur le point de sortie (ne bouge pas quand le joueur se déplace).
            if (_hasExitPoint) _root.position = _exitPoint;

            // Flèche : va-et-vient vertical + billboard (face caméra, pointe vers le bas/l'anneau).
            float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            _arrow.localPosition = new Vector3(0f, arrowHeight + bob, 0f);

            Vector3 toCam = _cam.transform.position - _arrow.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 1e-4f)
                _arrow.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }

        // ─── Spectator gating (idem BoundaryOutOfBoundsUI) ────────────────────────
        private static bool IsSpectator()
        {
            var bo = BackOfficeVaronia.Instance;
            if (bo == null || bo.config == null) return false;
            var m = bo.config.DeviceMode;
            return m == DeviceMode.Server_Spectator || m == DeviceMode.Client_Spectator;
        }

        // ─── Construction procédurale ─────────────────────────────────────────────

        private void Build()
        {
            var shader = Shader.Find("Sprites/Default");

            // Même layer que la boundary (le layer n'est pas hérité par les enfants → à poser sur chaque GO),
            // pour être rendu par les mêmes caméras/culling masks que le grillage.
            int layer = AdvBoundarySettings.Layer;

            var rootGo = new GameObject("BoundaryReturnIndicator");
            rootGo.hideFlags = HideFlags.DontSave;
            rootGo.layer = layer;
            DontDestroyOnLoad(rootGo);
            _root = rootGo.transform;

            // Anneau au sol (cercle plat sur XZ).
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(_root, false);
            ringGo.layer = layer;
            _ring = ringGo.AddComponent<LineRenderer>();
            _ring.useWorldSpace     = false;
            _ring.loop              = true;
            _ring.widthMultiplier   = 0.05f;
            _ring.numCornerVertices = 4;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows    = false;
            _ringMat = new Material(shader) { color = color };
            _ring.material  = _ringMat;
            _ring.textureMode = LineTextureMode.Stretch;

            const int seg = 48;
            _ring.positionCount = seg;
            for (int i = 0; i < seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(a) * ringRadius, 0.02f, Mathf.Sin(a) * ringRadius));
            }

            // Flèche billboard (mesh plat pointant vers le bas).
            var arrowGo = new GameObject("Arrow");
            arrowGo.transform.SetParent(_root, false);
            arrowGo.layer = layer;
            arrowGo.transform.localPosition = new Vector3(0f, arrowHeight, 0f);
            arrowGo.transform.localScale    = Vector3.one * arrowScale;
            var mf = arrowGo.AddComponent<MeshFilter>();
            _arrowRenderer = arrowGo.AddComponent<MeshRenderer>();
            _arrowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _arrowRenderer.receiveShadows    = false;
            mf.mesh = BuildArrowMesh();
            _arrowMat = new Material(shader) { color = color };
            _arrowRenderer.material = _arrowMat;
            _arrow = arrowGo.transform;
        }

        // Flèche pleine pointant vers -Y (bas), dans le plan local XY, double-face.
        private static Mesh BuildArrowMesh()
        {
            var verts = new Vector3[]
            {
                new Vector3(-0.10f, 0.55f, 0f), // 0 stem
                new Vector3( 0.10f, 0.55f, 0f), // 1
                new Vector3( 0.10f, 0.10f, 0f), // 2
                new Vector3(-0.10f, 0.10f, 0f), // 3
                new Vector3(-0.30f, 0.15f, 0f), // 4 head
                new Vector3( 0.30f, 0.15f, 0f), // 5
                new Vector3( 0.00f,-0.45f, 0f), // 6 tip
            };
            var tris = new int[]
            {
                0, 1, 2,  0, 2, 3,  4, 5, 6,   // front
                0, 2, 1,  0, 3, 2,  4, 6, 5,   // back
            };
            var m = new Mesh { name = "ReturnArrow" };
            m.vertices  = verts;
            m.triangles = tris;
            m.RecalculateBounds();
            return m;
        }

        private void SetAlpha(float a)
        {
            var c = color; c.a *= a;
            if (_ringMat != null)  _ringMat.color  = c;
            if (_arrowMat != null) _arrowMat.color = c;
            if (_ring != null)
            {
                _ring.startColor = _ring.endColor = c;   // le LineRenderer se colore par ses vertex colors
                _ring.enabled    = a > 0.001f;
            }
            if (_arrowRenderer != null) _arrowRenderer.enabled = a > 0.001f;
        }
    }
}
