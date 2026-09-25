using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

namespace ChooGuard.App.Fps.Runtime
{
    // A lease owns cosmetics only. The parent gameplay registry and ActorNavigationBinding own identity/lifecycle.
    [DisallowMultipleComponent]
    public sealed class ActorVisualPool : MonoBehaviour
    {
        public GameObject[] Templates = Array.Empty<GameObject>();
        public Camera ViewCamera;
        public TMP_FontAsset KoreanFont;
        [Min(1)] public int MaximumVisible = 300;
        [Min(1f)] public float CullDistance = 65f;
        [Min(1f)] public float LabelDistance = 12f;
        [Min(1f)] public float AnimationDistance = 25f;
        [Min(.05f)] public float RefreshInterval = .2f;
        public int RegisteredCount => entries.Count;
        public int VisibleCount { get; private set; }

        private sealed class Visual
        {
            public GameObject Root;
            public TextMeshPro Label;
            public Animator[] Animators;
            public int Variant;
        }
        private sealed class Entry
        {
            public string Id;
            public ActorNavigationBinding Actor;
            public string Name;
            public string Role;
            public string Goal;
            public Visual Visual;
            public bool Hidden;
            public float DistanceSquared;
        }
        private sealed class EntryDistanceComparer : IComparer<Entry>
        {
            public int Compare(Entry a, Entry b)
            {
                var result = a.DistanceSquared.CompareTo(b.DistanceSquared);
                return result == 0 ? string.CompareOrdinal(a.Id, b.Id) : result;
            }
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<int, Stack<Visual>> free = new Dictionary<int, Stack<Visual>>();
        private readonly List<Entry> ordered = new List<Entry>(300);
        private readonly EntryDistanceComparer comparer = new EntryDistanceComparer();
        private readonly HashSet<int> validatedTemplates = new HashSet<int>();
        private float refreshAt;

        public bool RegisterActor(ActorNavigationBinding actor, string koreanName, string role, string readableGoal, out string reason)
        {
            if (actor == null || string.IsNullOrWhiteSpace(actor.ActorId))
            { reason = "actor_identity_missing"; return false; }
            if (entries.TryGetValue(actor.ActorId, out var existing))
            {
                if (existing.Actor != actor) { reason = "duplicate_actor_visual_identity"; return false; }
                SetPresentation(actor.ActorId, koreanName, role, readableGoal);
                reason = "ready"; return true;
            }
            entries.Add(actor.ActorId, new Entry { Id = actor.ActorId, Actor = actor,
                Name = koreanName ?? "", Role = role ?? "", Goal = readableGoal ?? "" });
            reason = "ready"; return true;
        }

        public bool SetPresentation(string actorId, string koreanName, string role, string readableGoal)
        {
            if (actorId == null || !entries.TryGetValue(actorId, out var entry)) return false;
            entry.Name = koreanName ?? ""; entry.Role = role ?? ""; entry.Goal = readableGoal ?? "";
            UpdateLabel(entry);
            return true;
        }

        // Useful for floor cutaways. This does not disable the actor, cancel its route or remove registry state.
        public bool SetCulled(string actorId, bool culled)
        {
            if (actorId == null || !entries.TryGetValue(actorId, out var entry)) return false;
            entry.Hidden = culled;
            if (culled) Release(entry);
            return true;
        }

        public bool TryGetVisual(string actorId, out GameObject visual)
        {
            visual = null;
            if (actorId == null || !entries.TryGetValue(actorId, out var entry) || entry.Visual == null) return false;
            visual = entry.Visual.Root;
            return visual != null;
        }

        // Only the authoritative lifecycle owner calls this on actual actor removal, never on visibility changes.
        public bool UnregisterActor(string actorId)
        {
            if (actorId == null || !entries.TryGetValue(actorId, out var entry)) return false;
            Release(entry);
            entries.Remove(actorId);
            return true;
        }

        public bool RefreshVisibility(Vector3 observer, out string reason)
        {
            if (!DetourNavigationSurface.Finite(observer)) { reason = "observer_position_invalid"; return false; }
            ordered.Clear();
            var maxDistanceSquared = CullDistance * CullDistance;
            foreach (var entry in entries.Values)
            {
                if (entry.Actor == null || entry.Hidden || !entry.Actor.gameObject.activeInHierarchy)
                { Release(entry); continue; }
                entry.DistanceSquared = (entry.Actor.transform.position - observer).sqrMagnitude;
                if (entry.DistanceSquared > maxDistanceSquared) { Release(entry); continue; }
                ordered.Add(entry);
            }
            ordered.Sort(comparer);
            for (var i = Mathf.Max(0, MaximumVisible); i < ordered.Count; i++) Release(ordered[i]);
            var succeeded = true;
            reason = "ready";
            for (var i = 0; i < ordered.Count && i < MaximumVisible; i++)
            {
                var entry = ordered[i];
                if (entry.Visual == null && !Acquire(entry, out var failure)) { succeeded = false; reason = failure; continue; }
                var label = entry.Visual.Label;
                label.gameObject.SetActive(KoreanFont != null && entry.DistanceSquared <= LabelDistance * LabelDistance);
                // Far bodies retain their identity and pose while skipping cosmetic skeletal evaluation.
                foreach (var animator in entry.Visual.Animators)
                    animator.enabled = entry.DistanceSquared <= AnimationDistance * AnimationDistance;
            }
            return succeeded;
        }

