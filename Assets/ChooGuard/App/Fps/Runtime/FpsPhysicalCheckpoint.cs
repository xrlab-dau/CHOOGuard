using System;
using System.Collections.Generic;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>In-memory physical half of a paired tutorial checkpoint; does not pretend to be a persisted WorldSnapshot.</summary>
    public sealed class FpsPhysicalCheckpoint
    {
        private readonly struct Pose
        {
            public readonly Vector3 Position, Velocity, AngularVelocity;
            public readonly Quaternion Rotation;
            public Pose(Rigidbody body)
            { Position = body.position; Rotation = body.rotation; Velocity = body.velocity; AngularVelocity = body.angularVelocity; }
            public void Restore(Rigidbody body)
            { body.position = Position; body.rotation = Rotation; body.velocity = Velocity; body.angularVelocity = AngularVelocity; }
        }
        private readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>(StringComparer.Ordinal);
        private readonly StableId runId;
        private readonly long generation;
        private readonly Vector3 playerPosition;
        private readonly float playerYaw, playerPitch;
        private FpsPhysicalCheckpoint(WorldSession session, FirstPersonResponder responder)
        {
            runId = session.RunId; generation = session.Generation;
            playerPosition = responder.transform.position; playerYaw = responder.YawDegrees; playerPitch = responder.PitchDegrees;
        }

        internal static FpsPhysicalCheckpoint Capture(WorldSession session, FirstPersonResponder responder)
        {
            if (session == null || session.Mode != GameplayMode.Tutorial) throw new InvalidOperationException("Only tutorial checkpoints may be rewound.");
            if (responder == null || responder.PlayerCamera == null) throw new InvalidOperationException("The physical player rig is not bound.");
            var result = new FpsPhysicalCheckpoint(session, responder);
            foreach (var binding in FpsEntityBinding.Live)
            {
                if (binding == null || binding.Body == null || string.IsNullOrEmpty(binding.EntityId)) continue;
                if (!session.TryGetEntity(new StableId(binding.EntityId), out _)) continue;
                if (binding.Body.velocity.sqrMagnitude > .0001f || binding.Body.angularVelocity.sqrMagnitude > .0001f)
                    throw new InvalidOperationException("Settle physical bodies before capturing a checkpoint.");
                result.poses.Add(binding.EntityId, new Pose(binding.Body));
            }
            return result;
        }
        internal void Validate(WorldSession session, FirstPersonResponder responder)
        {
            if (session == null || session.Mode != GameplayMode.Tutorial || !session.RunId.Equals(runId))
                throw new InvalidOperationException("The physical checkpoint belongs to a different tutorial run.");
            if (responder == null || responder.PlayerCamera == null) throw new InvalidOperationException("The physical player rig is not bound.");
            var matched = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in FpsEntityBinding.Live)
            {
                if (binding == null || binding.Body == null || string.IsNullOrEmpty(binding.EntityId)) continue;
                if (!session.TryGetEntity(new StableId(binding.EntityId), out _)) continue;
                if (!poses.ContainsKey(binding.EntityId) || !matched.Add(binding.EntityId))
                    throw new InvalidOperationException("Physical binding layout differs from the checkpoint.");
            }
            if (matched.Count != poses.Count) throw new InvalidOperationException("A checkpoint body is missing.");
        }
        internal void Restore(WorldSession session, FirstPersonResponder responder)
        {
            Validate(session, responder);
            if (session.Generation <= generation)
                throw new InvalidOperationException("Restore the paired tutorial world snapshot before its physical checkpoint.");
            foreach (var binding in FpsEntityBinding.Live)
            {
                if (binding == null || binding.Body == null || string.IsNullOrEmpty(binding.EntityId)) continue;
                if (!session.TryGetEntity(new StableId(binding.EntityId), out _)) continue;
                binding.StopConstraint(); poses[binding.EntityId].Restore(binding.Body);
            }
            responder.RestorePhysicalPose(playerPosition, playerYaw, playerPitch);
        }
    }
}
