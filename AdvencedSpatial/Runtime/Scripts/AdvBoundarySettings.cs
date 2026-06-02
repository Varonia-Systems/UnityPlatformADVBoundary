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
        [Tooltip("Génère un MeshCollider physique sur le rideau (mur) de chaque segment " +
                 "de boundary au runtime, pour bloquer physiquement le passage. " +
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
    }
}
