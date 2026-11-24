using Unity.Cinemachine;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // Source-based stage adapter, calling the unmodified Cinemachine 3.1.7 Deoccluder.
    // Foundation: revision 8cb586bf217170b0035e42685880fe1fdefc9729,
    // Runtime/Behaviours/CinemachineDeoccluder.cs:382-550 (Body correction),
    // Runtime/Core/CinemachineCameraManagerBase.cs:320-325 (blended Finalize state).
    // Unity Companion Cinemachine Code License 1.0; copyright 2025 Unity Technologies ApS.
    // Retain package LICENSE.md and license.txt with the package distribution.
    [DisallowMultipleComponent]
    public sealed class DepartureSignalCameraDeoccluder : CinemachineDeoccluder
    {
        public int CorrectedFrame { get; private set; } = -1;

        protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            // Manager extensions also receive child callbacks. Correct only the final blended state.
            if (vcam != ComponentOwner || !(vcam is DepartureSignalCameraSelector) ||
                stage != CinemachineCore.Stage.Finalize) return;
            base.PostPipelineStageCallback(vcam, CinemachineCore.Stage.Body, ref state, deltaTime);
            base.PostPipelineStageCallback(vcam, CinemachineCore.Stage.Finalize, ref state, deltaTime);
            CorrectedFrame = Time.frameCount;
        }
    }
}
