using UnityEngine;

namespace ChooGuard.App.Fps.Architecture
{
    // Editable reconstruction provenance, with no interaction behaviour.
    public sealed class BlueprintPartProvenance : MonoBehaviour
    {
        public string PacketPath;
        public string PacketSha256;
        public string PartId;
        public string Storey;
        public string ArchitecturalRole;
        public string BlueprintPath;
        public string BlueprintSha256;
        public string SourceFeatureId;
        [TextArea] public string SourceControlsPixels;
        [TextArea] public string ModelHeightBasis;
    }
}
