using System.Collections.Generic;
using UnityEngine;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Boundary avancée style Meta Quest :
    ///  - Contours au sol permanents (LineRenderer coloré)
    ///  - Rideau de mur par segment avec fade horizontal dynamique centré sur la caméra
    ///  - Son d'alerte avec volume/pitch qui montent à l'approche et au dépassement
    ///  - Shaders VR-compatibles (stereo instancing)
    /// </summary>
    public class AdvBoundary : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────────

        [Header("Geometry")]
        [Tooltip("Décalage Y minimal pour éviter le z-fighting.")]
        [SerializeField] private float groundOffset = 0.005f;
        [Tooltip("Épaisseur des lignes de contour au sol.")]
        [SerializeField] private float groundLineWidth = 0.05f;
        [Tooltip("Hauteur des murs en mètres.")]
        [SerializeField] private float wallHeight = 2.5f;

        [Header("Proximity Wall")]
        [Tooltip("Distance à partir de laquelle le rideau commence à apparaître.")]
        [SerializeField] private float proximityDistance = 3.0f;
        [Tooltip("Distance à laquelle le rideau est à pleine intensité.")]
        [SerializeField] private float fullIntensityDistance = 0.5f;
        [Tooltip("Vitesse de transition de l'effet d'approche.")]
        [SerializeField] private float proximityLerpSpeed = 6.0f;
        [Tooltip("Rayon du fade horizontal en UV (0.1 = très localisé, 0.5 = demi-segment).")]
        [SerializeField] private float wallFadeRadius = 0.25f;
        [Tooltip("Une fois le joueur sorti de la boundary, le grillage (murs) reste masqué en permanence, " +
                 "pour ne pas laisser croire que la zone extérieure est interdite.")]
        [SerializeField] private bool hideWallsOnceExited = true;
        [Tooltip("Affiche automatiquement le repère 'rentrez par ici' (anneau au sol + flèche) quand le " +
                 "joueur est sorti. Le composant BoundaryReturnIndicator est ajouté tout seul si absent.")]
        [SerializeField] private bool showReturnIndicator = true;

        [Header("Wall Shader Params")]
        [SerializeField] private float wallPulseSpeed     = 1.5f;
        [SerializeField] private float wallPulseIntensity = 0.3f;
        [Tooltip("Longueur d'un bras de croix en mètres.")]
        [SerializeField] private float crossSize             = 0.012f;
        [Tooltip("Espace vide entre les croix en mètres.")]
        [SerializeField] private float crossGap              = 0.22f;
        [Tooltip("Largeur des bras des croix au repos (loin de la boundary).")]
        [SerializeField] private float crossThicknessMin     = 0.15f;
        [Tooltip("Largeur des bras des croix à pleine proximité (collé au mur).")]
        [SerializeField] private float crossThicknessMax     = 0.25f;
        [Tooltip("Netteté des bords des croix (plus élevé = bords plus nets).")]
        [SerializeField] private float crossSharpness        = 200.0f;

        [Header("Ground Line Params")]
        [Tooltip("Alpha de base des lignes au sol (0-1).")]
        [SerializeField] private float groundBaseAlpha      = 0.8f;
        [SerializeField] private float groundPulseSpeed     = 1.0f;
        [SerializeField] private float groundPulseIntensity = 0.15f;
        [Tooltip("Épaisseur de la ligne au sol loin de la boundary.")]
        [SerializeField] private float groundWidthMin       = 0.03f;
        [Tooltip("Épaisseur de la ligne au sol quand on est collé à la boundary.")]
        [SerializeField] private float groundWidthMax       = 0.12f;
        [Tooltip("Intensité (alpha boost) minimale de la ligne au sol loin de la boundary.")]
        [SerializeField] private float groundIntensityMin   = 0.5f;
        [Tooltip("Intensité (alpha boost) maximale de la ligne au sol quand on est collé.")]
        [SerializeField] private float groundIntensityMax   = 1.0f;

        [Header("Player")]
        [Tooltip("Transform du joueur/caméra. Auto-détecté si vide.")]
        [SerializeField] private Transform playerTarget;

        /// <summary>
        /// Force la désactivation de l'alerte visuelle (murs) quand on est hors-zone.
        /// Utilisé principalement en éditeur pour travailler sans être gêné par les murs rouges.
        /// </summary>
        public static bool ForceDisableBoundaryWarning { get; set; } = false;

        [Header("Proximity Sound")]
        [Tooltip("Clip audio joué à l'approche. Laisser vide pour générer un bip procédural.")]
        [SerializeField] private AudioClip boundaryAlertClip;
        [Tooltip("Distance à laquelle le son commence à monter (début du fondu). Doit être > 'Full Intensity Distance'.")]
        [SerializeField] private float soundStartDistance = 2.0f;
        [Tooltip("Pitch de base du son (loin de la boundary).")]
        [SerializeField] private float soundBasePitch = 0.8f;
        [Tooltip("Pitch maximum quand collé au mur ou en dehors.")]
        [SerializeField] private float soundMaxPitch = 1.4f;
        [Tooltip("Vitesse de lerp du volume/pitch sonore.")]
        [SerializeField] private float soundLerpSpeed = 8.0f;

        // "Beep à l'approche" : option GLOBALE lue depuis le spatial (NewSpatial.json / SpatialConfig),
        // pas depuis l'inspecteur. false = son uniquement hors zone ; true = bip aussi à l'approche.
        private bool _beepOnApproach;

        
        
        
        
        
        
        [SerializeField] private Shader groundShader;
        [SerializeField] private Shader wallShader;
        
        
        
        
        // ─── Private ──────────────────────────────────────────────────────────────

        private class WallSegment
        {
            public GameObject   GO;
            public MeshFilter   MF;
            public MeshRenderer Renderer;
            public Material     Mat;
            public Vector3      A;
            public Vector3      B;
            public float        SegmentLength;

            // Optional trigger collider on the curtain. Lives on its OWN
            // always-active GameObject (ColliderGO) so it keeps reporting triggers
            // even when the visual segment GO is toggled off for the proximity fade.
            // A thin BoxCollider (oriented along the segment) is used so it can be
            // a trigger (trigger MeshColliders must be convex → no flat quad).
            public GameObject  ColliderGO;
            public BoxCollider Collider;
        }

        private class BoundaryRenderable
        {
            public GameObject          GroundGO;
            public LineRenderer        GroundLine;
            public Material            GroundMat;
            public List<Vector3>       LocalPoints  = new List<Vector3>();
            public List<Vector3>       WorldPoints  = new List<Vector3>();
            public List<WallSegment>   WallSegments = new List<WallSegment>();
            public float               CurrentProximity;
            public bool                IsMain;
            public float               ProximityDistance;
            public bool                AlertLimit;
            public bool                HideLineFar;
            public float               CurrentFarFade; // 0 = hidden, 1 = visible (lerped)
        }

        private readonly List<BoundaryRenderable> _renderables = new List<BoundaryRenderable> ();

        // Instances de prefabs d'obstacles spawnés depuis le JSON (détruites au Clear).
        private readonly List<GameObject> _obstacleInstances = new List<GameObject>();

        /// <summary>True si le joueur est en dehors de la boundary principale.</summary>
        public bool IsOutside { get; private set; }

        /// <summary>True si AlertLimit = false sur toutes les boundaries (pas de son ni d'alerte UI).</summary>
        public bool IsNolimit { get; private set; }

        private bool             _movieMode;
        private int              _boundaryLayer;
        private AudioSource      _audioSource;
        private float            _currentSoundVolume;
        private float            _currentSoundPitch;
        private bool             _cameraFound;
        private AdvBoundaryDebug _debug;


        // Instance cachée : évite un FindObjectOfType par frame dans les accesseurs statiques.
        private static AdvBoundary _instance;
        private static AdvBoundary Instance =>
            _instance != null ? _instance : (_instance = FindObjectOfType<AdvBoundary>());

        private static readonly int ProximityFadeId = Shader.PropertyToID("_ProximityFade");
        private static readonly int ColorId         = Shader.PropertyToID("_Color");
        private static readonly int PulseSpeedId    = Shader.PropertyToID("_PulseSpeed");
        private static readonly int PulseIntId      = Shader.PropertyToID("_PulseIntensity");
        private static readonly int BaseAlphaId       = Shader.PropertyToID("_BaseAlpha");
        private static readonly int IntensityScaleId  = Shader.PropertyToID("_IntensityScale");
        private static readonly int FadeRadiusId    = Shader.PropertyToID("_FadeRadius");
        private static readonly int CamProjUId      = Shader.PropertyToID("_CamProjU");
        private static readonly int CamWorldYId     = Shader.PropertyToID("_CamWorldY");
        private static readonly int WallBottomYId   = Shader.PropertyToID("_WallBottomY");
        private static readonly int WallHeightId    = Shader.PropertyToID("_WallHeight");
        private static readonly int SegmentAId      = Shader.PropertyToID("_SegmentA");
        private static readonly int SegmentBId      = Shader.PropertyToID("_SegmentB");
        private static readonly int CamPosWorldId   = Shader.PropertyToID("_CamPosWorld");
        private static readonly int CrossSizeId      = Shader.PropertyToID("_CrossSize");
        private static readonly int CrossGapId        = Shader.PropertyToID("_CrossGap");
        private static readonly int CrossThicknessId  = Shader.PropertyToID("_CrossThickness");
        private static readonly int CrossThicknessMinId = Shader.PropertyToID("_CrossThicknessMin");
        private static readonly int CrossThicknessMaxId = Shader.PropertyToID("_CrossThicknessMax");
        private static readonly int CrossSharpnessId  = Shader.PropertyToID("_CrossSharpness");

        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _instance = this;
            SetupAudioSource();
            _debug = GetComponent<AdvBoundaryDebug>();
        }

        private Vector3 GetCameraWorldPosition()
        {
            if (playerTarget != null)
            {
                _cameraFound = true;
                return playerTarget.position;
            }

            var cam = Camera.main;
            if (cam != null)
            {
                playerTarget = cam.transform;
                _cameraFound = true;
                return cam.transform.position;
            }

            _cameraFound = false;
            return Vector3.zero;
        }

        private void Start()
        {
            // Repère 'rentrez par ici' : ajouté automatiquement s'il n'existe pas déjà (rien à câbler).
            // Inutile si le spatial est désactivé : il n'y aura aucune boundary à réintégrer.
            if (showReturnIndicator && !GlobalConfig.SpatialSyncDisabled
                && GetComponent<BoundaryReturnIndicator>() == null)
                gameObject.AddComponent<BoundaryReturnIndicator>();

            if (VaroniaSpatialLoader.Data != null)
                Build();
            else
                VaroniaSpatialLoader.OnLoaded += OnSpatialLoaded;

            if (BackOfficeVaronia.Instance != null)
                BackOfficeVaronia.OnConfigLoaded += OnConfigLoaded;
            BackOfficeVaronia.OnMovieChanged += ApplyMovieMode;
        }

        private void OnDestroy()
        {
            VaroniaSpatialLoader.OnLoaded -= OnSpatialLoaded;
            BackOfficeVaronia.OnConfigLoaded -= OnConfigLoaded;
            BackOfficeVaronia.OnMovieChanged -= ApplyMovieMode;
            if (_instance == this) _instance = null;
            Clear();
        }

        private void OnSpatialLoaded()
        {
            VaroniaSpatialLoader.OnLoaded -= OnSpatialLoaded;
            Build();
        }

        private void OnConfigLoaded()
        {
            ApplyMovieMode();
        }

        /// <summary>Active ou désactive les MeshRenderers et LineRenderers selon BackOfficeVaronia.Instance.config.Movie.</summary>
        public void ApplyMovieMode()
        {
            _movieMode = BackOfficeVaronia.Instance != null
                         && BackOfficeVaronia.Instance.config != null
                         && BackOfficeVaronia.Instance.config.HideMode==2;

            foreach (var renderable in _renderables)
            {
                if (renderable.GroundLine != null)
                    renderable.GroundLine.enabled = !_movieMode;

                if (_movieMode)
                {
                    foreach (var seg in renderable.WallSegments)
                    {
                        if (seg.GO != null && seg.GO.activeSelf)
                            seg.GO.SetActive(false);
                    }
                }
            }
        }

        private void Update()
        {
            // Spatial désactivé : aucune géométrie, aucune proximité, aucun son — composant inerte.
            if (GlobalConfig.SpatialSyncDisabled) return;

            UpdateWorldPoints();
            UpdateProximity();
            UpdateSound();
        }

        private void UpdateWorldPoints()
        {
            Transform parentTr = transform.parent;

            foreach (var renderable in _renderables)
            {
                bool changed = false;
                for (int i = 0; i < renderable.LocalPoints.Count; i++)
                {
                    Vector3 newWorld = parentTr != null
                        ? parentTr.TransformPoint(renderable.LocalPoints[i])
                        : renderable.LocalPoints[i];

                    if (newWorld != renderable.WorldPoints[i])
                    {
                        renderable.WorldPoints[i] = newWorld;
                        changed = true;
                    }
                }

                if (!changed) continue;

                var lr = renderable.GroundLine;
                for (int i = 0; i < renderable.LocalPoints.Count; i++)
                    lr.SetPosition(i, renderable.LocalPoints[i]);

                for (int i = 0; i < renderable.WallSegments.Count; i++)
                {
                    var seg = renderable.WallSegments[i];
                    seg.A = renderable.WorldPoints[i];
                    seg.B = renderable.WorldPoints[(i + 1) % renderable.WorldPoints.Count];

                    Vector3 aLocal = renderable.LocalPoints[i];
                    Vector3 bLocal = renderable.LocalPoints[(i + 1) % renderable.LocalPoints.Count];

                    // On met à jour les vertices du mesh EXISTANT (pas de new Mesh par frame → pas de fuite).
                    var mesh = seg.MF.sharedMesh;
                    if (mesh == null) { mesh = BuildWallMesh(aLocal, bLocal, wallHeight); seg.MF.sharedMesh = mesh; }
                    else SetWallMeshVertices(mesh, aLocal, bLocal, wallHeight);

                    if (seg.Collider != null)
                        ConfigureWallColliderBox(seg, aLocal, bLocal, AdvBoundarySettings.WallColliderThickness);
                    seg.SegmentLength = Vector3.Distance(
                        new Vector3(aLocal.x, 0f, aLocal.z),
                        new Vector3(bLocal.x, 0f, bLocal.z));
                }
            }
        }

        // ─── Spectator gating ───────────────────────────────────────────────────────

        /// <summary>True when the device is a spectator (server or client). Spectators
        /// fly freely, so the boundary proximity/out-of-bounds sound is muted for them.
        /// Read live so a runtime DeviceMode change is honoured.</summary>
        private static bool IsSpectator()
        {
            var bo = BackOfficeVaronia.Instance;
            if (bo == null || bo.config == null) return false;
            var mode = bo.config.DeviceMode;
            return mode == DeviceMode.Server_Spectator || mode == DeviceMode.Client_Spectator;
        }

        // ─── Audio Setup ──────────────────────────────────────────────────────────

        private void SetupAudioSource()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop         = true;
            _audioSource.spatialBlend = 0f;
            _audioSource.volume       = 0f;
            _audioSource.pitch        = soundBasePitch;
            _audioSource.playOnAwake  = false;
            _audioSource.priority     = 0;
            _currentSoundPitch        = soundBasePitch;

            _audioSource.clip = boundaryAlertClip != null
                ? boundaryAlertClip
                : GenerateBeepClip(440f, 0.5f);
        }

        private static AudioClip GenerateBeepClip(float frequency, float duration)
        {
            int sampleRate  = 44100;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            var samples     = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t        = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / sampleCount);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
            }

            var clip = AudioClip.Create("BoundaryBeep", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ─── Build ────────────────────────────────────────────────────────────────

        public void Build()
        {
            Clear();

            // DontUseSpatialSync : une boundary posée manuellement dans la scène ne construit rien
            // (ni contours, ni murs, ni obstacles) et reste totalement inerte.
            if (GlobalConfig.SpatialSyncDisabled)
                return;

            _boundaryLayer = AdvBoundarySettings.Layer;
            gameObject.layer = _boundaryLayer;

            var spatial = VaroniaSpatialLoader.Data as Spatial;
            if (spatial?.Boundaries == null || spatial.Boundaries.Count == 0)
            {
                Debug.LogWarning("[AdvBoundary] Aucune boundary trouvée.");
                return;
            }

            // Option globale lue depuis le spatial (SpatialConfig / NewSpatial.json).
            _beepOnApproach = spatial.BeepOnApproach;

            for (int i = 0; i < spatial.Boundaries.Count; i++)
            {
                BuildBoundary(spatial.Boundaries[i], i);
                BuildObstacles(spatial.Boundaries[i], i);
            }

            Debug.Log($"[AdvBoundary] {_renderables.Count} boundary(ies), {_obstacleInstances.Count} obstacle(s) construits.");
            ApplyMovieMode();
        }

        private void BuildBoundary(Boundary_ boundary, int index)
        {
            if (boundary?.Points == null || boundary.Points.Count < 2) return;

            var bc  = boundary.BoundaryColor;
            Color col = bc != null ? new Color(bc.x, bc.y, bc.z, 1f) : Color.cyan;

            var renderable = new BoundaryRenderable
            {
                IsMain                         = boundary.MainBoundary,
                ProximityDistance              = boundary.DisplayDistance > 0f ? boundary.DisplayDistance : proximityDistance,
                AlertLimit                     = boundary.AlertLimit,
                HideLineFar = boundary.HideLineFar
            };

            // ── World points ─────────────────────────────────────────────────────
            foreach (var p in boundary.Points)
            {
                Vector3 localPt = new Vector3(p.x, groundOffset + 0.08f, p.z);
                renderable.LocalPoints.Add(localPt);
                Vector3 worldPt = transform.parent != null
                    ? transform.parent.TransformPoint(localPt)
                    : localPt;
                renderable.WorldPoints.Add(worldPt);
            }

            int ptCount = renderable.WorldPoints.Count;

            // ── Ground contour (LineRenderer) ────────────────────────────────────
            renderable.GroundGO = new GameObject($"AdvBoundary_Ground_{index}");
            renderable.GroundGO.layer = _boundaryLayer;
            renderable.GroundGO.transform.SetParent(transform, false);
            renderable.GroundGO.transform.localPosition = Vector3.zero;
            renderable.GroundGO.transform.localRotation = Quaternion.identity;
            renderable.GroundGO.transform.localScale    = Vector3.one;

            renderable.GroundLine = renderable.GroundGO.AddComponent<LineRenderer>();
            renderable.GroundMat  = CreateGroundMaterial(col);

            var lr = renderable.GroundLine;
            lr.material          = renderable.GroundMat;
            lr.useWorldSpace     = false;
            lr.loop              = true;
            lr.widthMultiplier   = boundary.MainBoundary ? groundLineWidth * 2f : groundLineWidth;
            lr.numCornerVertices = 8;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows    = false;
            lr.positionCount     = renderable.WorldPoints.Count;

            for (int i = 0; i < renderable.LocalPoints.Count; i++)
                lr.SetPosition(i, renderable.LocalPoints[i]);

            // ── Wall segments (mesh par segment) ─────────────────────────────────
            bool  genCollider       = AdvBoundarySettings.GenerateWallCollider;
            float colliderThickness = AdvBoundarySettings.WallColliderThickness;
            bool  colliderIsTrigger = AdvBoundarySettings.WallColliderIsTrigger;
            for (int i = 0; i < renderable.LocalPoints.Count; i++)
            {
                Vector3 a = renderable.LocalPoints[i];
                Vector3 b = renderable.LocalPoints[(i + 1) % renderable.LocalPoints.Count];

                var seg = new WallSegment
                {
                    A             = renderable.WorldPoints[i],
                    B             = renderable.WorldPoints[(i + 1) % renderable.WorldPoints.Count],
                    SegmentLength = Vector3.Distance(
                        new Vector3(a.x, 0f, a.z),
                        new Vector3(b.x, 0f, b.z))
                };

                seg.GO = new GameObject($"AdvBoundary_Wall_{index}_{i}");
                seg.GO.layer = _boundaryLayer;
                seg.GO.transform.SetParent(transform, false);
                seg.GO.transform.localPosition = Vector3.zero;
                seg.GO.transform.localRotation = Quaternion.identity;
                seg.GO.transform.localScale    = Vector3.one;

                seg.MF       = seg.GO.AddComponent<MeshFilter>();
                seg.Renderer = seg.GO.AddComponent<MeshRenderer>();
                seg.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                seg.Renderer.receiveShadows    = false;

                var wallMesh = BuildWallMesh(a, b, wallHeight);
                seg.MF.sharedMesh = wallMesh;
                seg.Mat     = CreateWallMaterial(col);
                seg.Renderer.material = seg.Mat;
                seg.GO.SetActive(false);

                // Optional trigger collider on the curtain (separate, always-active GO).
                if (genCollider)
                {
                    seg.ColliderGO = new GameObject($"AdvBoundary_WallCollider_{index}_{i}");
                    seg.ColliderGO.layer = _boundaryLayer;
                    seg.ColliderGO.transform.SetParent(transform, false);
                    seg.ColliderGO.transform.localScale = Vector3.one;
                    seg.Collider = seg.ColliderGO.AddComponent<BoxCollider>();
                    seg.Collider.isTrigger = colliderIsTrigger;
                    ConfigureWallColliderBox(seg, a, b, colliderThickness);
                }

                renderable.WallSegments.Add(seg);
            }

            _renderables.Add(renderable);
        }

        // ─── Obstacles ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Instancie le prefab (Small/Medium/Large, configuré dans Project Settings) pour chaque
        /// obstacle du JSON de cette boundary, et applique ses options au spawn :
        /// Position / Rotation / Scale sur le transform (repère local identique aux points de boundary),
        /// puis transmission des données complètes (dont SpecialId) via IAdvObstacle si présent.
        /// </summary>
        private void BuildObstacles(Boundary_ boundary, int index)
        {
            if (boundary?.Obstacles == null) return;

            // Scène active → sélectionne l'éventuel override de prefabs propre à cette map.
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            for (int oi = 0; oi < boundary.Obstacles.Count; oi++)
            {
                var o = boundary.Obstacles[oi];
                if (o == null) continue;

                // Aucun prefab configuré (ni override de scène, ni défaut) pour cette taille → on n'instancie rien.
                var prefab = AdvBoundarySettings.GetObstaclePrefab(o.Size, sceneName);
                if (prefab == null) continue;

                var go = Instantiate(prefab, transform, false);
                go.name = $"AdvObstacle_{index}_{oi}_{o.Size}";

                // Repère local identique aux points de boundary (local à 'transform').
                Vector3 pos = o.Position != null ? o.Position.asVec3() : Vector3.zero;
                Vector3 rot = o.Rotation != null ? o.Rotation.asVec3() : Vector3.zero;
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(rot);
                go.transform.localScale    = prefab.transform.localScale * Mathf.Max(0.0001f, o.Scale);

                // Hook optionnel : le prefab peut consommer les données complètes (SpecialId, etc.).
                foreach (var recv in go.GetComponentsInChildren<IAdvObstacle>(true))
                {
                    try { recv.OnObstacleSpawn(o); }
                    catch (System.Exception e) { Debug.LogException(e); }
                }

                _obstacleInstances.Add(go);
            }
        }

        // ─── Wall Collider ──────────────────────────────────────────────────────────

        /// <summary>
        /// Positions/sizes a segment's trigger BoxCollider as a thin wall from local
        /// point a to b, rising by wallHeight. The box is oriented so its length runs
        /// along the segment, its height is vertical, and its (thin) depth is the wall
        /// thickness. a/b are in the parent transform's local space (same as the mesh).
        /// </summary>
        private void ConfigureWallColliderBox(WallSegment seg, Vector3 a, Vector3 b, float thickness)
        {
            if (seg == null || seg.ColliderGO == null || seg.Collider == null) return;

            Vector3 a2 = new Vector3(a.x, 0f, a.z);
            Vector3 b2 = new Vector3(b.x, 0f, b.z);
            Vector3 d  = b2 - a2;
            float   len = d.magnitude;

            Vector3 mid = (a + b) * 0.5f;
            mid.y = (a.y + b.y) * 0.5f + wallHeight * 0.5f;

            seg.ColliderGO.transform.localPosition = mid;
            seg.ColliderGO.transform.localRotation = (len > 1e-4f)
                ? Quaternion.LookRotation(d / len, Vector3.up) // forward(z)=segment, up(y)=vertical
                : Quaternion.identity;

            seg.Collider.center = Vector3.zero;
            seg.Collider.size   = new Vector3(
                Mathf.Max(0.001f, thickness), // x = thickness (depth through the wall)
                Mathf.Max(0.001f, wallHeight),// y = height
                Mathf.Max(0.001f, len));      // z = length along the segment
        }

        // ─── Wall Mesh ────────────────────────────────────────────────────────────

        private static Mesh BuildWallMesh(Vector3 a, Vector3 b, float height)
        {
            // 4 vertices : bas-gauche, haut-gauche, haut-droite, bas-droite
            // UV.x = 0 à gauche, 1 à droite (le long du segment)
            // UV.y = 0 en bas, 1 en haut
            var mesh = new Mesh { name = "WallSegment" };

            SetWallMeshVertices(mesh, a, b, height);

            mesh.uv = new Vector2[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f),
            };

            // Front + back (double-sided) — inchangés lors des updates de vertices.
            mesh.triangles = new int[]
            {
                0, 1, 2,  0, 2, 3,   // front
                0, 2, 1,  0, 3, 2,   // back
            };

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Met à jour seulement les 4 positions du quad (UV/triangles restent constants).
        // Utilisé chaque frame quand la boundary bouge → évite d'allouer un nouveau Mesh.
        private static void SetWallMeshVertices(Mesh mesh, Vector3 a, Vector3 b, float height)
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(a.x, a.y,          a.z), // 0 bas-gauche
                new Vector3(a.x, a.y + height, a.z), // 1 haut-gauche
                new Vector3(b.x, b.y + height, b.z), // 2 haut-droite
                new Vector3(b.x, b.y,          b.z), // 3 bas-droite
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        // ─── Material Factories ───────────────────────────────────────────────────

        private Material CreateWallMaterial(Color col)
        {
            var shader = wallShader != null ? wallShader : Shader.Find("VaroniaBackOffice/AdvBoundaryWall");
            if (shader == null) { Debug.LogError("[AdvBoundary] Shader mur introuvable (VaroniaBackOffice/AdvBoundaryWall)."); return null; }
            var mat = new Material(shader);
            mat.SetColor(ColorId,         col);
            mat.SetFloat(PulseSpeedId,    wallPulseSpeed);
            mat.SetFloat(PulseIntId,      wallPulseIntensity);
            mat.SetFloat(FadeRadiusId,    wallFadeRadius);
            mat.SetFloat(CamProjUId,      0.5f);
            mat.SetFloat(CrossSizeId,         crossSize);
            mat.SetFloat(CrossGapId,          crossGap);
            mat.SetFloat(CrossThicknessId,    crossThicknessMin);
            mat.SetFloat(CrossThicknessMinId, crossThicknessMin);
            mat.SetFloat(CrossThicknessMaxId, crossThicknessMax);
            mat.SetFloat(CrossSharpnessId,    crossSharpness);
            mat.SetFloat(ProximityFadeId, 0f);
            return mat;
        }

        private Material CreateGroundMaterial(Color col)
        {
            var shader = groundShader != null ? groundShader : Shader.Find("VaroniaBackOffice/AdvBoundaryGround");
            if (shader == null) { Debug.LogError("[AdvBoundary] Shader sol introuvable (VaroniaBackOffice/AdvBoundaryGround)."); return null; }
            var mat = new Material(shader);
            mat.SetColor(ColorId,         col);
            mat.SetFloat(PulseSpeedId,    groundPulseSpeed);
            mat.SetFloat(PulseIntId,      groundPulseIntensity);
            mat.SetFloat(BaseAlphaId,      groundBaseAlpha);
            mat.SetFloat(IntensityScaleId, groundIntensityMin);
            mat.SetFloat(ProximityFadeId,  0f);
            return mat;
        }

        // ─── Proximity Update ─────────────────────────────────────────────────────

        private void UpdateProximity()
        {
            Vector3 camPos = GetCameraWorldPosition();
            if (!_cameraFound) return;

            Vector3 camPos2D = new Vector3(camPos.x, 0f, camPos.z);

            bool isOutside = _renderables.Count > 0 && IsOutsideAllBoundaries(camPos2D);

#if UNITY_EDITOR
            if (ForceDisableBoundaryWarning)
                isOutside = false;
#endif

            // Quand on est DEHORS, on masque le grillage au lieu de le forcer ON, pour ne pas laisser
            // croire que l'extérieur est interdit. De retour à l'intérieur, il refonctionne normalement
            // (le grillage se lève à l'approche des bords, comme avant).
            bool hideWalls = hideWallsOnceExited && isOutside;

            // ── Debug overlay ─────────────────────────────────────────────────────
            float debugMinDist = float.MaxValue;
            float debugMaxProx = 0f;
            float debugMaxGround = 0f;
            int   debugActive = 0;
            int   debugTotal  = 0;

            foreach (var r in _renderables)
            {
                // Distance minimale caméra → boundary (pour ground shader + son)
                float minDist = float.MaxValue;
                foreach (var seg in r.WallSegments)
                {
                    float d = DistancePointToSegment2D(camPos2D,
                        new Vector3(seg.A.x, 0f, seg.A.z),
                        new Vector3(seg.B.x, 0f, seg.B.z));
                    if (d < minDist) minDist = d;
                }

                if (r.HideLineFar && r.GroundLine != null)
                {
                    float farTarget = (isOutside || minDist < r.ProximityDistance) ? 1f : 0f;
                    r.CurrentFarFade = Mathf.Lerp(r.CurrentFarFade, farTarget,
                                                   Time.deltaTime * proximityLerpSpeed);

                    bool active = !_movieMode && r.CurrentFarFade > 0.01f;
                    if (r.GroundLine.enabled != active) r.GroundLine.enabled = active;
                    if (r.GroundMat != null)
                        r.GroundMat.SetFloat(BaseAlphaId, groundBaseAlpha * r.CurrentFarFade);
                }

                // Proximité globale (pour ground shader) — distance doublée pour le sol
                float groundProxDist  = r.ProximityDistance * 2f;
                float targetProximity = 0f;
                if (isOutside)
                    targetProximity = 1f;
                else if (minDist < groundProxDist)
                    targetProximity = 1f - Mathf.Clamp01(
                        (minDist - fullIntensityDistance) /
                        (groundProxDist - fullIntensityDistance));

                r.CurrentProximity = Mathf.Lerp(r.CurrentProximity, targetProximity,
                                                 Time.deltaTime * proximityLerpSpeed);
                r.GroundMat.SetFloat(ProximityFadeId, r.CurrentProximity);
                if (r.CurrentProximity > debugMaxGround) debugMaxGround = r.CurrentProximity;

                // Taille et intensité de la ligne au sol selon la proximité
                bool isMain = r.IsMain;
                r.GroundLine.widthMultiplier = Mathf.Lerp(
                    isMain ? groundWidthMin * 2f : groundWidthMin,
                    isMain ? groundWidthMax * 2f : groundWidthMax,
                    r.CurrentProximity);
                float intensityScale = Mathf.Lerp(groundIntensityMin, groundIntensityMax, r.CurrentProximity);
                r.GroundMat.SetFloat(IntensityScaleId, intensityScale);

                // Mise à jour de chaque segment de mur
                foreach (var seg in r.WallSegments)
                {
                    Vector3 a2D = new Vector3(seg.A.x, 0f, seg.A.z);
                    Vector3 b2D = new Vector3(seg.B.x, 0f, seg.B.z);

                    float segDist = DistancePointToSegment2D(camPos2D, a2D, b2D);
                    bool  visible = !hideWalls && (isOutside || segDist < r.ProximityDistance);
                    debugTotal++;
                    if (visible) debugActive++;
                    if (segDist < debugMinDist) debugMinDist = segDist;

                    if (!_movieMode && seg.GO.activeSelf != visible)
                        seg.GO.SetActive(visible);
                    else if (_movieMode && seg.GO.activeSelf)
                        seg.GO.SetActive(false);

                    if (!visible) continue;

                    // Proximité locale pour ce segment (max si en dehors)
                    float localProx = isOutside ? 1f : 1f - Mathf.Clamp01(
                        (segDist - fullIntensityDistance) /
                        (r.ProximityDistance - fullIntensityDistance));
                    if (localProx > debugMaxProx) debugMaxProx = localProx;

                    // Projection de la caméra sur le segment → coordonnée U [0,1]
                    float camProjU = GetProjectionU(camPos2D, a2D, b2D);

                    seg.Mat.SetFloat(ProximityFadeId, localProx);
                    seg.Mat.SetFloat(CamProjUId,      camProjU);
                    seg.Mat.SetFloat(FadeRadiusId,    r.ProximityDistance);
                    seg.Mat.SetFloat(CamWorldYId,     camPos.y);
                    seg.Mat.SetFloat(WallBottomYId,   seg.A.y);
                    seg.Mat.SetFloat(WallHeightId,    wallHeight);
                    seg.Mat.SetVector(SegmentAId,     new Vector4(seg.A.x, seg.A.y, seg.A.z, 0f));
                    seg.Mat.SetVector(SegmentBId,     new Vector4(seg.B.x, seg.B.y, seg.B.z, 0f));
                    seg.Mat.SetVector(CamPosWorldId,  new Vector4(camPos.x, camPos.y, camPos.z, 0f));
                }
            }

            if (_debug != null)
            {
                _debug.IsInsideBoundary = !isOutside;
                _debug.DistanceToWall   = debugMinDist >= float.MaxValue ? 9999f : debugMinDist;
                _debug.ProximityFade    = debugMaxProx;
            }
        }

        // ─── Sound Update ─────────────────────────────────────────────────────────

        private void UpdateSound()
        {
            if (_audioSource == null) return;

            Vector3 camPos   = GetCameraWorldPosition();
            Vector3 camPos2D = new Vector3(camPos.x, 0f, camPos.z);

            float globalMinDist = float.MaxValue;
            foreach (var r in _renderables)
            {
                float d = ComputeMinDistanceToBoundary(camPos2D, r.WorldPoints);
                if (d < globalMinDist) globalMinDist = d;
            }

            IsOutside = _renderables.Count > 0 && IsOutsideAllBoundaries(camPos2D);
            bool isOutside = IsOutside;

            // AlertLimit = false → pas de son ni d'alerte
            IsNolimit = _renderables.Count > 0 && _renderables.TrueForAll(r => !r.AlertLimit);

#if UNITY_EDITOR
            if (ForceDisableBoundaryWarning)
                IsNolimit = true;
#endif

            // Pas de son en mode spectateur (idem alerte UI), ni si AlertLimit=false partout.
            if (IsNolimit || IsSpectator())
            {
                if (_audioSource.isPlaying) _audioSource.Stop();
                _currentSoundVolume = 0f;
                return;
            }

            // Fenêtre de fondu : le son monte de 'soundStartDistance' (extérieur) jusqu'à
            // 'fullIntensityDistance' (collé au mur). On garantit start > full pour que la rampe soit atteignable.
            float startDist = Mathf.Max(soundStartDistance, fullIntensityDistance + 0.01f);

            float targetVolume;
            float targetPitch;

            if (isOutside)
            {
                // Hors zone : intensité maximale. (Toujours actif, quelle que soit l'option d'approche.)
                targetVolume = 1f;
                targetPitch  = soundMaxPitch;
            }
            else if (_beepOnApproach && globalMinDist <= fullIntensityDistance)
            {
                // Collé au mur (mais encore dedans) : quasi plein. Seulement si le bip d'approche est activé.
                targetVolume = 0.8f;
                targetPitch  = soundMaxPitch;
            }
            else if (_beepOnApproach && globalMinDist < startDist)
            {
                // Rampe progressive volume + pitch entre startDist et fullIntensityDistance (bip d'approche).
                float t = 1f - Mathf.Clamp01(
                    (globalMinDist - fullIntensityDistance) /
                    (startDist - fullIntensityDistance));
                targetVolume = t * 0.8f;
                targetPitch  = Mathf.Lerp(soundBasePitch, soundMaxPitch, t);
            }
            else
            {
                // Dedans sans bip d'approche (défaut), ou loin de la boundary : silence.
                targetVolume = 0f;
                targetPitch  = soundBasePitch;
            }

            _currentSoundVolume = Mathf.Lerp(_currentSoundVolume, targetVolume,
                                              Time.deltaTime * soundLerpSpeed);
            _currentSoundPitch  = Mathf.Lerp(_currentSoundPitch, targetPitch,
                                              Time.deltaTime * soundLerpSpeed);

            _audioSource.volume = _currentSoundVolume;
            _audioSource.pitch  = _currentSoundPitch;

            if (targetVolume > 0f && !_audioSource.isPlaying)
                _audioSource.Play();
            else if (targetVolume <= 0f && _currentSoundVolume < 0.01f && _audioSource.isPlaying)
                _audioSource.Stop();
        }

        /// <summary>
        /// Vérifie si une coordonnée world est à l'intérieur de la main boundary.
        /// </summary>
        /// <param name="worldPosition">Position world à tester (Y ignoré).</param>
        /// <param name="distanceToWall">Distance au mur le plus proche de la main boundary. -1 si aucune boundary trouvée.</param>
        /// <returns>True si la position est à l'intérieur de la main boundary, false sinon.</returns>
        public static bool IsInsideMainBoundary(Vector3 worldPosition, out float distanceToWall)
        {
            distanceToWall = -1f;

            var instance = Instance;
            if (instance == null || instance._renderables.Count == 0)
                return false;

            BoundaryRenderable main = null;
            foreach (var r in instance._renderables)
            {
                if (r.IsMain) { main = r; break; }
            }
            if (main == null) main = instance._renderables[0];

            Vector3 p2D = new Vector3(worldPosition.x, 0f, worldPosition.z);

            distanceToWall = ComputeMinDistanceToBoundary(p2D, main.WorldPoints);
            bool inside    = IsPointInsidePolygon2D(p2D, main.WorldPoints);

            return inside;
        }

        /// <summary>
        /// Retourne le centre (centroïde) de la main boundary en coordonnées world.
        /// Y = 0. Retourne Vector3.zero si aucune boundary n'est trouvée.
        /// </summary>
        public static Vector3 GetCenter()
        {
            var instance = Instance;
            if (instance == null || instance._renderables.Count == 0)
                return Vector3.zero;

            BoundaryRenderable main = null;
            foreach (var r in instance._renderables)
            {
                if (r.IsMain) { main = r; break; }
            }
            if (main == null) main = instance._renderables[0];

            var pts = main.WorldPoints;
            if (pts.Count == 0) return Vector3.zero;

            Vector3 sum = Vector3.zero;
            for (int i = 0; i < pts.Count; i++)
                sum += new Vector3(pts[i].x, 0f, pts[i].z);

            return sum / pts.Count;
        }

        /// <summary>
        /// Point le plus proche sur la main boundary (là où le joueur devrait re-rentrer) et
        /// direction pointant vers l'intérieur. Retourne false si aucune boundary exploitable.
        /// </summary>
        public static bool TryGetReturnTarget(Vector3 worldPos, out Vector3 closestPoint, out Vector3 inwardDir)
        {
            closestPoint = worldPos;
            inwardDir    = Vector3.forward;

            var instance = Instance;
            if (instance == null || instance._renderables.Count == 0) return false;

            BoundaryRenderable main = null;
            foreach (var r in instance._renderables) if (r.IsMain) { main = r; break; }
            if (main == null) main = instance._renderables[0];

            var pts = main.WorldPoints;
            if (pts == null || pts.Count < 2) return false;

            Vector3 p2D = new Vector3(worldPos.x, 0f, worldPos.z);
            int count   = pts.Count;
            float best  = float.MaxValue;
            Vector3 bestPt   = p2D;
            Vector3 centroid = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Vector3 a = pts[i];
                Vector3 b = pts[(i + 1) % count];
                centroid += new Vector3(a.x, 0f, a.z);

                Vector3 cp = ClosestPointOnSegment(p2D,
                    new Vector3(a.x, 0f, a.z), new Vector3(b.x, 0f, b.z));
                float d = Vector3.Distance(p2D, cp);
                if (d < best)
                {
                    best   = d;
                    bestPt = new Vector3(cp.x, (a.y + b.y) * 0.5f, cp.z);
                }
            }
            centroid /= count;

            closestPoint = bestPt;
            Vector3 dir  = new Vector3(centroid.x - bestPt.x, 0f, centroid.z - bestPt.z);
            inwardDir    = dir.sqrMagnitude < 1e-4f ? Vector3.forward : dir.normalized;
            return true;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float   t  = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return a + t * ab;
        }

        private bool IsOutsideAllBoundaries(Vector3 point2D)
        {
            if (_renderables.Count == 0) return false;

            foreach (var r in _renderables)
            {
                if (r.IsMain)
                    return !IsPointInsidePolygon2D(point2D, r.WorldPoints);
            }

            return !IsPointInsidePolygon2D(point2D, _renderables[0].WorldPoints);
        }

        private static bool IsPointInsidePolygon2D(Vector3 point, List<Vector3> polygon)
        {
            int  count  = polygon.Count;
            bool inside = false;
            float px = point.x, pz = point.z;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                float xi = polygon[i].x, zi = polygon[i].z;
                float xj = polygon[j].x, zj = polygon[j].z;

                bool intersect = ((zi > pz) != (zj > pz)) &&
                                 (px < (xj - xi) * (pz - zi) / (zj - zi) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static float ComputeMinDistanceToBoundary(Vector3 point, List<Vector3> pts)
        {
            float minDist = float.MaxValue;
            int   count   = pts.Count;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = pts[i];               a.y = 0f;
                Vector3 b = pts[(i + 1) % count]; b.y = 0f;
                float d = DistancePointToSegment2D(point, a, b);
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        private static float DistancePointToSegment2D(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float   t  = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
            return Vector3.Distance(p, a + t * ab);
        }

        private static float GetProjectionU(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            return Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.0001f));
        }

        // ─── Clear ────────────────────────────────────────────────────────────────

        public void Clear()
        {
            foreach (var r in _renderables)
            {
                if (r.GroundGO  != null) Destroy(r.GroundGO);
                if (r.GroundMat != null) Destroy(r.GroundMat);

                foreach (var seg in r.WallSegments)
                {
                    // Mesh créé au runtime (new Mesh) → non détruit avec le GO, à libérer explicitement.
                    if (seg.MF != null && seg.MF.sharedMesh != null) Destroy(seg.MF.sharedMesh);
                    if (seg.GO         != null) Destroy(seg.GO);
                    if (seg.ColliderGO != null) Destroy(seg.ColliderGO);
                    if (seg.Mat        != null) Destroy(seg.Mat);
                }
            }
            _renderables.Clear();

            foreach (var go in _obstacleInstances)
                if (go != null) Destroy(go);
            _obstacleInstances.Clear();
        }
    }
}
