using UnityEngine;

namespace ChooGuard.Foundation.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoPlayerController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField, Min(.1f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(.1f)] private float interactionReach = 2.8f;
        private CharacterController character;
        private float verticalSpeed;
        private float pitch;
        private bool controlEnabled;
        private float lookReadyAt;

        public Camera ViewCamera { get { return viewCamera; } }
        public float InteractionReach { get { return interactionReach; } }
        public bool ControlEnabled { get { return controlEnabled; } }
        public bool InputAvailable { get { return DemoInput.Available; } }
        public string InputError { get { return DemoInput.Error; } }

        public void Configure(Camera camera)
        {
            viewCamera = camera;
            character = GetComponent<CharacterController>();
        }

        private void Awake()
        {
            character = GetComponent<CharacterController>();
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
        }

        public void SetControlEnabled(bool value)
        {
            controlEnabled = value;
            if (value) lookReadyAt = Time.unscaledTime + .12f;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
        }

        public void Teleport(Transform spawn)
        {
            if (spawn == null) return;
            if (character == null) character = GetComponent<CharacterController>();
            var wasEnabled = character.enabled;
            character.enabled = false;
            transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            character.enabled = wasEnabled;
            verticalSpeed = 0;
            pitch = 0;
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            if (!controlEnabled || viewCamera == null || !DemoInput.Available) return;
            ApplyLook(DemoInput.Look);
            var input = DemoInput.Movement;
            if (character.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            var movement = (transform.right * input.x + transform.forward * input.y) * moveSpeed;
            movement.y = verticalSpeed;
            character.Move(movement * Time.deltaTime);
        }

        public void ApplyLook(Vector2 look)
        {
            if (!controlEnabled || viewCamera == null || Time.unscaledTime < lookReadyAt) return;
            transform.Rotate(0, look.x, 0, Space.Self);
            pitch = Mathf.Clamp(pitch - look.y, -75, 75);
            viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        public bool TryGetTarget(out DemoInteractable target)
        {
            target = null;
            if (viewCamera == null) return false;
            RaycastHit hit;
            var ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            // First hit wins: walls and unrelated objects occlude interaction. Trigger zones are ignored.
            if (!Physics.Raycast(ray, out hit, interactionReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            target = hit.collider.GetComponentInParent<DemoInteractable>();
            return target != null && target.isActiveAndEnabled;
        }

        private void OnDisable()
        {
            if (Application.isPlaying) SetControlEnabled(false);
        }
    }
}
