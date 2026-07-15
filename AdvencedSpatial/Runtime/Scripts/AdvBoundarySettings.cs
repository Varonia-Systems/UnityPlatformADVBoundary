using System.Collections.Generic;
using UnityEngine;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Réglages projet de la boundary avancée, éditables via
    /// Project Settings ▸ Varonia ▸ Advanced Boundary.
    /// L'asset est chargé au runtime depuis un dossier Resources du package.
    /// </summary>
    public class AdvBoundarySettings : ScriptableObject
    {
        public const string ResourceName = "AdvBoundarySettings";

        [SerializeField]
        [Tooltip("Layer assigné à tous les GameObjects générés par la boundary lors de son instanciation.")]
        private int boundaryLayer = 0;

        /// <summary>Index du layer configuré pour les objets de la boundary.</summary>
        public int BoundaryLayer => boundaryLayer;

        [SerializeField]
        [Tooltip("Génère un BoxCollider fin sur le rideau (mur) de chaque segment " +
                 "de boundary au runtime, pour détecter ou bloquer le passage. " +
                 "Le collider reste actif même quand le mur n'est pas visible.")]
        private bool generateWallCollider = false;

        /// <summary>True si un collider doit être généré sur les murs de la boundary.</summary>
        public bool GenerateWallColliderValue => generateWallCollider;

        [SerializeField]
        [Tooltip("Épaisseur (en mètres) du collider de mur généré. Garder faible (rideau fin).")]
        private float wallColliderThickness = 0.1f;

        /// <summary>Épaisseur du collider de mur, en mètres.</summary>
        public float WallColliderThicknessValue => wallColliderThickness;

        [SerializeField]
        [Tooltip("Si activé, le collider de mur est un trigger (détecte le passage sans bloquer). " +
                 "Sinon c'est un collider solide qui bloque physiquement.")]
        private bool wallColliderIsTrigger = true;

        /// <summary>True si le collider de mur doit être un trigger.</summary>
        public bool WallColliderIsTriggerValue => wallColliderIsTrigger;

        [Header("Obstacle Prefabs")]
        [SerializeField]
        [Tooltip("Prefab instancié pour un obstacle de taille Small.")]
        private GameObject obstaclePrefabSmall;

        [SerializeField]
        [Tooltip("Prefab instancié pour un obstacle de taille Medium.")]
        private GameObject obstaclePrefabMedium;

        [SerializeField]
        [Tooltip("Prefab instancié pour un obstacle de taille Large.")]
        private GameObject obstaclePrefabLarge;

        /// <summary>
        /// Override des prefabs d'obstacles pour une scène donnée (par nom).
        /// Ex. : scène futuriste → obstacles futuristes ; grotte → cailloux.
        /// Un slot laissé vide retombe sur le prefab par défaut de cette taille.
        /// </summary>
        [System.Serializable]
        public class SceneObstacleOverride
        {
            [Tooltip("Nom de la scène (sans extension) sur laquelle cet override s'applique.")]
            public string sceneName;
            public GameObject small;
            public GameObject medium;
            public GameObject large;

            public GameObject Get(ObstacleSize size)
            {
                switch (size)
                {
                    case ObstacleSize.Small:  return small;
                    case ObstacleSize.Medium: return medium;
                    case ObstacleSize.Large:  return large;
                    default:                  return null;
                }
            }
        }

        [SerializeField]
        [Tooltip("Overrides de prefabs d'obstacles par scène.")]
        private List<SceneObstacleOverride> sceneOverrides = new List<SceneObstacleOverride>();

        private static AdvBoundarySettings _instance;

        /// <summary>Asset de réglages chargé depuis Resources, ou null s'il n'existe pas encore.</summary>
        public static AdvBoundarySettings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<AdvBoundarySettings>(ResourceName);
                return _instance;
            }
        }

        /// <summary>Layer configuré, ou 0 (Default) si aucun asset de réglages n'existe.</summary>
        public static int Layer => Instance != null ? Instance.boundaryLayer : 0;

        /// <summary>True si un collider doit être généré sur les murs (false si aucun asset).</summary>
        public static bool GenerateWallCollider => Instance != null && Instance.generateWallCollider;

        /// <summary>Épaisseur du collider de mur (m), clampée &gt; 0. 0.1 par défaut si aucun asset.</summary>
        public static float WallColliderThickness =>
            Instance != null ? Mathf.Max(0.001f, Instance.wallColliderThickness) : 0.1f;

        /// <summary>True si le collider de mur est un trigger (true par défaut si aucun asset).</summary>
        public static bool WallColliderIsTrigger => Instance == null || Instance.wallColliderIsTrigger;

        private GameObject DefaultPrefab(ObstacleSize size)
        {
            switch (size)
            {
                case ObstacleSize.Small:  return obstaclePrefabSmall;
                case ObstacleSize.Medium: return obstaclePrefabMedium;
                case ObstacleSize.Large:  return obstaclePrefabLarge;
                default:                  return null;
            }
        }

        /// <summary>Prefab par défaut (hors override de scène) pour la taille donnée.</summary>
        public static GameObject GetObstaclePrefab(ObstacleSize size)
        {
            var inst = Instance;
            return inst != null ? inst.DefaultPrefab(size) : null;
        }

        /// <summary>
        /// Prefab d'obstacle pour la taille donnée, en tenant compte d'un éventuel override de scène.
        /// Si la scène a un override et que le slot de cette taille est rempli, il l'emporte ;
        /// sinon on retombe sur le prefab par défaut.
        /// </summary>
        public static GameObject GetObstaclePrefab(ObstacleSize size, string sceneName)
        {
            var inst = Instance;
            if (inst == null) return null;

            if (!string.IsNullOrEmpty(sceneName) && inst.sceneOverrides != null)
            {
                foreach (var ov in inst.sceneOverrides)
                {
                    if (ov == null || ov.sceneName != sceneName) continue;
                    var p = ov.Get(size);
                    if (p != null) return p;   // slot rempli → override
                    break;                     // scène trouvée mais slot vide → défaut
                }
            }
            return inst.DefaultPrefab(size);
        }
    }
}
