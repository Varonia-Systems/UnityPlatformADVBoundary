using UnityEngine;

namespace VaroniaBackOffice
{
    /// <summary>
    /// Script de debug pour tester IsInsideMainBoundary en loop.
    /// Attacher sur n'importe quel GameObject. Visible dans l'Inspector.
    /// </summary>
    public class AdvBoundaryInsideChecker : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Transform dont la position world est testée. Si vide, utilise ce GameObject.")]
        [SerializeField] private Transform target;

        [Header("Result (read-only)")]
        [SerializeField] private bool  isInsideMainBoundary;
        [SerializeField] private float distanceToWall;

        private void Update()
        {
            Vector3 pos = target != null ? target.position : transform.position;
            isInsideMainBoundary = AdvBoundary.IsInsideMainBoundary(pos, out distanceToWall);
        }
    }
}
