using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ChooGuard.Foundation.Multiplayer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class VoiceEpochCoordinatorTests
    {
        private const int TestStepBudget = 1024;
        private const int TestActionBudget = 1024;
        private static readonly string[] RoomsA = { "room-a", "room-command" };
        private static readonly string[] RoomsB = { "room-b", "room-command-b" };

        [Test]
        public void F1_OldRetirementIsCompletedBeforeAnyReplacementCreate()
        {
            var store = new FakeStore(Manifest("a", RoomsA, true));
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("b")), Is.True);

            var trace = Drain(coordinator, action => true);
            Assert.That(trace.Select(x => x.Kind).Take(2), Is.EqualTo(new[] { VoiceEpochActionKind.DeleteRoom, VoiceEpochActionKind.DeleteRoom }));
            var firstCreate = trace.FindIndex(x => x.Kind == VoiceEpochActionKind.CreateRoom);
            Assert.That(firstCreate, Is.GreaterThan(1));
            Assert.That(trace.Take(firstCreate).All(x => x.Kind == VoiceEpochActionKind.DeleteRoom || x.Kind == VoiceEpochActionKind.CommitIntent), Is.True);
            Assert.That(trace.Take(firstCreate).Count(x => x.Kind == VoiceEpochActionKind.DeleteRoom), Is.EqualTo(RoomsA.Length));
            Assert.That(store.Commits.Select(x => x.Ready), Is.EqualTo(new[] { false, true }));
            Assert.That(coordinator.Ready, Is.True);
        }

        [Test]
        public void F2_FailedCandidateRemainsRetirableAfterReconstruction()
        {
            var store = new FakeStore(Manifest("a", RoomsA, true));
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("b")), Is.True);
            var calls = new List<string>();
            Drain(coordinator, action =>
            {
                calls.Add(action.Kind + ":" + action.Room);
                return action.Kind != VoiceEpochActionKind.CreateRoom;
            });
            Assert.That(coordinator.Failed, Is.True);
            Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));

            var recovery = NewCoordinator(store);
            Assert.That(recovery.StartRetire(), Is.True);
            var retired = Drain(recovery, action => true);
            Assert.That(retired.Where(x => x.Kind == VoiceEpochActionKind.DeleteRoom).Select(x => x.Room),
                Is.EquivalentTo(RoomsB));
            Assert.That(calls.Any(x => x.Contains("CreateRoom")), Is.True);
        }

        [Test]
        public void F3_ResponseLossKeepsCandidateInventoryForExplicitRetry()
        {
            var store = new FakeStore(Manifest("a", RoomsA, true));
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("b")), Is.True);
            var createdExternally = new List<string>();
            var first = true;
            Drain(coordinator, action =>
            {
                if (action.Kind == VoiceEpochActionKind.CreateRoom && first)
                {
                    first = false;
                    createdExternally.Add(action.Room);
                    return false;
                }
                return true;
            });
            Assert.That(coordinator.Failed, Is.True);
            Assert.That(store.Read().Manifest.Rooms, Is.EquivalentTo(RoomsB));
            Assert.That(createdExternally, Is.Not.Empty);

            var retry = NewCoordinator(store);
            Assert.That(retry.StartProvision(Epoch("c")), Is.True);
            var retryDeletes = new List<string>();
            Drain(retry, action =>
            {
                if (action.Kind == VoiceEpochActionKind.DeleteRoom) retryDeletes.Add(action.Room);
                return true;
            });
            Assert.That(retryDeletes, Is.EquivalentTo(RoomsB));
            Assert.That(retry.Ready, Is.True);
        }

        [Test]
        public void F4_FreshCoordinatorUsesDurableIntentWithoutMemoryState()
        {
            var store = new FakeStore(Manifest("b", RoomsB, false));
            var fresh = NewCoordinator(store);
            Assert.That(fresh.StartRetire(), Is.True);
            var actions = Drain(fresh, action => true);
            Assert.That(actions.Select(x => x.Room), Is.EquivalentTo(RoomsB));
            Assert.That(fresh.Failed, Is.False);
        }

        [Test]
        public void F5_HappyPathKeepsReadyEpochAndExpectedRoomInventory()
        {
            var store = new FakeStore();
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            var actions = Drain(coordinator, action => true);
            Assert.That(actions.Count(x => x.Kind == VoiceEpochActionKind.CreateRoom), Is.EqualTo(RoomsA.Length));
            Assert.That(coordinator.Ready, Is.True);
            Assert.That(store.Read().Manifest.Ready, Is.True);
            Assert.That(store.Read().Manifest.Rooms, Is.EquivalentTo(RoomsA));

            var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000);
            var trainee = new ParticipantState { ParticipantId = "trainee", TeamId = "red", RoleId = "role-01" };
            var instructor = new ParticipantState { ParticipantId = "instructor", TeamId = "red", RoleId = "role-01", IsInstructor = true };
            var team = issuer.Join("world", "shift", Epoch("a"), trainee, "team", "wss://voice.example");
            var command = issuer.Join("world", "shift", Epoch("a"), trainee, "command", "wss://voice.example");
            var instructorCommand = issuer.Join("world", "shift", Epoch("a"), instructor, "command", "wss://voice.example");
            var nextEpoch = issuer.Join("world", "shift", Epoch("b"), trainee, "team", "wss://voice.example");
            Assert.That(team.CanPublish, Is.True);
            Assert.That(command.CanPublish, Is.False);
            Assert.That(instructorCommand.CanPublish, Is.True);
            Assert.That(team.ExpiresAt, Is.EqualTo(1090));
            Assert.That(nextEpoch.Room, Is.Not.EqualTo(team.Room));
        }

        [Test]
        public void F6_CommitUncertaintyFailsClosedBeforeRemoteCreate()
        {
            var store = new FakeStore { CommitResult = false, PersistOnFailedCommit = false };
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            var first = default(VoiceEpochAction);
            Assert.That(coordinator.TryGetAction(out first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(VoiceEpochActionKind.CommitIntent));
            Assert.That(coordinator.Complete(first, true), Is.False);
            Assert.That(coordinator.Failed, Is.True);
            Assert.That(coordinator.HasPendingAction, Is.False);
            Assert.That(store.Read().Exists, Is.False);

            var persistedButUnacknowledged = new FakeStore { CommitResult = false, PersistOnFailedCommit = true };
            var recovered = NewCoordinator(persistedButUnacknowledged);
            Assert.That(recovered.StartProvision(Epoch("a")), Is.True);
            Assert.That(recovered.TryGetAction(out first), Is.True);
            Assert.That(recovered.Complete(first, true), Is.True, "A canonical reread confirms an unacknowledged intent commit.");
            Assert.That(recovered.TryGetAction(out first), Is.True);
            Assert.That(first.Kind, Is.EqualTo(VoiceEpochActionKind.CreateRoom));

            var persistedThenException = new FakeStore { ThrowAfterPersist = true };
            var exceptionRecovery = NewCoordinator(persistedThenException);
            Assert.That(exceptionRecovery.StartProvision(Epoch("a")), Is.True);
            Assert.That(exceptionRecovery.TryGetAction(out first), Is.True);
            Assert.That(exceptionRecovery.Complete(first, true), Is.True,
                "A commit exception is accepted only after the exact canonical intent is reread.");
        }

        [TestCase("null")]
        [TestCase("empty")]
        [TestCase("blank")]
        [TestCase("duplicate")]
        public void A_D05_MalformedExpectedInventoryIsNotNormalizedIntoAnAcceptedCandidate(string shape)
        {
            string[] rooms = null;
            if (shape == "empty") rooms = Array.Empty<string>();
            if (shape == "blank") rooms = new[] { "room-a", "" };
            if (shape == "duplicate") rooms = new[] { "room-a", "room-a" };
            var store = new FakeStore();
            var coordinator = new VoiceEpochCoordinator("world", "shift", epoch => rooms, store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.False);
            Assert.That(coordinator.HasPendingAction, Is.False);
            Assert.That(store.Commits, Is.Empty);
        }

        [Test]
        public void F6_CommitReadyAcknowledgementIsAcceptedOnlyWhenExactReadyCanonicalIsReread()
        {
            foreach (var mode in new[] { ReadyCommitMode.FalseWithoutPersist, ReadyCommitMode.FalseAfterPersist, ReadyCommitMode.ThrowAfterPersist })
            {
                var store = new ReadyCommitStore(mode);
                var coordinator = NewCoordinator(store);
                Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
                Drain(coordinator, action => true);
                Assert.That(coordinator.Ready, Is.EqualTo(mode != ReadyCommitMode.FalseWithoutPersist), mode.ToString());
                Assert.That(coordinator.Failed, Is.EqualTo(mode == ReadyCommitMode.FalseWithoutPersist), mode.ToString());
            }
        }

        [Test]
        public void F7_InvalidManifestPerformsNoRemoteMutation()
        {
            var invalid = Manifest("epoch-a", new[] { "foreign-room" }, true);
            invalid.WorldId = "foreign-world";
            var store = new FakeStore(invalid);
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("b")), Is.False);
            Assert.That(coordinator.InitialReadFailed, Is.False);
            VoiceEpochAction action;
            Assert.That(coordinator.TryGetAction(out action), Is.False);
            Assert.That(coordinator.HasPendingAction, Is.False);
            Assert.That(coordinator.Ready, Is.False);
        }

        [Test]
        public void A_D01_JsonUtilityRoundTripsEveryCanonicalFieldForReadyFalseAndTrue()
        {
            Assert.That(typeof(VoiceEpochManifest).IsDefined(typeof(SerializableAttribute), false), Is.True,
                "SOURCE_CONTRACT: the production persistence DTO must explicitly declare its Unity serialization contract.");
            foreach (var ready in new[] { false, true })
            {
                var source = Manifest("a", RoomsA, ready);
                var json = JsonUtility.ToJson(source);
                var restored = JsonUtility.FromJson<VoiceEpochManifest>(json);
                Assert.That(json, Does.Contain("\"Schema\":3"));
                Assert.That(restored.Schema, Is.EqualTo(VoiceEpochManifest.CurrentSchema));
                Assert.That(restored.WorldId, Is.EqualTo(source.WorldId));
                Assert.That(restored.ShiftId, Is.EqualTo(source.ShiftId));
                Assert.That(restored.Epoch, Is.EqualTo(source.Epoch));
                Assert.That(restored.Rooms, Is.EqualTo(source.Rooms));
                Assert.That(restored.PendingOperations, Is.EqualTo(source.PendingOperations));
                Assert.That(restored.Ready, Is.EqualTo(ready));
            }
        }

        [Test]
        public void AB_D02_PendingRevocationBlocksGrantEvenIfReadyWasRestoredByAnOldProvision()
        {
            var participant = new ParticipantState { ParticipantId = "pending-revoke", TeamId = "red", RoleId = "role-01" };
            var service = new GameObject("VoiceEpochGrantGate").AddComponent<ServerVoiceService>();
            try
            {
                SetField(service, "config", new VoiceServiceConfig { WebSocketUrl = "wss://voice.example" });
                SetField(service, "world", new WorldState { WorldId = "world", ShiftId = "shift", Participants = new[] { participant } });
                SetField(service, "issuer", new VoiceTokenIssuer("key", new string('s', 40), () => 1000));
                SetField(service, "roomState", Manifest("a", RoomsA, true));
                SetField(service, "rotationRequested", true);
                ((HashSet<string>)GetField(service, "revocations")).Add(participant.ParticipantId);
                SetField(service, "ready", true);

                Assert.That(service.Grant(participant, "team"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [TestCase(null)]
        [TestCase("")]
        public void A_D05_NullOrEmptyRawRoomIsRejected(string malformedRoom)
        {
            var store = new FakeStore(Manifest("a", new[] { "room-a", "room-command", malformedRoom }, true));
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("b")), Is.False);
            Assert.That(coordinator.HasPendingAction, Is.False);
        }

        [Test]
        public void A_D05_CommitAcknowledgementRequiresValidExactCanonicalReread()
        {
            var malformed = Manifest("a", new[] { "room-a", "room-command", "room-command" }, false);
            var store = new FakeStore { CommitResult = false, ReadAfterCommit = VoiceEpochStoreRead.Valid(malformed) };
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            Assert.That(coordinator.TryGetAction(out var action), Is.True);
            Assert.That(coordinator.Complete(action, true), Is.False);
            Assert.That(coordinator.Failed, Is.True);
        }

        [Test]
        public void A_D05_CommitRereadExceptionFailsClosedWithoutEscaping()
        {
            var store = new FakeStore { CommitResult = false, ThrowOnReadAfterCommit = true };
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            Assert.That(coordinator.TryGetAction(out var action), Is.True);
            Assert.DoesNotThrow(() => Assert.That(coordinator.Complete(action, true), Is.False));
            Assert.That(coordinator.Failed, Is.True);
        }

        [Test]
        public void A_D05_CommitTrueStillRequiresExactRoomOrderInCanonicalReread()
        {
            var reordered = Manifest("a", RoomsA.Reverse().ToArray(), false);
            var store = new FakeStore { CommitResult = true, ReadAfterCommit = VoiceEpochStoreRead.Valid(reordered) };
            var coordinator = NewCoordinator(store);
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            Assert.That(coordinator.TryGetAction(out var action), Is.True);
            Assert.That(coordinator.Complete(action, true), Is.False);
            Assert.That(coordinator.Failed, Is.True);
        }

        [Test]
        public void AB_D04_CoordinatorInitialReadExceptionIsControlledAndRawInvalidIsNotDurableAuthority()
        {
            var throwing = new FakeStore { ThrowOnInitialRead = true };
            var coordinator = NewCoordinator(throwing);
            Assert.DoesNotThrow(() => Assert.That(coordinator.StartProvision(Epoch("a")), Is.False));
            Assert.That(coordinator.InitialReadFailed, Is.True);
            Assert.That(coordinator.Ready, Is.False);
            throwing.ThrowOnInitialRead = false;
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            Assert.That(coordinator.InitialReadFailed, Is.False);

            var invalid = Manifest("a", new[] { "foreign-room" }, true);
            coordinator = NewCoordinator(new FakeStore(invalid));
            Assert.That(coordinator.DurableManifest, Is.Null);
        }

        [Test]
        public void AB_D02_CreateCompletionCannotRestoreReadyWhileRevocationRemoveIsPendingOrFailed()
        {
            var participant = Participant("inflight-revoke", "red");
            var store = new FakeStore();
            var transport = new FakeTransport { PauseNextCreate = true, PauseNextRemove = true };
            transport.RemoveResults.Enqueue(true);
            transport.RemoveResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            try
            {
                var driver = new CoroutineDriver(service.RetryFailedProvision());
                Assert.That(driver.MoveToTransportPause(), Is.EqualTo("CreateRoom"));
                service.RevokeConnection(participant.ParticipantId);
                Assert.That(service.Ready, Is.False);

                Assert.That(driver.MoveToTransportPause(), Is.EqualTo("RemoveParticipant"));
                Assert.That(service.Ready, Is.False, "The superseded create generation cannot publish readiness.");
                Assert.That(service.Grant(participant, "team"), Is.Null);

                driver.Finish();
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.CreateCount, Is.EqualTo(2));
                Assert.That(transport.RemoveCount, Is.EqualTo(2));

                transport.RemoveResults.Enqueue(true); // A not-found participant is an idempotent success at the adapter boundary.
                transport.RemoveResults.Enqueue(true);
                Drain(service.RetryFailedProvision());
                Assert.That(transport.RemoveCount, Is.EqualTo(4));
                Assert.That(service.Grant(participant, "team"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void F1_F3_FirstMiddleAndLastCreateFailureKeepWholeCandidateForExplicitRetry(int failureIndex)
        {
            var participants = new[] { Participant("matrix-red", "red"), Participant("matrix-blue", "blue") };
            var store = new FakeStore();
            var failedTransport = new FakeTransport();
            for (var i = 0; i < failureIndex; i++) failedTransport.CreateResults.Enqueue(true);
            failedTransport.CreateResults.Enqueue(false);
            var service = NewService(participants, store, failedTransport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                Assert.That(failedTransport.CreateCount, Is.EqualTo(failureIndex + 1));
                Assert.That(store.Read().Manifest.Rooms, Is.EquivalentTo(ServiceRooms(Epoch("b"), "red", "blue")));
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }

            var retryTransport = new FakeTransport();
            var retry = NewService(participants, store, retryTransport);
            try
            {
                Drain(retry.RetryFailedProvision());
                Assert.That(retryTransport.DeleteCount, Is.EqualTo(3));
                Assert.That(retryTransport.CreateCount, Is.EqualTo(3));
                Assert.That(retry.Ready, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(retry.gameObject); }
        }

        [Test]
        public void F2_F3_PartialCreateAndResponseLossRemainRetirableAcrossServiceReconstruction()
        {
            foreach (var failureIndex in new[] { 0, 1 })
            {
                var participant = Participant("partial-create-" + failureIndex, "red");
                var store = new FakeStore();
                var remoteRooms = new HashSet<string>(StringComparer.Ordinal);
                var operations = new List<string>();
                var firstTransport = new FakeTransport(remoteRooms, operations)
                {
                    SideEffectOnNextFailedCreate = true
                };
                for (var i = 0; i < failureIndex; i++) firstTransport.CreateResults.Enqueue(true);
                firstTransport.CreateResults.Enqueue(false);
                var first = NewService(participant, store, firstTransport);
                VoiceEpochManifest durable;
                try
                {
                    Drain(first.RetryFailedProvision());
                    Assert.That(first.Ready, Is.False);
                    Assert.That(firstTransport.CreateCount, Is.EqualTo(failureIndex + 1));
                    durable = store.Read().Manifest.Clone();
                    Assert.That(durable.Ready, Is.False);
                    Assert.That(remoteRooms, Is.Not.Empty,
                        "The response-loss branch must preserve the actual remote side effect across reconstruction.");
                }
                finally { UnityEngine.Object.DestroyImmediate(first.gameObject); }

                var reconstructedStore = new FakeStore(durable);
                var retirementTransport = new FakeTransport(remoteRooms, operations);
                retirementTransport.DeleteResults.Enqueue(true);
                retirementTransport.DeleteResults.Enqueue(false);
                var reconstructed = NewService(participant, reconstructedStore, retirementTransport);
                try
                {
                    Drain(reconstructed.RetireRooms());
                    Assert.That(reconstructed.RetiredSuccessfully, Is.False);
                    Assert.That(retirementTransport.DeleteCount, Is.EqualTo(2));
                    Assert.That(reconstructed.Grant(participant, "team"), Is.Null);

                    retirementTransport.DeleteResults.Enqueue(true);
                    retirementTransport.DeleteResults.Enqueue(true);
                    Drain(reconstructed.RetireRooms());
                    Assert.That(reconstructed.RetiredSuccessfully, Is.True);
                    Assert.That(retirementTransport.DeleteCount, Is.EqualTo(4));
                    Assert.That(remoteRooms, Is.Empty);
                }
                finally { UnityEngine.Object.DestroyImmediate(reconstructed.gameObject); }
            }
        }

        [Test]
        public void F2_ShutdownRequestedDuringInflightCreateRetiresTheDurableWholeCandidate()
        {
            var participant = Participant("shutdown-inflight", "red");
            var store = new FakeStore();
            var transport = new FakeTransport { PauseNextCreate = true };
            var service = NewService(participant, store, transport);
            try
            {
                var provision = new CoroutineDriver(service.RetryFailedProvision());
                Assert.That(provision.MoveToTransportPause(), Is.EqualTo("CreateRoom"));
                var retirement = service.RetireRooms();
                Assert.That(retirement.MoveNext(), Is.True, "Retirement waits for the in-flight request.");
                provision.Finish();
                Drain(retirement);
                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.DeleteCount, Is.EqualTo(2));
                Assert.That(service.Grant(participant, "team"), Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void AB_D03_InvalidCanonicalBlocksServiceCreateDeleteAndRemoveMutations()
        {
            var participant = Participant("invalid-owner", "red");
            foreach (var invalid in InvalidManifests())
            {
                var store = new FakeStore(invalid);
                var transport = new FakeTransport();
                var service = NewService(participant, store, transport);
                try
                {
                    Drain(service.RetryFailedProvision());
                    service.RevokeConnection(participant.ParticipantId);
                    Drain(service.RetireRooms());
                    Assert.That(transport.CreateCount, Is.Zero);
                    Assert.That(transport.DeleteCount, Is.Zero);
                    Assert.That(transport.RemoveCount, Is.Zero);
                    Assert.That(service.DurableManifest, Is.Null);
                }
                finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
            }
        }

        [Test]
        public void A_D01_ProductionStoreUtf8RoundTripsReadyFalseAndTrueThroughFreshInstances()
        {
            var files = new FakeFiles();
            foreach (var ready in new[] { false, true })
            {
                var source = Manifest("a", RoomsA, ready);
                Assert.That(new PersistentVoiceEpochStore("canonical", files).TryCommit(source), Is.True);
                var restored = new PersistentVoiceEpochStore("canonical", files).Read().Manifest;
                Assert.That(restored.Schema, Is.EqualTo(3));
                Assert.That(restored.WorldId, Is.EqualTo("world"));
                Assert.That(restored.ShiftId, Is.EqualTo("shift"));
                Assert.That(restored.Epoch, Is.EqualTo(Epoch("a")));
                Assert.That(restored.Rooms, Is.EqualTo(RoomsA));
                Assert.That(restored.Ready, Is.EqualTo(ready));
                Assert.That(files.Canonical, Is.EqualTo(Encoding.UTF8.GetBytes(JsonUtility.ToJson(source))));
            }
        }

        [Test]
        public void AB_D04_ExplicitNotFoundIsMissingButReadUncertaintyFailsClosedAndRecovers()
        {
            var files = new FakeFiles();
            var store = new PersistentVoiceEpochStore("canonical", files);
            files.ThrowReadCount = 1;
            Assert.That(store.Read().Readable, Is.False);
            files.Reads.Enqueue(null);
            Assert.That(store.Read().Readable, Is.False);
            files.Reads.Enqueue(FileReadResult.Failure());
            Assert.That(store.Read().Readable, Is.False);
            files.Reads.Enqueue(FileReadResult.NotFound());
            Assert.That(store.Read().Exists, Is.False);
            files.Reads.Enqueue(FileReadResult.Success(Encoding.UTF8.GetBytes("{not-json")));
            Assert.That(store.Read().Readable, Is.False);
            files.Reads.Enqueue(FileReadResult.Success(Encoding.UTF8.GetBytes(JsonUtility.ToJson(Manifest("a", RoomsA, true)))));
            Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("a")));
        }

        [Test]
        public void AB_D04_D05_CommitUsesValidatedCanonicalAcrossMoveReplaceAndAcknowledgementUncertainty()
        {
            foreach (var boundary in Enum.GetValues(typeof(FileCommitBoundary)).Cast<FileCommitBoundary>())
            {
                var files = new FakeFiles { Boundary = boundary };
                var store = new PersistentVoiceEpochStore("canonical", files);
                var manifest = Manifest("a", RoomsA, false);
                var result = store.TryCommit(manifest);
                var reconstructed = new PersistentVoiceEpochStore("canonical", files);
                var reread = reconstructed.Read();
                Assert.That(result, Is.EqualTo(reread.Exists && reread.Readable), boundary.ToString());
                if (result)
                {
                    Assert.That(reread.Manifest.Epoch, Is.EqualTo(manifest.Epoch));
                    Assert.That(reread.Manifest.Ready, Is.False);
                    Assert.That(reread.Manifest.Rooms, Is.EqualTo(RoomsA));
                }
            }

            var existing = new FakeFiles();
            existing.Seed(JsonUtility.ToJson(Manifest("a", RoomsA, true)));
            existing.Boundary = FileCommitBoundary.AfterMoveOrReplace;
            var replacement = Manifest("b", RoomsB, false);
            Assert.That(new PersistentVoiceEpochStore("canonical", existing).TryCommit(replacement), Is.True);
            Assert.That(new PersistentVoiceEpochStore("canonical", existing).Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));
        }

        [TestCase("Schema")]
        [TestCase("WorldId")]
        [TestCase("ShiftId")]
        [TestCase("Epoch")]
        [TestCase("Rooms")]
        [TestCase("PendingOperations")]
        [TestCase("Ready")]
        public void A_D01_ProductionStoreRejectsAnyMissingPersistenceField(string field)
        {
            var json = JsonUtility.ToJson(Manifest("a", RoomsA, true));
            json = RemoveJsonField(json, field);
            var files = new FakeFiles();
            files.Seed(json);
            Assert.That(new PersistentVoiceEpochStore("canonical", files).Read().Readable, Is.False, field);
        }

        [Test]
        public void A_D05_ProductionStoreRejectsNullEmptyDuplicateAndMalformedInventory()
        {
            foreach (var rooms in new[]
            {
                (string[])null,
                Array.Empty<string>(),
                new[] { "room-a", "" },
                new[] { "room-a", (string)null },
                new[] { "room-a", "room-a" }
            })
            {
                var files = new FakeFiles();
                var store = new PersistentVoiceEpochStore("canonical", files);
                Assert.That(store.TryCommit(Manifest("a", rooms, false)), Is.False);
                Assert.That(files.CommitCount, Is.Zero);
            }
        }

        [TestCase("CreateRoom", UnityWebRequest.Result.ProtocolError, 404, false)]
        [TestCase("DeleteRoom", UnityWebRequest.Result.ProtocolError, 404, true)]
        [TestCase("RemoveParticipant", UnityWebRequest.Result.ProtocolError, 404, true)]
        [TestCase("DeleteRoom", UnityWebRequest.Result.ConnectionError, 404, false)]
        [TestCase("RemoveParticipant", UnityWebRequest.Result.DataProcessingError, 404, false)]
        [TestCase("CreateRoom", UnityWebRequest.Result.Success, 200, true)]
        public void F5_HttpResultClassifiesOnlySuccessfulOrIdempotentNotFoundMutations(
            string method, UnityWebRequest.Result result, long responseCode, bool expected)
        {
            Assert.That(ServerVoiceService.IsSuccessfulResponse(method, result, responseCode), Is.EqualTo(expected));
        }

        [Test]
        public void AB_D04_MissingRaceNeverReplacesNewInvalidOrDifferentCanonical()
        {
            foreach (var competing in new[]
            {
                "{not-json",
                JsonUtility.ToJson(Manifest("b", RoomsB, true))
            })
            {
                var files = new FakeFiles();
                var store = new PersistentVoiceEpochStore("canonical", files);
                Assert.That(store.Read().Exists, Is.False);
                files.Seed(competing);
                Assert.That(store.TryCommit(Manifest("a", RoomsA, false)), Is.False);
                Assert.That(files.CommitCount, Is.Zero);
            }
        }

        [Test]
        public void F5_JwtPayloadPreservesIdentityRoomJoinRoleAndMicrophoneOnlyForNinetySeconds()
        {
            var issuer = new VoiceTokenIssuer("key", new string('s', 40), () => 1000);
            var trainee = Participant("jwt-trainee", "red");
            var instructor = Participant("jwt-instructor", "red"); instructor.IsInstructor = true;
            foreach (var pair in new[]
            {
                new { Grant = issuer.Join("world", "shift", Epoch("a"), trainee, "team", "wss://voice.example"), Publish = true },
                new { Grant = issuer.Join("world", "shift", Epoch("a"), trainee, "command", "wss://voice.example"), Publish = false },
                new { Grant = issuer.Join("world", "shift", Epoch("a"), instructor, "command", "wss://voice.example"), Publish = true }
            })
            {
                var payload = DecodeJwtPayload(pair.Grant.Token);
                Assert.That(payload, Does.Contain("\"sub\":\"" + (pair.Publish && pair.Grant.Channel == "command" ? "jwt-instructor" : "jwt-trainee") + "\""));
                Assert.That(payload, Does.Contain("\"roomJoin\":true"));
                Assert.That(payload, Does.Contain("\"room\":\"" + pair.Grant.Room + "\""));
                Assert.That(payload, Does.Contain("\"canPublish\":" + pair.Publish.ToString().ToLowerInvariant()));
                Assert.That(payload, Does.Contain("\"canPublishSources\":[\"microphone\"]"));
                Assert.That(payload, Does.Contain("\"canPublishData\":false"));
                Assert.That(payload, Does.Contain("\"canUpdateOwnMetadata\":false"));
                Assert.That(payload, Does.Not.Contain("camera"));
                Assert.That(pair.Grant.ExpiresAt, Is.EqualTo(1090));
            }

            var baseGrant = issuer.Join("world", "shift", Epoch("a"), trainee, "team", "wss://voice.example");
            Assert.That(issuer.Join("other-world", "shift", Epoch("a"), trainee, "team", "wss://voice.example").Room,
                Is.Not.EqualTo(baseGrant.Room));
            Assert.That(issuer.Join("world", "other-shift", Epoch("a"), trainee, "team", "wss://voice.example").Room,
                Is.Not.EqualTo(baseGrant.Room));
            Assert.That(issuer.Join("world", "shift", Epoch("a"), Participant("jwt-blue", "blue"), "team", "wss://voice.example").Room,
                Is.Not.EqualTo(baseGrant.Room));
        }

        [Test]
        public void AB_G01_G02_ServiceRetryRetiresPossibleInventoryBeforeCreateAndKeepsGrantsClosedOnFailure()
        {
            var participant = Participant("retry-owner", "red");
            var store = new FakeStore(Manifest("a", ServiceRooms(Epoch("a"), "red"), true));
            var transport = new FakeTransport();
            transport.DeleteResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(transport.DeleteCount, Is.EqualTo(1));
                Assert.That(transport.CreateCount, Is.Zero);
                Assert.That(service.Grant(participant, "team"), Is.Null);

                transport.DeleteResults.Enqueue(true);
                transport.DeleteResults.Enqueue(true);
                Drain(service.RetryFailedProvision());
                Assert.That(transport.CreateCount, Is.EqualTo(2));
                Assert.That(service.Grant(participant, "team"), Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B01_InitialPrecommitFailureWithKnownDisconnectRecoversThroughExplicitRetry()
        {
            var participant = Participant("initial-precommit", "red");
            var store = new FakeStore { CommitResult = false, PersistOnFailedCommit = false };
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                Assert.That(store.Read().Exists, Is.False);
                Assert.That(transport.CreateCount, Is.Zero);

                service.RevokeConnection(participant.ParticipantId);
                Assert.That(service.Ready, Is.False);
                store.CommitResult = true;
                Drain(service.RetryFailedProvision());

                Assert.That(service.Ready, Is.True);
                Assert.That(service.Grant(participant, "team"), Is.Not.Null);
                Assert.That(transport.RemoveCount, Is.Zero,
                    "A participant cannot exist in voice rooms that were proven never to have been created.");
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(ServiceRooms(Epoch("b"), "red")));
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B01_KnownEmptyRecoveryNeverAppliesAfterAuthorityLossOrCommitUncertainty()
        {
            var participant = Participant("sticky-authority", "red");

            var authoritativeStore = new FakeStore(Manifest("a", ServiceRooms(Epoch("a"), "red"), true));
            var authoritativeTransport = new FakeTransport { PauseNextCreate = true };
            authoritativeTransport.SeedRooms(ServiceRooms(Epoch("a"), "red"));
            var authoritative = NewService(participant, authoritativeStore, authoritativeTransport);
            try
            {
                var authorityProvision = new CoroutineDriver(authoritative.RetryFailedProvision());
                Assert.That(authorityProvision.MoveToTransportPause(), Is.EqualTo("CreateRoom"));
                Assert.That(authoritativeStore.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")),
                    "The new full intent is canonical before any create side effect.");
                authorityProvision.Finish();
                Assert.That(authoritative.Ready, Is.True);
                authoritativeStore.ClearCanonical();
                var before = authoritativeTransport.TotalMutationCount;
                Drain(authoritative.RetryFailedProvision());
                Assert.That(authoritative.Ready, Is.False);
                Assert.That(authoritativeTransport.TotalMutationCount, Is.EqualTo(before),
                    "An unexpected disappearance stays closed even without a queued revocation.");
                authoritative.RevokeConnection(participant.ParticipantId);
                Drain(authoritative.RetryFailedProvision());
                Assert.That(authoritative.Ready, Is.False);
                Assert.That(authoritativeTransport.TotalMutationCount, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(authoritative.gameObject); }

            foreach (var uncertainStore in new[]
            {
                new FakeStore
                {
                    CommitResult = false,
                    PersistOnFailedCommit = false,
                    ReadAfterCommit = VoiceEpochStoreRead.Invalid()
                },
                new FakeStore
                {
                    CommitResult = false,
                    PersistOnFailedCommit = false,
                    ThrowOnReadAfterCommit = true
                },
                new FakeStore
                {
                    CommitResult = false,
                    ThrowAfterPersist = true,
                    ReadAfterCommit = VoiceEpochStoreRead.Missing()
                },
                new FakeStore
                {
                    CommitResult = true,
                    ReadAfterCommit = VoiceEpochStoreRead.Missing()
                }
            })
            {
                var uncertainTransport = new FakeTransport();
                var uncertain = NewService(participant, uncertainStore, uncertainTransport);
                try
                {
                    Drain(uncertain.RetryFailedProvision());
                    uncertainStore.ReadAfterCommit = null;
                    uncertainStore.ThrowOnReadAfterCommit = false;
                    uncertainStore.CommitResult = true;
                    uncertain.RevokeConnection(participant.ParticipantId);
                    Drain(uncertain.RetryFailedProvision());
                    Assert.That(uncertain.Ready, Is.False);
                    Assert.That(uncertainTransport.TotalMutationCount, Is.Zero);
                }
                finally { UnityEngine.Object.DestroyImmediate(uncertain.gameObject); }
            }
        }

        [Test]
        public void B01_MissingCanonicalAfterPossibleCreateEffectDoesNotBecomeKnownEmpty()
        {
            var participant = Participant("possible-effect", "red");
            var store = new FakeStore();
            var transport = new FakeTransport { SideEffectOnNextFailedCreate = true };
            transport.CreateResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(transport.RemoteRooms, Is.Not.Empty);
                store.ClearCanonical();
                var before = transport.TotalMutationCount;
                service.RevokeConnection(participant.ParticipantId);
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                Assert.That(transport.TotalMutationCount, Is.EqualTo(before));
                Assert.That(transport.RemoteRooms, Is.Not.Empty,
                    "Possible external rooms remain visible in the stateful fake rather than being erased by counters.");
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B02_ConfigureIsRejectedBeforeReplacingAnOwnerWithDelayedCreateAfterDelete404()
        {
            var participant = Participant("configure-owner", "red");
            var oldRooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", oldRooms, true));
            var transport = new FakeTransport { PauseNextCreate = true };
            var service = NewService(participant, store, transport, () => Epoch("b"));
            try
            {
                var driver = new CoroutineDriver(service.RetryFailedProvision());
                Assert.That(driver.MoveToTransportPause(), Is.EqualTo("CreateRoom"));
                Assert.That(transport.Operations.Count(value => value.EndsWith(":404", StringComparison.Ordinal)),
                    Is.EqualTo(oldRooms.Length), "The fake records actual absent-room 404 state before the delayed create effect.");

                var replacementTransport = new FakeTransport();
                var replacementStore = new FakeStore();
                Assert.Throws<InvalidOperationException>(() => service.ConfigureForTests(
                    TestConfig(), TestWorld(participant), replacementStore, replacementTransport,
                    () => Epoch("c"), () => 1000));
                Assert.That(replacementTransport.TotalMutationCount, Is.Zero);
                Assert.DoesNotThrow(driver.Finish);

                Assert.That(service.Ready, Is.True);
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(ServiceRooms(Epoch("b"), "red")));
                Assert.That(transport.RemoteRooms.Any(room => room.EndsWith(Epoch("c"), StringComparison.Ordinal)), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B02_DuplicateRetireIsRejectedWithoutReplacingTheActiveCoordinator()
        {
            var participant = Participant("retire-owner", "red");
            var store = new FakeStore();
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.True);
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(ServiceRooms(Epoch("b"), "red")));

                transport.PauseNextDelete = true;
                var first = new CoroutineDriver(service.RetireRooms());
                Assert.That(first.MoveToTransportPause(), Is.EqualTo("DeleteRoom"));
                var duplicate = service.RetireRooms();
                Assert.That(duplicate.MoveNext(), Is.False, "The active retirement remains the sole action owner.");
                Assert.DoesNotThrow(first.Finish);

                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [TestCase("DeleteRoom")]
        [TestCase("CreateRoom")]
        [TestCase("RemoveParticipant")]
        public void B02_EveryTransportPauseKeepsOneLifecycleOwnerAndClosesGrants(string pauseMethod)
        {
            var participants = new[] { Participant("pause-red", "red"), Participant("pause-blue", "blue") };
            var oldRooms = ServiceRooms(Epoch("a"), "red", "blue");
            var store = new FakeStore(Manifest("a", oldRooms, true));
            var transport = new FakeTransport();
            transport.SeedRooms(oldRooms);
            transport.PauseNextDelete = pauseMethod == "DeleteRoom";
            transport.PauseNextCreate = pauseMethod == "CreateRoom" || pauseMethod == "RemoveParticipant";
            transport.PauseNextRemove = pauseMethod == "RemoveParticipant";
            var service = NewService(participants, store, transport, () => Epoch("b"));
            try
            {
                var owner = new CoroutineDriver(service.RetryFailedProvision());
                var observedPause = owner.MoveToTransportPause();
                if (pauseMethod == "RemoveParticipant")
                {
                    Assert.That(observedPause, Is.EqualTo("CreateRoom"));
                    service.RevokeConnection(participants[0].ParticipantId);
                    observedPause = owner.MoveToTransportPause();
                }
                Assert.That(observedPause, Is.EqualTo(pauseMethod));

                var competingTransport = new FakeTransport();
                Assert.Throws<InvalidOperationException>(() => service.ConfigureForTests(
                    TestConfig(), TestWorld(participants), new FakeStore(), competingTransport,
                    () => Epoch("c"), () => 1000));
                Drain(service.RetryFailedProvision());
                service.RevokeConnection(participants[1].ParticipantId);
                Assert.That(service.Grant(participants[0], "team"), Is.Null);

                var retirement = service.RetireRooms();
                Assert.That(retirement.MoveNext(), Is.True, "Retirement joins behind the active provision owner.");
                var duplicateRetirement = service.RetireRooms();
                Assert.That(duplicateRetirement.MoveNext(), Is.False);
                Assert.DoesNotThrow(owner.Finish);
                Drain(retirement);

                Assert.That(service.Ready, Is.False);
                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty, "No possible epoch inventory is dropped at " + pauseMethod);
                Assert.That(competingTransport.TotalMutationCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D01_FreshKnownEmptyRetirementSucceedsWithoutRemoteMutation()
        {
            var participant = Participant("fresh-retire", "red");
            var transport = new FakeTransport();
            var service = NewService(participant, new FakeStore(), transport);
            try
            {
                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.TotalMutationCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D01_KnownEmptyPrecommitFailureStillRetiresWithoutRemoteMutation()
        {
            var participant = Participant("known-empty-retire", "red");
            var store = new FakeStore { CommitResult = false, PersistOnFailedCommit = false };
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                Assert.That(store.Read().Exists, Is.False);
                Assert.That(transport.TotalMutationCount, Is.Zero);

                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.True,
                    "A proven precommit failure has no possible external room side effect.");
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.TotalMutationCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D01_ReadyThenMissingRequiresCanonicalRecoveryBeforeExactRetirement()
        {
            var participant = Participant("ready-missing", "red");
            var store = new FakeStore();
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport, () => Epoch("b"));
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.True);
                var owned = store.Read().Manifest.Clone();
                store.ClearCanonical();
                var mutationsBeforeMissingRetire = transport.TotalMutationCount;

                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.False);
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsBeforeMissingRetire));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(owned.Rooms));

                store.RestoreCanonical(owned);
                transport.RemoteRooms.Remove(owned.Rooms[0]);
                var operationStart = transport.Operations.Count;
                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty);
                Assert.That(transport.Operations.Skip(operationStart), Is.EqualTo(new[]
                {
                    "DeleteRoom:" + owned.Rooms[0] + ":404",
                    "DeleteRoom:" + owned.Rooms[1] + ":ok"
                }));
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D01_MissingAfterPossibleFailedCreateEffectCannotReportRetired()
        {
            var participant = Participant("failed-create-missing", "red");
            var store = new FakeStore();
            var transport = new FakeTransport { SideEffectOnNextFailedCreate = true };
            transport.CreateResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(transport.RemoteRooms, Is.Not.Empty);
                store.ClearCanonical();
                var mutationsBeforeRetire = transport.TotalMutationCount;

                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.False);
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsBeforeRetire));
                Assert.That(transport.RemoteRooms, Is.Not.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D01_MissingAfterUncertainIntentCommitCannotReportRetired()
        {
            var participant = Participant("uncertain-commit-missing", "red");
            var store = new FakeStore
            {
                CommitResult = true,
                ReadAfterCommit = VoiceEpochStoreRead.Missing()
            };
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                store.ReadAfterCommit = null;
                store.ClearCanonical();

                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.False);
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Grant(participant, "team"), Is.Null);
                Assert.That(transport.TotalMutationCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void A_D02_TwoPreadvancedRetryParentsKeepOneCoordinatorAndOneEpoch()
        {
            var participant = Participant("dual-retry", "red");
            var epochs = new Queue<string>(new[] { Epoch("b"), Epoch("c") });
            var store = new FakeStore();
            var transport = new FakeTransport { PauseNextCreate = true };
            var service = NewService(participant, store, transport, () => epochs.Dequeue());
            IEnumerator firstParent = null;
            IEnumerator secondParent = null;
            CoroutineDriver firstChild = null;
            CoroutineDriver secondChild = null;
            try
            {
                firstParent = service.RetryFailedProvision();
                secondParent = service.RetryFailedProvision();
                Assert.That(firstParent.MoveNext(), Is.True);
                Assert.That(secondParent.MoveNext(), Is.True);
                firstChild = new CoroutineDriver((IEnumerator)firstParent.Current);
                secondChild = new CoroutineDriver((IEnumerator)secondParent.Current);

                Assert.That(firstChild.MoveToTransportPause(), Is.EqualTo("CreateRoom"));
                var mutationsAtPause = transport.TotalMutationCount;
                Assert.DoesNotThrow(secondChild.Finish);
                secondChild.Dispose();

                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsAtPause));
                Assert.That((bool)GetField(service, "provisioning"), Is.True,
                    "A rejected or disposed non-owner must not clear the active owner's flag.");
                Assert.DoesNotThrow(firstChild.Finish);
                Assert.That(service.Ready, Is.True);
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(ServiceRooms(Epoch("b"), "red")));
                Assert.That(transport.RemoteRooms.Any(room => room.EndsWith(Epoch("c"), StringComparison.Ordinal)), Is.False);
                Assert.That(epochs.Count, Is.EqualTo(1), "The rejected child must not consume another epoch identity.");
            }
            finally
            {
                firstChild?.Dispose();
                secondChild?.Dispose();
                DisposeEnumerator(firstParent);
                DisposeEnumerator(secondParent);
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void A_D02_RetirementStartedBeforeYieldedRetryChildBlocksQueuedRevocationMutation()
        {
            var participant = Participant("retire-before-child", "red");
            var rooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", rooms, true));
            var transport = new FakeTransport();
            transport.SeedRooms(rooms);
            transport.DeleteResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            IEnumerator retryParent = null;
            CoroutineDriver retryChild = null;
            CoroutineDriver retirement = null;
            try
            {
                Drain(service.RetryFailedProvision());
                Assert.That(service.Ready, Is.False);
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(rooms),
                    "The failed delete is the before-side-effect branch, leaving validated inventory recoverable.");
                service.RevokeConnection(participant.ParticipantId);
                transport.PauseNextDelete = true;
                retryParent = service.RetryFailedProvision();
                Assert.That(retryParent.MoveNext(), Is.True);
                retryChild = new CoroutineDriver((IEnumerator)retryParent.Current);

                retirement = new CoroutineDriver(service.RetireRooms());
                Assert.That(retirement.MoveToTransportPause(), Is.EqualTo("DeleteRoom"));
                var mutationsAtRetirePause = transport.TotalMutationCount;
                Assert.DoesNotThrow(retryChild.Finish);

                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsAtRetirePause));
                Assert.That(transport.RemoveCount, Is.Zero);
                Assert.DoesNotThrow(retirement.Finish);
                Assert.That(transport.DeleteCount, Is.EqualTo(rooms.Length + 1));
                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty);
            }
            finally
            {
                retryChild?.Dispose();
                retirement?.Dispose();
                DisposeEnumerator(retryParent);
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [TestCase("DeleteRoom")]
        [TestCase("CreateRoom")]
        [TestCase("RemoveParticipant")]
        public void A_D02_PreadvancedDuplicateCannotMutateOrReleaseOwnerAtTransportPause(string pauseMethod)
        {
            var participant = Participant("pause-owner", "red");
            var oldRooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", oldRooms, true));
            var transport = new FakeTransport
            {
                PauseNextDelete = pauseMethod == "DeleteRoom",
                PauseNextCreate = pauseMethod == "CreateRoom" || pauseMethod == "RemoveParticipant",
                PauseNextRemove = pauseMethod == "RemoveParticipant"
            };
            transport.SeedRooms(oldRooms);
            var service = NewService(participant, store, transport, () => Epoch("b"));
            IEnumerator firstParent = null;
            IEnumerator secondParent = null;
            CoroutineDriver owner = null;
            CoroutineDriver duplicate = null;
            try
            {
                if (pauseMethod == "RemoveParticipant")
                {
                    transport.DeleteResults.Enqueue(false);
                    Drain(service.RetryFailedProvision());
                    Assert.That(service.Ready, Is.False);
                    Assert.That(transport.RemoteRooms, Is.EquivalentTo(oldRooms));
                    service.RevokeConnection(participant.ParticipantId);
                }
                firstParent = service.RetryFailedProvision();
                secondParent = service.RetryFailedProvision();
                Assert.That(firstParent.MoveNext(), Is.True);
                Assert.That(secondParent.MoveNext(), Is.True);
                owner = new CoroutineDriver((IEnumerator)firstParent.Current);
                duplicate = new CoroutineDriver((IEnumerator)secondParent.Current);

                var observedPause = owner.MoveToTransportPause();
                if (pauseMethod == "RemoveParticipant" && observedPause == "CreateRoom")
                    observedPause = owner.MoveToTransportPause();
                Assert.That(observedPause, Is.EqualTo(pauseMethod));
                var mutationsAtPause = transport.TotalMutationCount;

                Assert.DoesNotThrow(duplicate.Finish);
                duplicate.Dispose();
                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsAtPause));
                Assert.That((bool)GetField(service, "provisioning"), Is.True);
                Assert.DoesNotThrow(owner.Finish);
                Assert.That(service.Ready, Is.True);
                if (pauseMethod == "RemoveParticipant")
                {
                    Assert.That(transport.Operations.Where(value => value.StartsWith("RemoveParticipant:", StringComparison.Ordinal)),
                        Is.EqualTo(oldRooms.Select(room => "RemoveParticipant:" + room + ":" + participant.ParticipantId + ":ok")));
                }
            }
            finally
            {
                owner?.Dispose();
                duplicate?.Dispose();
                DisposeEnumerator(firstParent);
                DisposeEnumerator(secondParent);
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void A_D01_FailedDeleteFakePreservesWhetherTheRemoteSideEffectOccurred(bool sideEffect)
        {
            var participant = Participant("delete-effect-" + sideEffect, "red");
            var rooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", rooms, false));
            var transport = new FakeTransport { SideEffectOnNextFailedDelete = sideEffect };
            transport.SeedRooms(rooms);
            transport.DeleteResults.Enqueue(false);
            var service = NewService(participant, store, transport);
            try
            {
                Drain(service.RetireRooms());

                Assert.That(service.RetiredSuccessfully, Is.False);
                Assert.That(transport.RemoteRooms.Contains(rooms.OrderBy(room => room, StringComparer.Ordinal).First()),
                    Is.EqualTo(!sideEffect));
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B_D01_DetachedUnstartedCreateCallCannotDispatchAfterReplacementEpochCommits()
        {
            var participant = Participant("detached-call", "red");
            var epochs = new Queue<string>(new[] { Epoch("b"), Epoch("c") });
            var store = new FakeStore();
            var transport = new FakeTransport();
            var service = NewService(participant, store, transport, () => epochs.Dequeue());
            SplitProvision split = null;
            try
            {
                split = CaptureFirstCreateCall(service);
                var abandonedRooms = store.Read().Manifest.Rooms.ToArray();
                split.DisposeParents();

                Drain(service.RetryFailedProvision());
                var current = store.Read().Manifest.Clone();
                Assert.That(current.Epoch, Is.EqualTo(Epoch("c")));
                Assert.That(service.Ready, Is.True);
                var mutationsAfterReplacement = transport.TotalMutationCount;

                Drain(split.Call);

                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsAfterReplacement),
                    "An unstarted child from the released lease must make no stale dispatch.");
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(current.Rooms));
                Assert.That(transport.RemoteRooms.Intersect(abandonedRooms), Is.Empty);
            }
            finally
            {
                split?.Dispose();
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void B_D01_DetachedStartedTransportCallSettlesBeforeReplacementAndLeavesOnlyCanonicalRooms()
        {
            var participant = Participant("detached-transport", "red");
            var epochs = new Queue<string>(new[] { Epoch("b"), Epoch("c"), Epoch("d") });
            var store = new FakeStore();
            var transport = new FakeTransport { PauseNextCreate = true };
            var service = NewService(participant, store, transport, () => epochs.Dequeue());
            SplitProvision split = null;
            try
            {
                split = CaptureFirstCreateCall(service);
                Assert.That(split.Call.MoveNext(), Is.True);
                Assert.That(split.Call.Current, Is.Not.InstanceOf<IEnumerator>(),
                    "A raw transport iterator must not escape the guarded call boundary.");
                split.DisposeParents();

                Drain(service.RetryFailedProvision());
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")),
                    "A possibly dispatched call keeps its durable epoch authoritative until it settles.");
                Assert.That(service.Ready, Is.False);
                var retirement = service.RetireRooms();
                Assert.That(retirement.MoveNext(), Is.True,
                    "Retirement must join rather than race a possible remote effect.");

                Drain(split.Call);
                Drain(retirement);
                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty);
                var retiredCanonical = store.Read().Manifest;
                var mutationsAfterRetirement = transport.TotalMutationCount;

                Drain(service.RetryFailedProvision());

                Assert.That(service.Ready, Is.False,
                    "A retired service remains closed rather than racing a new epoch after retirement completion.");
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(retiredCanonical.Epoch));
                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsAfterRetirement));
            }
            finally
            {
                split?.Dispose();
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void B_D01_NestedTransportAdvancementStaysInsideGuardedCallUntilSettlement()
        {
            var participant = Participant("detached-nested", "red");
            var epochs = new Queue<string>(new[] { Epoch("b"), Epoch("c"), Epoch("d") });
            var store = new FakeStore();
            var transport = new FakeTransport { NestNextCall = true, PauseNextCreate = true };
            var service = NewService(participant, store, transport, () => epochs.Dequeue());
            SplitProvision split = null;
            try
            {
                split = CaptureFirstCreateCall(service);
                Assert.That(split.Call.MoveNext(), Is.True);
                Assert.That(split.Call.Current, Is.Not.InstanceOf<IEnumerator>(),
                    "Nested transport iterators must not escape as separately advanceable raw children.");
                split.DisposeParents();

                Drain(service.RetryFailedProvision());
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(service.Ready, Is.False);

                Drain(split.Call);
                Drain(service.RetryFailedProvision());

                var canonical = store.Read().Manifest;
                Assert.That(canonical.Epoch, Is.EqualTo(Epoch("c")));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(canonical.Rooms));
                Assert.That(service.Ready, Is.True);
            }
            finally
            {
                split?.Dispose();
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void B_D01_DispatchedCreateWithLateCallbackBlocksReplacementUntilExplicitSettlement()
        {
            var participant = Participant("late-create-callback", "red");
            var epochs = new Queue<string>(new[] { Epoch("b"), Epoch("c") });
            var store = new FakeStore();
            var transport = new FakeTransport
            {
                DelayNextCallback = true,
                SideEffectOnNextFailedCreate = true
            };
            transport.CreateResults.Enqueue(false);
            var service = NewService(participant, store, transport, () => epochs.Dequeue());
            try
            {
                Drain(service.RetryFailedProvision());
                var failedIntent = store.Read().Manifest.Clone();
                Assert.That(failedIntent.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(transport.RemoteRooms, Is.Not.Empty);
                Assert.That(transport.PendingCallbackCount, Is.EqualTo(1));
                var mutationsBeforeSettlement = transport.TotalMutationCount;

                Drain(service.RetryFailedProvision());

                Assert.That(transport.TotalMutationCount, Is.EqualTo(mutationsBeforeSettlement));
                Assert.That(store.Read().Manifest.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(service.Ready, Is.False);

                transport.CompleteNextCallback();
                Drain(service.RetryFailedProvision());

                var canonical = store.Read().Manifest;
                Assert.That(canonical.Epoch, Is.EqualTo(Epoch("c")));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(canonical.Rooms));
                Assert.That(service.Ready, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [Test]
        public void B_D01_DetachedUnstartedRemoveCannotDispatchAfterItsLeaseIsReplaced()
        {
            var participant = Participant("detached-remove", "red");
            var oldRooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", oldRooms, true));
            var transport = new FakeTransport();
            transport.SeedRooms(oldRooms);
            var service = NewService(participant, store, transport, () => Epoch("b"));
            IEnumerator retryParent = null;
            IEnumerator loop = null;
            IEnumerator oldRemove = null;
            try
            {
                ((HashSet<string>)GetField(service, "revocations")).Add(participant.ParticipantId);
                SetField(service, "rotationRequested", true);
                retryParent = service.RetryFailedProvision();
                Assert.That(retryParent.MoveNext(), Is.True);
                loop = (IEnumerator)retryParent.Current;
                Assert.That(loop.MoveNext(), Is.True);
                oldRemove = (IEnumerator)loop.Current;
                DisposeEnumerator(loop);
                DisposeEnumerator(retryParent);

                Drain(service.RetryFailedProvision());
                var removesAfterReplacement = transport.RemoveCount;
                Assert.That(service.Ready, Is.True);

                Drain(oldRemove);

                Assert.That(transport.RemoveCount, Is.EqualTo(removesAfterReplacement));
                Assert.That(transport.RemoteRooms, Is.EquivalentTo(store.Read().Manifest.Rooms));
            }
            finally
            {
                DisposeEnumerator(oldRemove);
                DisposeEnumerator(loop);
                DisposeEnumerator(retryParent);
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void B_D01_DetachedUnstartedRetirementDeleteCannotRunAfterSuccessfulRetirement()
        {
            var participant = Participant("detached-retire", "red");
            var rooms = ServiceRooms(Epoch("a"), "red");
            var store = new FakeStore(Manifest("a", rooms, true));
            var transport = new FakeTransport();
            transport.SeedRooms(rooms);
            var service = NewService(participant, store, transport);
            IEnumerator retirement = null;
            IEnumerator oldDelete = null;
            try
            {
                retirement = service.RetireRooms();
                Assert.That(retirement.MoveNext(), Is.True);
                oldDelete = (IEnumerator)retirement.Current;
                DisposeEnumerator(retirement);

                Drain(service.RetireRooms());
                Assert.That(service.RetiredSuccessfully, Is.True);
                Assert.That(transport.RemoteRooms, Is.Empty);
                var deletesAfterRetirement = transport.DeleteCount;

                Drain(oldDelete);

                Assert.That(transport.DeleteCount, Is.EqualTo(deletesAfterRetirement),
                    "A released retirement lease cannot emit a late idempotent delete either.");
                Assert.That(service.RetiredSuccessfully, Is.True);
            }
            finally
            {
                DisposeEnumerator(oldDelete);
                DisposeEnumerator(retirement);
                UnityEngine.Object.DestroyImmediate(service.gameObject);
            }
        }

        [Test]
        public void V_B01_AllIteratorDrainsShareOneFiniteBudgetAndDisposeEveryCreatedIterator()
        {
            var root = new EndlessNestedIterator();
            var failure = Assert.Throws<AssertionException>(() => Drain(root));
            Assert.That(failure.Message, Does.Contain("finite total coroutine step budget"));
            Assert.That(root.Disposed, Is.True);
            Assert.That(root.CreatedChildren, Is.GreaterThan(0));
            Assert.That(root.DisposedChildren, Is.EqualTo(root.CreatedChildren));
        }

        [Test]
        public void V_B01_CoordinatorDrainRejectsExcessiveActionInventoryWithinFiniteBudget()
        {
            var rooms = Enumerable.Range(0, TestActionBudget + 1).Select(i => "room-" + i).ToArray();
            var coordinator = new VoiceEpochCoordinator("world", "shift", epoch => rooms, new FakeStore());
            Assert.That(coordinator.StartProvision(Epoch("a")), Is.True);
            var failure = Assert.Throws<AssertionException>(() => Drain(coordinator, action => true));
            Assert.That(failure.Message, Does.Contain("finite total coordinator action budget"));
            Assert.That(coordinator.Ready, Is.False,
                "Budget rejection must not be converted into a fake successful completion.");
        }

        [TestCase(0, false)]
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void V_B02_CreateFailureEffectMatrixPreservesRemoteStateAcrossFreshRetryAndRetirement(
            int failureIndex, bool effectBeforeFailedResponse)
        {
            var participants = new[] { Participant("matrix-state-red", "red"), Participant("matrix-state-blue", "blue") };
            var remoteRooms = new HashSet<string>(StringComparer.Ordinal);
            var operations = new List<string>();
            var store = new FakeStore();
            var firstTransport = new FakeTransport(remoteRooms, operations)
            {
                SideEffectOnFailedCreate = effectBeforeFailedResponse
            };
            for (var i = 0; i < failureIndex; i++) firstTransport.CreateResults.Enqueue(true);
            firstTransport.CreateResults.Enqueue(false);
            var first = NewService(participants, store, firstTransport, () => Epoch("b"));
            try
            {
                Drain(first.RetryFailedProvision());
                Assert.That(first.Ready, Is.False);
                Assert.That(remoteRooms.Count, Is.EqualTo(failureIndex + (effectBeforeFailedResponse ? 1 : 0)));
            }
            finally { UnityEngine.Object.DestroyImmediate(first.gameObject); }

            var failedIntent = store.Read().Manifest.Clone();
            var retryStore = new FakeStore(failedIntent);
            var retryTransport = new FakeTransport(remoteRooms, operations);
            var operationStart = operations.Count;
            var retry = NewService(participants, retryStore, retryTransport, () => Epoch("c"));
            VoiceEpochManifest ready;
            try
            {
                Drain(retry.RetryFailedProvision());
                ready = retryStore.Read().Manifest.Clone();
                Assert.That(retry.Ready, Is.True);
                Assert.That(ready.Epoch, Is.EqualTo(Epoch("c")));
                Assert.That(remoteRooms, Is.EquivalentTo(ready.Rooms));
                var transition = operations.Skip(operationStart).ToList();
                var firstCreate = transition.FindIndex(value => value.StartsWith("CreateRoom:", StringComparison.Ordinal));
                Assert.That(firstCreate, Is.GreaterThanOrEqualTo(failedIntent.Rooms.Length));
                Assert.That(transition.Take(failedIntent.Rooms.Length).Select(OperationRoom),
                    Is.EqualTo(failedIntent.Rooms.OrderBy(value => value, StringComparer.Ordinal)));
            }
            finally { UnityEngine.Object.DestroyImmediate(retry.gameObject); }

            var retireStore = new FakeStore(ready);
            var retire = NewService(participants, retireStore, new FakeTransport(remoteRooms, operations));
            try
            {
                Drain(retire.RetireRooms());
                Assert.That(retire.RetiredSuccessfully, Is.True);
                Assert.That(remoteRooms, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(retire.gameObject); }
        }

        [Test]
        public void R6_F4_FreshServiceAndFreshStoreAdapterCannotReplaceStartedOldCreate()
        {
            var participant = Participant("fresh-boundary", "red");
            var files = new FakeFiles();
            var remoteRooms = new HashSet<string>(StringComparer.Ordinal);
            var operations = new List<string>();
            var oldTransport = new FakeTransport(remoteRooms, operations) { PauseNextCreate = true };
            var oldService = NewService(participant,
                new PersistentVoiceEpochStore("canonical", files), oldTransport, () => Epoch("b"));
            SplitProvision split = null;
            ServerVoiceService freshService = null;
            try
            {
                split = CaptureFirstCreateCall(oldService);
                Assert.That(split.Call.MoveNext(), Is.True);
                Assert.That(split.Call.Current, Is.TypeOf<FakeTransport.Pause>());
                Assert.That(oldTransport.CreateCount, Is.EqualTo(1));
                var e1Rooms = ServiceRooms(Epoch("b"), "red");
                var e1Intent = new PersistentVoiceEpochStore("canonical", files).Read().Manifest;
                Assert.That(e1Intent.Epoch, Is.EqualTo(Epoch("b")));
                Assert.That(e1Intent.Ready, Is.False);
                Assert.That(e1Intent.Rooms, Is.EquivalentTo(e1Rooms));

                // The manual IEnumerator split models a dispatched but not-yet-settled call.
                // Disposing only the old parents must not be treated as remote completion.
                var operationCountAtPause = operations.Count;
                split.DisposeParents();

                var freshTransport = new FakeTransport(remoteRooms, operations);
                freshService = NewService(participant,
                    new PersistentVoiceEpochStore("canonical", files), freshTransport, () => Epoch("c"));
                Drain(freshService.RetryFailedProvision());
                var canonicalBeforeSettlement = new PersistentVoiceEpochStore("canonical", files).Read().Manifest;
                var readyBeforeSettlement = freshService.Ready;
                var grantBeforeSettlement = freshService.Grant(participant, "team");
                var operationsBeforeSettlement = operations.ToArray();

                // Resume the original child after replacement was attempted, then inspect the shared remote set.
                Drain(split.Call);
                var canonicalAfterSettlement = new PersistentVoiceEpochStore("canonical", files).Read().Manifest;
                var pendingAfterSettlement = canonicalAfterSettlement.PendingOperations.ToArray();
                var remoteAfterSettlement = remoteRooms.ToArray();
                var leakedAfterSettlement = remoteAfterSettlement
                    .Except(canonicalAfterSettlement == null || canonicalAfterSettlement.Rooms == null
                        ? Array.Empty<string>() : canonicalAfterSettlement.Rooms, StringComparer.Ordinal)
                    .ToArray();

                Drain(freshService.RetireRooms());
                var remoteAfterRetirement = remoteRooms.ToArray();

                {
                    Assert.That(operationsBeforeSettlement.Length, Is.EqualTo(operationCountAtPause),
                        "A fresh service must not dispatch replacement mutations while the old child is unsettled.");
                    Assert.That(canonicalBeforeSettlement.Epoch, Is.EqualTo(Epoch("b")));
                    Assert.That(canonicalBeforeSettlement.Ready, Is.False,
                        "A fresh service must not replace the full E1 intent before the old child settles.");
                    Assert.That(readyBeforeSettlement, Is.False,
                        "Ready must remain closed until the old remote effect has settled.");
                    Assert.That(grantBeforeSettlement, Is.Null,
                        "Grant must remain closed at the transition boundary.");
                    Assert.That(pendingAfterSettlement, Is.Empty,
                        "Confirmed child completion must clear the durable dispatch uncertainty marker.");
                    Assert.That(leakedAfterSettlement, Is.Empty,
                        "After the old child settles, remote inventory must not escape canonical ownership.");
                    Assert.That(freshService.RetiredSuccessfully && remoteAfterRetirement.Length != 0, Is.False,
                        "Retirement must not report success while stale remote inventory remains.");
                }
            }
            finally
            {
                split?.Dispose();
                if (freshService != null) UnityEngine.Object.DestroyImmediate(freshService.gameObject);
                UnityEngine.Object.DestroyImmediate(oldService.gameObject);
            }
        }

        [Test]
        public void R6_F4_UnresolvedDurableDispatchFailsClosedButUnrelatedCanonicalIsUnaffected()
        {
            var participant = Participant("dispatch-boundary", "red");
            var blockedFiles = new FakeFiles();
            var blockedStore = new PersistentVoiceEpochStore("canonical-a", blockedFiles);
            Assert.That(blockedStore.TryCommit(Manifest("a", ServiceRooms(Epoch("a"), "red"), false)), Is.True);
            Assert.That(blockedStore.TryBeginDispatch("CreateRoom:unresolved"), Is.True);

            var blockedTransport = new FakeTransport();
            var blockedService = NewService(participant,
                new PersistentVoiceEpochStore("canonical-a", blockedFiles), blockedTransport, () => Epoch("b"));
            var otherTransport = new FakeTransport();
            var otherService = NewService(participant,
                new PersistentVoiceEpochStore("canonical-b", new FakeFiles()), otherTransport, () => Epoch("c"));
            try
            {
                Drain(blockedService.RetryFailedProvision());
                Assert.That(blockedTransport.TotalMutationCount, Is.Zero);
                Assert.That(blockedService.Ready, Is.False);
                Assert.That(blockedService.Grant(participant, "team"), Is.Null);

                Drain(otherService.RetryFailedProvision());
                Assert.That(otherService.Ready, Is.True);
                Assert.That(otherTransport.CreateCount, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherService.gameObject);
                UnityEngine.Object.DestroyImmediate(blockedService.gameObject);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void R6_F6_TransientInitialReadRecoveryRereadsSameServiceWithoutDispatch(
            bool initialReadThrows, bool recoverExistingManifest)
        {
            var participant = Participant("transient-read-" + initialReadThrows + "-" + recoverExistingManifest, "red");
            var oldRooms = recoverExistingManifest ? ServiceRooms(Epoch("a"), "red") : Array.Empty<string>();
            var candidateRooms = ServiceRooms(Epoch("b"), "red").OrderBy(room => room, StringComparer.Ordinal).ToArray();
            var operations = new List<string>();
            var initialCanonical = recoverExistingManifest ? Manifest("a", oldRooms, true) : null;
            var store = new ObservedRecoveryStore(initialCanonical, initialReadThrows, operations);
            var transport = new FakeTransport(new HashSet<string>(oldRooms, StringComparer.Ordinal), operations);
            var service = NewService(participant, store, transport, () => Epoch("b"));
            try
            {
                Drain(service.RetryFailedProvision());
                var readsAfterInitialFailure = store.ReadCount;
                var commitsAfterInitialFailure = store.Commits.Count;
                var mutationsAfterInitialFailure = transport.TotalMutationCount;
                var operationsAfterInitialFailure = operations.ToArray();
                var canonicalAfterInitialFailure = store.CanonicalSnapshot;
                var remoteAfterInitialFailure = transport.RemoteRooms.ToArray();
                var readyAfterInitialFailure = service.Ready;
                var grantAfterInitialFailure = service.Grant(participant, "team");

                store.RecoverCanonicalReads();
                var readsBeforeRecoveryRetry = store.ReadCount;
                Drain(service.RetryFailedProvision());

                var canonicalAfterRecovery = store.CanonicalSnapshot;
                var remoteAfterRecovery = transport.RemoteRooms.ToArray();
                var operationsAfterRecovery = operations.ToArray();
                var commitsAfterRecovery = store.Commits.Select(commit => commit.Clone()).ToArray();
                var readyAfterRecovery = service.Ready;
                var grantAfterRecovery = service.Grant(participant, "team");
                var expectedOperations = oldRooms.OrderBy(room => room, StringComparer.Ordinal)
                    .Select(room => "DeleteRoom:" + room + ":ok")
                    .Concat(new[] { "CommitIntent:" + Epoch("b") + ":" + string.Join(",", candidateRooms) })
                    .Concat(candidateRooms.Select(room => "CreateRoom:" + room + ":ok"))
                    .Concat(new[] { "CommitReady:" + Epoch("b") + ":" + string.Join(",", candidateRooms) })
                    .ToArray();

                {
                    Assert.That(readsAfterInitialFailure, Is.GreaterThan(0));
                    Assert.That(commitsAfterInitialFailure, Is.Zero);
                    Assert.That(mutationsAfterInitialFailure, Is.Zero);
                    Assert.That(operationsAfterInitialFailure, Is.Empty);
                    Assert.That(readyAfterInitialFailure, Is.False);
                    Assert.That(grantAfterInitialFailure, Is.Null);
                    if (recoverExistingManifest)
                    {
                        Assert.That(canonicalAfterInitialFailure.Epoch, Is.EqualTo(Epoch("a")));
                        Assert.That(canonicalAfterInitialFailure.Rooms, Is.EqualTo(oldRooms));
                    }
                    else Assert.That(canonicalAfterInitialFailure, Is.Null);
                    Assert.That(remoteAfterInitialFailure, Is.EquivalentTo(oldRooms));

                    Assert.That(store.ReadCount, Is.GreaterThan(readsBeforeRecoveryRetry),
                        "The same public Retry must perform a new canonical read after a no-dispatch initial failure.");
                    Assert.That(commitsAfterRecovery.Length, Is.EqualTo(2));
                    Assert.That(commitsAfterRecovery.Select(commit => commit.Ready), Is.EqualTo(new[] { false, true }));
                    Assert.That(commitsAfterRecovery.All(commit => commit.Rooms.SequenceEqual(candidateRooms)), Is.True,
                        "The entire ordered replacement inventory must be committed before the first create and at Ready.");
                    Assert.That(operationsAfterRecovery, Is.EqualTo(expectedOperations),
                        "Validated old deletes, whole-intent commit, creates, and Ready commit must remain serial and ordered.");
                    Assert.That(transport.RemoveCount, Is.Zero);
                    Assert.That(canonicalAfterRecovery, Is.Not.Null,
                        "Successful recovery must leave a canonical replacement manifest.");
                    if (canonicalAfterRecovery != null)
                    {
                        Assert.That(canonicalAfterRecovery.Epoch, Is.EqualTo(Epoch("b")));
                        Assert.That(canonicalAfterRecovery.Ready, Is.True);
                        Assert.That(canonicalAfterRecovery.Rooms, Is.EqualTo(candidateRooms));
                        Assert.That(remoteAfterRecovery, Is.EquivalentTo(canonicalAfterRecovery.Rooms));
                    }
                    Assert.That(readyAfterRecovery, Is.True);
                    Assert.That(grantAfterRecovery, Is.Not.Null);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void R6_F6_PublicRetryRereadsButDoesNotClearContinuingInitialReadUncertainty(bool initialReadThrows)
        {
            var participant = Participant("continuing-read-" + initialReadThrows, "red");
            var oldRooms = ServiceRooms(Epoch("a"), "red");
            var operations = new List<string>();
            var store = new ObservedRecoveryStore(Manifest("a", oldRooms, true), initialReadThrows, operations);
            var transport = new FakeTransport(new HashSet<string>(oldRooms, StringComparer.Ordinal), operations);
            var service = NewService(participant, store, transport, () => Epoch("b"));
            try
            {
                Drain(service.RetryFailedProvision());
                var readsAfterFirstFailure = store.ReadCount;
                store.KeepCanonicalReadsInvalid();
                Drain(service.RetryFailedProvision());

                var readsAfterSecondFailure = store.ReadCount;
                var canonicalAfterSecondFailure = store.CanonicalSnapshot;
                var remoteAfterSecondFailure = transport.RemoteRooms.ToArray();
                var operationsAfterSecondFailure = operations.ToArray();
                var commitsAfterSecondFailure = store.Commits.Select(commit => commit.Clone()).ToArray();
                var readyAfterSecondFailure = service.Ready;
                var grantAfterSecondFailure = service.Grant(participant, "team");

                {
                    Assert.That(readsAfterFirstFailure, Is.GreaterThan(0));
                    Assert.That(readsAfterSecondFailure, Is.GreaterThan(readsAfterFirstFailure),
                        "Each public Retry must reread rather than blindly clear or permanently skip initial uncertainty.");
                    Assert.That(commitsAfterSecondFailure, Is.Empty);
                    Assert.That(transport.TotalMutationCount, Is.Zero);
                    Assert.That(operationsAfterSecondFailure, Is.Empty);
                    Assert.That(canonicalAfterSecondFailure.Epoch, Is.EqualTo(Epoch("a")));
                    Assert.That(canonicalAfterSecondFailure.Ready, Is.True);
                    Assert.That(canonicalAfterSecondFailure.Rooms, Is.EqualTo(oldRooms));
                    Assert.That(remoteAfterSecondFailure, Is.EquivalentTo(oldRooms));
                    Assert.That(readyAfterSecondFailure, Is.False);
                    Assert.That(grantAfterSecondFailure, Is.Null);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(service.gameObject); }
        }

        private static string RemoveJsonField(string json, string field)
        {
            var marker = "\"" + field + "\":";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), field);
            var valueStart = start + marker.Length;
            var depth = 0;
            var quoted = false;
            var escaped = false;
            var end = valueStart;
            for (; end < json.Length; end++)
            {
                var c = json[end];
                if (quoted)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') quoted = true;
                else if (c == '[' || c == '{') depth++;
                else if (c == ']' || c == '}')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ',' && depth == 0) break;
            }
            var removeStart = start > 0 && json[start - 1] == ',' ? start - 1 : start;
            if (removeStart == start && end < json.Length && json[end] == ',') end++;
            return json.Remove(removeStart, end - removeStart);
        }

        private static string DecodeJwtPayload(string token)
        {
            var value = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            value = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static ServerVoiceService NewService(ParticipantState participant, IVoiceEpochStore store, IVoiceServiceTransport transport)
        {
            return NewService(new[] { participant }, store, transport);
        }

        private static ServerVoiceService NewService(ParticipantState[] participants, IVoiceEpochStore store, IVoiceServiceTransport transport)
        {
            return NewService(participants, store, transport, () => Epoch("b"));
        }

        private static ServerVoiceService NewService(ParticipantState participant, IVoiceEpochStore store,
            IVoiceServiceTransport transport, Func<string> nextEpoch)
        {
            return NewService(new[] { participant }, store, transport, nextEpoch);
        }

        private static ServerVoiceService NewService(ParticipantState[] participants, IVoiceEpochStore store,
            IVoiceServiceTransport transport, Func<string> nextEpoch)
        {
            var service = new GameObject("VoiceEpochServiceFake").AddComponent<ServerVoiceService>();
            service.ConfigureForTests(TestConfig(), TestWorld(participants), store, transport, nextEpoch, () => 1000);
            return service;
        }

        private static VoiceServiceConfig TestConfig()
        {
            return new VoiceServiceConfig
            {
                HttpUrl = "http://127.0.0.1", WebSocketUrl = "wss://voice.example",
                ApiKey = "key", ApiSecret = new string('s', 40)
            };
        }

        private static WorldState TestWorld(params ParticipantState[] participants)
        {
            return new WorldState { WorldId = "world", ShiftId = "shift", Participants = participants };
        }

        private static ParticipantState Participant(string id, string team)
        {
            return new ParticipantState { ParticipantId = id, TeamId = team, RoleId = "role-01" };
        }

        private static string[] ServiceRooms(string epoch, params string[] teams)
        {
            return teams.Select(team => VoiceTokenIssuer.Room("world", "shift", epoch, "team." + team))
                .Concat(new[] { VoiceTokenIssuer.Room("world", "shift", epoch, "command") }).Distinct().ToArray();
        }

        private static IEnumerable<VoiceEpochManifest> InvalidManifests()
        {
            var validRooms = ServiceRooms(Epoch("a"), "red");
            var invalid = Manifest("a", validRooms, true); invalid.Schema = 1; yield return invalid;
            invalid = Manifest("a", validRooms, true); invalid.WorldId = "foreign"; yield return invalid;
            invalid = Manifest("a", validRooms, true); invalid.ShiftId = "foreign"; yield return invalid;
            invalid = Manifest("not-guid", validRooms, true); yield return invalid;
            yield return Manifest("a", new[] { validRooms[0] }, true);
            yield return Manifest("a", validRooms.Concat(new[] { "extra" }).ToArray(), true);
            yield return Manifest("a", new[] { validRooms[0], validRooms[1], validRooms[1] }, true);
            yield return Manifest("a", new[] { validRooms[0], validRooms[1], (string)null }, true);
            yield return Manifest("a", new[] { validRooms[0], validRooms[1], "" }, true);
        }

        private static void Drain(IEnumerator routine)
        {
            using (var driver = new CoroutineDriver(routine)) driver.Finish();
        }

        private sealed class CoroutineDriver : IDisposable
        {
            private readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
            private int steps;
            public CoroutineDriver(IEnumerator root) { stack.Push(root); }

            public string MoveToTransportPause()
            {
                while (stack.Count != 0)
                {
                    Step();
                    var current = stack.Peek();
                    if (!current.MoveNext()) { DisposeTop(); continue; }
                    if (current.Current is FakeTransport.Pause pause) return pause.Method;
                    if (current.Current is IEnumerator nested) stack.Push(nested);
                }
                return null;
            }

            public void Finish()
            {
                try
                {
                    while (stack.Count != 0)
                    {
                        Step();
                        var current = stack.Peek();
                        if (!current.MoveNext()) { DisposeTop(); continue; }
                        if (current.Current is IEnumerator nested) stack.Push(nested);
                    }
                }
                finally
                {
                    Dispose();
                }
            }

            private void Step()
            {
                Assert.That(++steps, Is.LessThanOrEqualTo(TestStepBudget),
                    "Coroutine exceeded the finite total coroutine step budget.");
            }

            public void Dispose()
            {
                while (stack.Count != 0) DisposeTop();
            }

            private void DisposeTop()
            {
                var current = stack.Pop();
                (current as IDisposable)?.Dispose();
            }
        }

        private static void DisposeEnumerator(IEnumerator routine)
        {
            (routine as IDisposable)?.Dispose();
        }

        private static VoiceEpochCoordinator NewCoordinator(IVoiceEpochStore store)
        {
            return new VoiceEpochCoordinator("world", "shift", epoch => epoch == Epoch("a") ? RoomsA :
                epoch == Epoch("b") ? RoomsB : new[] { "room-c", "room-command-c" }, store);
        }

        private static List<VoiceEpochAction> Drain(VoiceEpochCoordinator coordinator, Func<VoiceEpochAction, bool> complete)
        {
            var actions = new List<VoiceEpochAction>();
            VoiceEpochAction action;
            while (coordinator.TryGetAction(out action))
            {
                Assert.That(actions.Count + 1, Is.LessThanOrEqualTo(TestActionBudget),
                    "Coordinator exceeded the finite total coordinator action budget.");
                actions.Add(action);
                if (action.Kind == VoiceEpochActionKind.CreateRoom)
                {
                    // The fake remote operation is the actual coordinator completion path.
                }
                var success = complete(action);
                if (!coordinator.Complete(action, success)) break;
            }
            return actions;
        }

        private static SplitProvision CaptureFirstCreateCall(ServerVoiceService service)
        {
            var split = new SplitProvision
            {
                Retry = service.RetryFailedProvision()
            };
            Assert.That(split.Retry.MoveNext(), Is.True);
            split.Loop = (IEnumerator)split.Retry.Current;
            Assert.That(split.Loop.MoveNext(), Is.True);
            split.Provision = (IEnumerator)split.Loop.Current;
            var steps = 0;
            while (split.Provision.MoveNext())
            {
                Assert.That(++steps, Is.LessThanOrEqualTo(TestStepBudget));
                var child = split.Provision.Current as IEnumerator;
                if (child == null) continue;
                split.Call = child;
                return split;
            }
            Assert.Fail("Provision did not expose a create call child.");
            return split;
        }

        private sealed class SplitProvision : IDisposable
        {
            public IEnumerator Retry, Loop, Provision, Call;
            public void DisposeParents()
            {
                DisposeEnumerator(Provision);
                DisposeEnumerator(Loop);
                DisposeEnumerator(Retry);
                Provision = null;
                Loop = null;
                Retry = null;
            }
            public void Dispose()
            {
                DisposeEnumerator(Call);
                DisposeParents();
            }
        }

        private sealed class EndlessNestedIterator : IEnumerator, IDisposable
        {
            private readonly EndlessNestedIterator root;
            public bool Disposed { get; private set; }
            public int CreatedChildren { get; private set; }
            public int DisposedChildren { get; private set; }
            public object Current { get; private set; }

            public EndlessNestedIterator() { root = this; }
            private EndlessNestedIterator(EndlessNestedIterator rootOwner) { root = rootOwner; }

            public bool MoveNext()
            {
                var child = new EndlessNestedIterator(root);
                root.CreatedChildren++;
                Current = child;
                return true;
            }

            public void Reset() { throw new NotSupportedException(); }
            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                if (!ReferenceEquals(root, this)) root.DisposedChildren++;
            }
        }

        private static string OperationRoom(string operation)
        {
            var first = operation.IndexOf(':');
            var last = operation.LastIndexOf(':');
            return operation.Substring(first + 1, last - first - 1);
        }

        private static object GetField(object target, string name)
        {
            return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static string Epoch(string suffix) => suffix.PadLeft(32, '0');

        private static VoiceEpochManifest Manifest(string epoch, string[] rooms, bool ready)
        {
            return new VoiceEpochManifest
            {
                WorldId = "world", ShiftId = "shift", Epoch = Epoch(epoch), Rooms = rooms,
                PendingOperations = Array.Empty<string>(), Ready = ready
            };
        }

        private sealed class FakeTransport : IVoiceServiceTransport
        {
            public sealed class Pause
            {
                public Pause(string method) { Method = method; }
                public string Method { get; private set; }
            }

            [Serializable] private sealed class CreateRequest { public string name; }
            [Serializable] private sealed class DeleteRequest { public string room; }
            [Serializable] private sealed class RemoveRequest { public string room, identity; }

            public readonly Queue<bool> CreateResults = new Queue<bool>();
            public readonly Queue<bool> DeleteResults = new Queue<bool>();
            public readonly Queue<bool> RemoveResults = new Queue<bool>();
            public readonly HashSet<string> RemoteRooms;
            public readonly List<string> Operations;
            private readonly Queue<Action> pendingCallbacks = new Queue<Action>();
            public bool PauseNextCreate, PauseNextDelete, PauseNextRemove, NestNextCall, DelayNextCallback;
            public bool SideEffectOnFailedCreate, SideEffectOnNextFailedCreate, SideEffectOnNextFailedDelete;
            public int CreateCount, DeleteCount, RemoveCount;
            public int TotalMutationCount => CreateCount + DeleteCount + RemoveCount;
            public int PendingCallbackCount => pendingCallbacks.Count;

            public void CompleteNextCallback()
            {
                Assert.That(pendingCallbacks.Count, Is.GreaterThan(0));
                pendingCallbacks.Dequeue()();
            }

            public FakeTransport()
                : this(new HashSet<string>(StringComparer.Ordinal), new List<string>()) { }

            public FakeTransport(HashSet<string> remoteRooms, List<string> operations)
            {
                RemoteRooms = remoteRooms ?? throw new ArgumentNullException(nameof(remoteRooms));
                Operations = operations ?? throw new ArgumentNullException(nameof(operations));
            }

            public void SeedRooms(IEnumerable<string> rooms)
            {
                foreach (var room in rooms) RemoteRooms.Add(room);
            }

            public IEnumerator Call(string method, string json, string administrationRoom, Action<bool> complete)
            {
                if (NestNextCall)
                {
                    NestNextCall = false;
                    yield return ExecuteCall(method, json, complete);
                    yield break;
                }
                var operation = ExecuteCall(method, json, complete);
                while (operation.MoveNext()) yield return operation.Current;
            }

            private IEnumerator ExecuteCall(string method, string json, Action<bool> complete)
            {
                Queue<bool> results;
                var pause = false;
                if (method == "CreateRoom") { CreateCount++; results = CreateResults; pause = PauseNextCreate; PauseNextCreate = false; }
                else if (method == "DeleteRoom") { DeleteCount++; results = DeleteResults; pause = PauseNextDelete; PauseNextDelete = false; }
                else { RemoveCount++; results = RemoveResults; pause = PauseNextRemove; PauseNextRemove = false; }
                if (pause) yield return new Pause(method);
                var configuredResult = results.Count == 0 || results.Dequeue();
                if (method == "CreateRoom")
                {
                    var room = JsonUtility.FromJson<CreateRequest>(json).name;
                    var failedCreateEffect = !configuredResult && (SideEffectOnFailedCreate || SideEffectOnNextFailedCreate);
                    if (configuredResult || failedCreateEffect) RemoteRooms.Add(room);
                    Operations.Add(method + ":" + room + ":" + (configuredResult ? "ok" : "failed"));
                    if (!configuredResult) SideEffectOnNextFailedCreate = false;
                }
                else if (method == "DeleteRoom")
                {
                    var room = JsonUtility.FromJson<DeleteRequest>(json).room;
                    var existed = RemoteRooms.Contains(room);
                    if (configuredResult || SideEffectOnNextFailedDelete) RemoteRooms.Remove(room);
                    Operations.Add(method + ":" + room + ":" + (!configuredResult ? "failed" : existed ? "ok" : "404"));
                    SideEffectOnNextFailedDelete = false;
                }
                else
                {
                    var request = JsonUtility.FromJson<RemoveRequest>(json);
                    Operations.Add(method + ":" + request.room + ":" + request.identity + ":" +
                        (configuredResult ? "ok" : "failed"));
                }
                if (DelayNextCallback)
                {
                    DelayNextCallback = false;
                    pendingCallbacks.Enqueue(() => complete(configuredResult));
                }
                else complete(configuredResult);
            }
        }

        private enum FileCommitBoundary
        {
            None, Write, Flush, DurableFlush, BeforeMoveOrReplace, AfterMoveOrReplace
        }

        private sealed class FakeFiles : IVoiceEpochFiles
        {
            private sealed class Writer : IVoiceEpochFileWriter
            {
                private readonly FakeFiles owner;
                public Writer(FakeFiles owner) { this.owner = owner; }
                public void Write(string json)
                {
                    if (owner.Boundary == FileCommitBoundary.Write) throw new InvalidOperationException("synthetic write failure");
                    owner.temporary = Encoding.UTF8.GetBytes(json);
                }
                public void FlushWriter()
                {
                    if (owner.Boundary == FileCommitBoundary.Flush) throw new InvalidOperationException("synthetic writer flush failure");
                }
                public void FlushDurable()
                {
                    if (owner.Boundary == FileCommitBoundary.DurableFlush) throw new InvalidOperationException("synthetic durable flush failure");
                }
                public void Dispose() { }
            }

            public readonly Queue<FileReadResult> Reads = new Queue<FileReadResult>();
            public FileCommitBoundary Boundary;
            public int CommitCount, ThrowReadCount;
            public byte[] Canonical => canonical == null ? null : canonical.ToArray();
            private byte[] canonical, temporary;

            public void Seed(string json) { canonical = Encoding.UTF8.GetBytes(json); }

            public FileReadResult ReadCanonical(string path)
            {
                if (ThrowReadCount > 0) { ThrowReadCount--; throw new InvalidOperationException("synthetic file read failure"); }
                return Reads.Count == 0
                    ? canonical == null ? FileReadResult.NotFound() : FileReadResult.Success(canonical)
                    : Reads.Dequeue();
            }
            public void CreateDirectory(string path) { }
            public IVoiceEpochFileWriter OpenTemporary(string path) => new Writer(this);
            public void MoveTemporary(string temporaryPath, string canonicalPath) => CommitTemporary();
            public void ReplaceTemporary(string temporaryPath, string canonicalPath, string backupPath) => CommitTemporary();
            public void DeleteTemporary(string path) { temporary = null; }

            private void CommitTemporary()
            {
                CommitCount++;
                if (Boundary == FileCommitBoundary.BeforeMoveOrReplace) throw new InvalidOperationException("synthetic pre-commit failure");
                canonical = temporary == null ? null : temporary.ToArray();
                if (Boundary == FileCommitBoundary.AfterMoveOrReplace) throw new InvalidOperationException("synthetic post-commit acknowledgement loss");
            }
        }

        private sealed class ObservedRecoveryStore : IVoiceEpochStore
        {
            private readonly bool readThrows;
            private readonly List<string> operations;
            private VoiceEpochManifest canonical;
            private bool recovered;
            private bool invalidAfterRecovery;

            public ObservedRecoveryStore(VoiceEpochManifest initial, bool readThrows, List<string> operations)
            {
                canonical = initial == null ? null : initial.Clone();
                this.readThrows = readThrows;
                this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            }

            public readonly List<VoiceEpochManifest> Commits = new List<VoiceEpochManifest>();
            public int ReadCount { get; private set; }
            public VoiceEpochManifest CanonicalSnapshot => canonical == null ? null : canonical.Clone();
            public void RecoverCanonicalReads() { recovered = true; }
            public void KeepCanonicalReadsInvalid() { recovered = true; invalidAfterRecovery = true; }

            public VoiceEpochStoreRead Read()
            {
                ReadCount++;
                if (!recovered)
                {
                    if (readThrows) throw new InvalidOperationException("synthetic transient initial canonical read failure");
                    return VoiceEpochStoreRead.Invalid();
                }
                if (invalidAfterRecovery)
                {
                    if (readThrows) throw new InvalidOperationException("synthetic continuing canonical read failure");
                    return VoiceEpochStoreRead.Invalid();
                }
                return canonical == null ? VoiceEpochStoreRead.Missing() : VoiceEpochStoreRead.Valid(canonical.Clone());
            }

            public bool TryCommit(VoiceEpochManifest next)
            {
                var committed = next.Clone();
                Commits.Add(committed);
                operations.Add((committed.Ready ? "CommitReady:" : "CommitIntent:") + committed.Epoch + ":" +
                    string.Join(",", committed.Rooms));
                canonical = committed;
                return true;
            }
        }

        private enum ReadyCommitMode { FalseWithoutPersist, FalseAfterPersist, ThrowAfterPersist }

        private sealed class ReadyCommitStore : IVoiceEpochStore
        {
            private readonly ReadyCommitMode mode;
            private VoiceEpochManifest manifest;
            public ReadyCommitStore(ReadyCommitMode mode) { this.mode = mode; }
            public VoiceEpochStoreRead Read() => manifest == null ? VoiceEpochStoreRead.Missing() : VoiceEpochStoreRead.Valid(manifest.Clone());
            public bool TryCommit(VoiceEpochManifest next)
            {
                if (!next.Ready) { manifest = next.Clone(); return true; }
                if (mode != ReadyCommitMode.FalseWithoutPersist) manifest = next.Clone();
                if (mode == ReadyCommitMode.ThrowAfterPersist) throw new InvalidOperationException("synthetic ready acknowledgement loss");
                return false;
            }
        }

        private sealed class FakeStore : IVoiceEpochStore
        {
            private VoiceEpochManifest manifest;
            public readonly List<VoiceEpochManifest> Commits = new List<VoiceEpochManifest>();
            public bool CommitResult = true;
            public bool PersistOnFailedCommit;
            public bool ThrowAfterPersist;
            public VoiceEpochStoreRead ReadAfterCommit;
            public bool ThrowOnInitialRead;
            public bool ThrowOnReadAfterCommit;
            private bool attemptedCommit;

            public FakeStore() { }
            public FakeStore(VoiceEpochManifest initial) { manifest = initial.Clone(); }
            public bool Exists => manifest != null;
            public void ClearCanonical() { manifest = null; }
            public void RestoreCanonical(VoiceEpochManifest restored) { manifest = restored == null ? null : restored.Clone(); }

            public VoiceEpochStoreRead Read()
            {
                if (!attemptedCommit && ThrowOnInitialRead) throw new InvalidOperationException("synthetic initial canonical read failure");
                if (attemptedCommit && ThrowOnReadAfterCommit) throw new InvalidOperationException("synthetic canonical reread failure");
                if (attemptedCommit && ReadAfterCommit != null) return ReadAfterCommit;
                return manifest == null ? VoiceEpochStoreRead.Missing() : VoiceEpochStoreRead.Valid(manifest.Clone());
            }

            public bool TryCommit(VoiceEpochManifest next)
            {
                attemptedCommit = true;
                Commits.Add(next.Clone());
                if (CommitResult || PersistOnFailedCommit || ThrowAfterPersist) manifest = next.Clone();
                if (ThrowAfterPersist) throw new InvalidOperationException("synthetic post-commit acknowledgement loss");
                return CommitResult;
            }
        }
    }
}
