using Unity.Cinemachine;
using UnityEngine;

namespace ChooGuard.App.Fps.TrainDeparture
{
    // Uses Cinemachine 3.1.7 CameraManagerBase/BlendManager unchanged.
    // Foundation: revision 8cb586bf217170b0035e42685880fe1fdefc9729,
    // Runtime/Core/CinemachineCameraManagerBase.cs:183-234,309-325.
    // Unity Companion Cinemachine Code License 1.0; copyright 2025 Unity Technologies ApS.
    // Retain package LICENSE.md and license.txt with the package distribution.
    [DisallowMultipleComponent]
    public sealed class DepartureSignalCameraSelector : CinemachineCameraManagerBase
    {
        public CinemachineCamera FpsCamera, EmoteCamera;
        public bool ShowEmote;

        protected override CinemachineVirtualCameraBase ChooseCurrentCamera(Vector3 worldUp, float deltaTime)
        {
            var selected = ShowEmote ? EmoteCamera : FpsCamera;
            return selected != null && selected.transform.parent == transform ? selected : null;
        }
    }
}
