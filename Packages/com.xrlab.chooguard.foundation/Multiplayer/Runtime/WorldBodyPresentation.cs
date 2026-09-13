using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    /// <summary>Presentation colliders never participate in server static geometry queries.
    /// Physical contacts use the corresponding authoritative crowd body.</summary>
    public sealed class WorldBodyPresentation : MonoBehaviour
    {
        // Existing Evacuee mesh fits the conservative bound sqrt(.3545^2 + .195^2) < .405 m.
        // This world profile is distinct from the .15 m laboratory corridor calibration profile.
        public const double NpcRadiusM = .41, NpcHeightM = 1.8, PlayerRadiusM = .3;
        public string BodyId;
        public static bool IsPresentation(Collider collider) => collider.GetComponentInParent<WorldBodyPresentation>() != null;
    }
}
