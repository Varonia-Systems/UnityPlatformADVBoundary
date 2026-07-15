namespace VaroniaBackOffice
{
    /// <summary>
    /// Implémentée par un composant présent sur un prefab d'obstacle (Small/Medium/Large).
    /// Appelée juste après l'instanciation par AdvBoundary, une fois Position/Rotation/Scale
    /// déjà appliqués, pour transmettre les données complètes de l'obstacle (notamment SpecialId).
    /// </summary>
    public interface IAdvObstacle
    {
        void OnObstacleSpawn(Obstacle_ data);
    }
}
