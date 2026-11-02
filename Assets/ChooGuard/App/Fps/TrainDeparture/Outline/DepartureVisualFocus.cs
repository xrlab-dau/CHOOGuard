// Domain adapter for Nition octree + Godot ray-triangle + Unity per-object outline foundations.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
namespace ChooGuard.App.Fps.TrainDeparture
{
    [Serializable] public sealed class VisualContactBinding
    {
        public Collider Contact;
        [Tooltip("Exact visual part(s) owned by this contact, never a shared train/enclosure parent.")]
        public Renderer[] Visuals = Array.Empty<Renderer>();
    }
    // Scene references must live on a scene component, not the persistent geometry catalog.
    [Serializable] public sealed class VisualSourceBinding
    {
        public MeshRenderer Renderer;
        public Mesh Source;
    }
    [DefaultExecutionOrder(9000)]
    public sealed class DepartureVisualFocus : MonoBehaviour
    {
        public TrainDepartureGame Game;
        public DepartureContextActions Actions;
        public Transform[] AdditionalDynamicRoots = Array.Empty<Transform>();
        public VisualGeometryCatalog GeometryCatalog;
        public VisualSourceBinding[] SourceBindings = Array.Empty<VisualSourceBinding>();
        public VisualAlphaCache[] AlphaCaches = Array.Empty<VisualAlphaCache>();
        public VisualContactBinding[] ContactBindings = Array.Empty<VisualContactBinding>();
        [Range(.001f, .05f)] public float ContactTolerance = .025f;
        public VisualSceneIndex Index { get; private set; }
        public VisualAimHit Hit { get; private set; }
        public Camera FpsCamera => Game != null && Game.Player != null ? Game.Player.PlayerCamera : null;
        public bool CanFocus => isActiveAndEnabled && Game != null && Game.Started && !Game.Completed && !Game.IsEmoting &&
            Game.Player != null && !Game.Player.IsPaused && (Game.Player.ExternalInputMode || Game.Player.HasFocus) &&
            (Game.View == null || !Game.View.MenuVisible) && FpsCamera != null && FpsCamera.isActiveAndEnabled && Time.timeScale > 0;
        static readonly Dictionary<Camera, DepartureVisualFocus> registry = new Dictionary<Camera, DepartureVisualFocus>();
        readonly Dictionary<Collider, HashSet<Renderer>> contacts = new Dictionary<Collider, HashSet<Renderer>>();
        Func<Collider, Vector3, float, bool> callback;
        VisualAimHit validatedHit;
        DepartureContextActions boundActions;
        Camera registeredCamera;
        bool initialized, started;
        void OnEnable()
        {
            callback = ValidateContact;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            SceneManager.sceneUnloaded += SceneUnloaded;
            if (started) Refresh();
        }
        void Start() { started = true; Refresh(); }
        public void Refresh()
        {
            Clear();
            if (Game == null) return;
            var moving = new HashSet<Transform>();
            if (Game.Player != null) moving.Add(Game.Player.transform);
            if (Game.OwnBag != null) moving.Add(Game.OwnBag);
            if (Game.KeyInventory != null && Game.KeyInventory.KeyModel != null) moving.Add(Game.KeyInventory.KeyModel);
            foreach (var door in Game.Doors) if (door != null) foreach (var piece in door.Pieces) if (piece != null) moving.Add(piece);
            if (Actions == null) Actions = Game.ContextActions;
            if (Actions != null)
            {
                if (Actions.RadioRoot != null) moving.Add(Actions.RadioRoot);
                if (Actions.Manipulation != null) foreach (var part in Actions.Manipulation.Parts)
                    if (part != null) { if (part.LinearPart != null) moving.Add(part.LinearPart); if (part.TurnPart != null) moving.Add(part.TurnPart); }
            }
            foreach (var root in AdditionalDynamicRoots) if (root != null) moving.Add(root);
            Index = new VisualSceneIndex();
            // One scan per explicit refresh. Includes inactive carried props that can become visible later.
            Index.Build(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None), Game.TrainRoot, moving, AlphaCaches, GeometryCatalog, SourceBindings);
            contacts.Clear();
            foreach (var binding in ContactBindings)
            {
                if (binding == null || binding.Contact == null) continue;
                if (!contacts.TryGetValue(binding.Contact, out var owners)) { owners = new HashSet<Renderer>(); contacts.Add(binding.Contact, owners); }
                foreach (var renderer in binding.Visuals) if (renderer != null) owners.Add(renderer);
            }
            Unbind(); boundActions = Actions;
            if (boundActions != null) boundActions.VisualAimValidator = callback;
            initialized = true;
        }
        void LateUpdate()
        {
            if (!initialized || !CanFocus) { Clear(); return; }
        }
        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != FpsCamera) return;
            // Camera/emote state can change after LateUpdate; never publish stale target to rendering.
            if (!CanFocus) Clear(); else Select();
        }
        void Select()
        {
            validatedHit = default;
            if (Index == null || boundActions == null || boundActions.VisualAimValidator != callback ||
                !boundActions.TryResolveFocusContact(out var contact) ||
                validatedHit.Renderer == null ||
                !MatchesContact(contact.collider, contact.point, contact.distance, validatedHit)) { Clear(); return; }
            Hit = validatedHit;
            if (registeredCamera != null && registeredCamera != FpsCamera) registry.Remove(registeredCamera);
            registeredCamera = FpsCamera; registry[registeredCamera] = this;
        }
        bool ValidateContact(Collider candidate, Vector3 point, float distance)
        {
            if (!CanFocus || Index == null || candidate == null || !candidate.enabled || !candidate.gameObject.activeInHierarchy ||
                float.IsNaN(distance) || float.IsInfinity(distance)) return false;
            // Fresh camera/geometry query at F admission, independent of previous frame's outline.
            if (!Index.Resolve(FpsCamera, out var hit)) return false;
            if (!MatchesContact(candidate, point, distance, hit)) return false;
            validatedHit = hit;
            return true;
        }
        bool MatchesContact(Collider candidate, Vector3 point, float distance, VisualAimHit hit)
        {
            if (candidate == null || hit.Renderer == null) return false;
            bool owner = contacts.TryGetValue(candidate, out var owners) && owners.Contains(hit.Renderer);
            // Own GameObject identity is safe; NEVER infer identity from a shared parent/aggregate collider.
            owner |= candidate.transform == hit.Renderer.transform;
            float tolerance = Mathf.Clamp(ContactTolerance, .001f, .05f);
            return owner && Mathf.Abs(distance - hit.Distance) <= tolerance && (point - hit.Point).sqrMagnitude <= tolerance * tolerance;
        }
        internal static bool TryGet(Camera camera, out DepartureVisualFocus focus)
        {
            if (registry.TryGetValue(camera, out focus) && focus != null && focus.CanFocus && focus.Hit.Renderer != null &&
                focus.Hit.Renderer.enabled && focus.Hit.Renderer.gameObject.activeInHierarchy && !focus.Hit.Renderer.forceRenderingOff) return true;
            focus = null; return false;
        }
        void SceneUnloaded(Scene scene) { Clear(); initialized = false; Index = null; }
        void Clear() { Hit = validatedHit = default; if (!ReferenceEquals(registeredCamera, null)) registry.Remove(registeredCamera); registeredCamera = null; }
        void Unbind() { if (boundActions != null && boundActions.VisualAimValidator == callback) boundActions.VisualAimValidator = null; boundActions = null; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera; SceneManager.sceneUnloaded -= SceneUnloaded;
            Unbind(); Clear(); initialized = false; Index = null;
        }
        void OnDestroy() { Unbind(); Clear(); }
    }
}
