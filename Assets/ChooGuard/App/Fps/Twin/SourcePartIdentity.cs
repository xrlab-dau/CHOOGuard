using UnityEngine;
namespace ChooGuard.App.Fps.Twin
{
    /// <summary>Identity of preserved legacy geometry. The transfer receipt proves the existing map was separated, not current site accuracy.</summary>
    [DisallowMultipleComponent]
    public sealed class SourcePartIdentity : MonoBehaviour
    {
        public string SourceId, SourcePath, SourceDefinition, Category, SourceSha256;
        public string ReconciliationReceipt;
        public int PreservedTriangles;
        public float TransferMaximumError;
        public bool LegacyGeometrySeparated;
        public bool SiteAccuracyVerified => false;
        public string OperationStatus;
    }
}