        private bool Acquire(Entry entry, out string reason)
        {
            if (Templates == null || Templates.Length == 0) { reason = "actor_visual_template_missing"; return false; }
            var variant = (int)(StableHash(entry.Id) % (uint)Templates.Length);
            var template = Templates[variant];
            if (template == null) { reason = "actor_visual_template_missing"; return false; }
            if (!validatedTemplates.Contains(template.GetInstanceID()))
            {
                if (template.GetComponentInChildren<NavMeshAgent>(true) != null ||
                    template.GetComponentInChildren<Rigidbody>(true) != null ||
                    template.GetComponentInChildren<CharacterController>(true) != null)
                { reason = "visual_template_contains_physics_pose_owner"; return false; }
                foreach (var behaviour in template.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(behaviour is TMP_Text))
                    { reason = "visual_template_contains_runtime_behaviour"; return false; }
                validatedTemplates.Add(template.GetInstanceID());
            }
            if (!free.TryGetValue(variant, out var pool)) { pool = new Stack<Visual>(); free.Add(variant, pool); }
            Visual visual = null;
            while (pool.Count > 0 && visual == null)
            {
                var cached = pool.Pop();
                if (cached.Root != null) visual = cached;
            }
            if (visual == null)
            {
                var root = new GameObject("Actor visual lease");
                root.SetActive(false);
                root.transform.SetParent(transform, false);
                var body = Instantiate(template, root.transform, false);
                body.transform.localPosition = Vector3.zero;
                body.SetActive(true);
                var animators = body.GetComponentsInChildren<Animator>(true);
                foreach (var animator in animators)
                {
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                }
                foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true)) renderer.updateWhenOffscreen = false;
                foreach (var collider in body.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                var labelObject = new GameObject("인물 이름과 목표");
                labelObject.SetActive(false);
                labelObject.transform.SetParent(root.transform, false);
                labelObject.transform.localPosition = Vector3.up * 2.05f;
                var label = labelObject.AddComponent<TextMeshPro>();
                label.fontSize = 2f;
                label.alignment = TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta = new Vector2(3.5f, .8f);
                label.richText = false;
                label.enableWordWrapping = true;
                visual = new Visual { Root = root, Label = label, Animators = animators, Variant = variant };
            }
            visual.Root.name = "인물 표현 " + entry.Id;
            visual.Root.transform.SetParent(entry.Actor.transform, false);
            visual.Root.transform.localPosition = Vector3.zero;
            visual.Root.transform.localRotation = Quaternion.identity;
            visual.Root.transform.localScale = Vector3.one;
            entry.Visual = visual;
            UpdateLabel(entry);
            visual.Root.SetActive(true);
            VisibleCount++;
            reason = "ready"; return true;
        }

        private void UpdateLabel(Entry entry)
        {
            if (entry.Visual == null) return;
            entry.Visual.Label.font = KoreanFont;
            entry.Visual.Label.text = entry.Name + " · " + entry.Role + (string.IsNullOrEmpty(entry.Goal) ? "" : "\n" + entry.Goal);
        }

        private void Release(Entry entry)
        {
            if (entry.Visual == null) return;
            var visual = entry.Visual;
            if (visual.Root != null)
            {
                visual.Root.SetActive(false);
                // Keep the inactive root in place: hierarchy changes are illegal during parent OnDisable.
                free[visual.Variant].Push(visual);
            }
            entry.Visual = null;
            VisibleCount--;
        }

        private void LateUpdate()
        {
            if (ViewCamera == null) return;
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + Mathf.Max(.05f, RefreshInterval);
                RefreshVisibility(ViewCamera.transform.position, out _);
            }
            foreach (var entry in entries.Values)
                if (entry.Visual != null && entry.Visual.Label.gameObject.activeSelf)
                    entry.Visual.Label.transform.rotation = ViewCamera.transform.rotation;
        }

        private void OnDisable()
        {
            foreach (var entry in entries.Values) Release(entry);
        }

        private void OnDestroy()
        {
            // Both leased and cached roots can remain under actors rather than the pool.
            foreach (var entry in entries.Values)
                if (entry.Visual != null && entry.Visual.Root != null) Destroy(entry.Visual.Root);
            foreach (var pool in free.Values)
                foreach (var visual in pool)
                    if (visual.Root != null) Destroy(visual.Root);
            free.Clear();
            entries.Clear();
        }

        private static uint StableHash(string id)
        {
            var value = 2166136261u;
            unchecked { foreach (var c in id) { value ^= c; value *= 16777619u; } }
            return value;
        }
    }
}
