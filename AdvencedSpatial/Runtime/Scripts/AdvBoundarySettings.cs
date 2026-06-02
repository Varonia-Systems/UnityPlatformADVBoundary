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
    }
}
