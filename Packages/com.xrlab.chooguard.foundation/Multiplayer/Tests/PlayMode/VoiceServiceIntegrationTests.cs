using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiveKit;
using LiveKit.Proto;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class VoiceServiceIntegrationTests
    {
        private readonly List<Room> rooms = new List<Room>();
        private ServerVoiceService service;
        private string records;
        private TestToneSource tone;

        private sealed class TestToneSource : RtcAudioSource
        {
            public override event Action<float[], int, int> AudioRead;
            private double phase;
            public TestToneSource() : base(RtcAudioSourceType.AudioSourceCustom, 48000, 1) { }
            public void Emit()
            {
                var samples = new float[960];
                for (var i = 0; i < samples.Length; i++)
                { samples[i] = (float)(.1 * Math.Sin(phase)); phase += 2 * Math.PI * 440 / 48000; }
                AudioRead?.Invoke(samples, 1, 48000);
            }
        }

        private sealed class RtpSample { public ulong Packets, Bytes; public double Energy; }
        [Serializable] private sealed class RoomStateFixture { public int Schema; public string WorldId, ShiftId, Epoch; public string[] Rooms; }

        [UnityTest, Explicit("Requires explicitly started local LiveKit and Temp/ChooGuardVoiceTestConfig.json")]
        public IEnumerator ExactSdkConnectsScopedRoomsAndRetiredEpochCannotBeReopened()
        {
            var configPath = Path.GetFullPath("Temp/ChooGuardVoiceTestConfig.json");
            if (!File.Exists(configPath)) Assert.Ignore("Private local voice integration configuration is absent.");
            var config = JsonUtility.FromJson<VoiceServiceConfig>(File.ReadAllText(configPath));
            records = Path.Combine(Path.GetTempPath(), "cg-voice-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(records);
            var a = new ParticipantState { ParticipantId = "voice-a", TeamId = "red", RoleId = "role-01", RegionId = "hall" };
            var b = new ParticipantState { ParticipantId = "voice-b", TeamId = "blue", RoleId = "role-01", RegionId = "hall" };
            var world = new WorldState { WorldId = "voice-integration", ShiftId = "test", Participants = new[] { a, b } };
            service = new GameObject("VoiceServiceFixture").AddComponent<ServerVoiceService>();
            service.Configure(config, world, records);
            yield return WaitReady(service);
            var old = service.Grant(a, "team");
            Assert.That(old.Room, Is.Not.EqualTo(service.Grant(b, "team").Room));
            Assert.That(service.Grant(a, "command").CanPublish, Is.False);
            var first = new Room(); rooms.Add(first);
            var connect = first.Connect(old.Url, old.Token, new LiveKit.RoomOptions());
            yield return connect;
            Assert.That(connect.IsError, Is.False, "Exact Unity SDK could not join its authorized room");
            UnityEngine.Object.Destroy(service.gameObject);
            yield return null;
            service = new GameObject("VoiceServiceRecoveryFixture").AddComponent<ServerVoiceService>();
            service.Configure(config, world, records);
            yield return WaitReady(service);
            Assert.That(service.Grant(a, "team").Room, Is.Not.EqualTo(old.Room));
            var stale = new Room(); rooms.Add(stale);
            var denied = stale.Connect(old.Url, old.Token, new LiveKit.RoomOptions());
            yield return denied;
            Assert.That(denied.IsError, Is.True, "A retired room token recreated a room despite auto_create=false");
        }

        [UnityTest, Explicit("Synthetic RTP only; requires explicitly started local LiveKit and private Temp config")]
        public IEnumerator SyntheticVoiceRtpReachesOnlyItsTeamAndStopsAfterMute()
        {
            var configPath = Path.GetFullPath("Temp/ChooGuardVoiceTestConfig.json");
            if (!File.Exists(configPath)) Assert.Ignore("Private local voice integration configuration is absent.");
            var config = JsonUtility.FromJson<VoiceServiceConfig>(File.ReadAllText(configPath));
            records = Path.Combine(Path.GetTempPath(), "cg-voice-rtp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(records);
            var participants = new[] {
                new ParticipantState { ParticipantId = "tone-publisher", TeamId = "red", RoleId = "role-01", RegionId = "hall" },
                new ParticipantState { ParticipantId = "tone-receiver", TeamId = "red", RoleId = "role-01", RegionId = "hall" },
                new ParticipantState { ParticipantId = "other-team", TeamId = "blue", RoleId = "role-01", RegionId = "hall" } };
            service = new GameObject("VoiceRtpFixture").AddComponent<ServerVoiceService>();
            service.Configure(config, new WorldState { WorldId = "voice-rtp", ShiftId = "test", Participants = participants }, records);
            yield return WaitReady(service);
            foreach (var participant in participants)
            {
                var grant = service.Grant(participant, "team");
                var room = new Room(); rooms.Add(room);
                var connect = room.Connect(grant.Url, grant.Token, new LiveKit.RoomOptions { AutoSubscribe = true });
                yield return connect;
                Assert.That(connect.IsError, Is.False, participant.ParticipantId);
            }
            IRemoteTrack received = null;
            var outsiderReceived = false;
            rooms[1].TrackSubscribed += (track, publication, participant) => {
                if (track.Kind == TrackKind.KindAudio && publication.Name == "fixture-tone" && participant.Identity == "tone-publisher") received = track; };
            rooms[2].TrackSubscribed += (track, publication, participant) => {
                if (participant.Identity == "tone-publisher") outsiderReceived = true; };
            tone = new TestToneSource();
            var local = LocalAudioTrack.CreateAudioTrack("fixture-tone", tone, rooms[0]);
            ((ILocalTrack)local).SetMute(true);
            var publish = rooms[0].LocalParticipant.PublishTrack(local, new TrackPublishOptions {
                Source = TrackSource.SourceMicrophone, AudioEncoding = new AudioEncoding { MaxBitrate = 32000 } });
            yield return publish;
            Assert.That(publish.IsError, Is.False);
            tone.Start(); ((ILocalTrack)local).SetMute(false);
            yield return EmitFrames(120);
            Assert.That(received, Is.Not.Null);
            RtpSample first = null, second = null;
            yield return ReadRtp(received, value => first = value);
            yield return EmitFrames(80);
            yield return ReadRtp(received, value => second = value);
            Assert.That(second.Packets, Is.GreaterThan(first.Packets));
            Assert.That(second.Bytes, Is.GreaterThan(first.Bytes));
            Assert.That(rooms[2].ConnectionState, Is.EqualTo(ConnectionState.ConnConnected));
            Assert.That(outsiderReceived, Is.False);
            PushToTalkGate.Stop(() => ((ILocalTrack)local).SetMute(true), () => tone.Stop());
            // Let already queued media drain. This is not a PTT latency acceptance threshold.
            yield return new WaitForSecondsRealtime(1);
            yield return ReadRtp(received, value => first = value);
            yield return new WaitForSecondsRealtime(1);
            yield return ReadRtp(received, value => second = value);
            Assert.That(second.Packets, Is.EqualTo(first.Packets));
            Assert.That(second.Bytes, Is.EqualTo(first.Bytes));
            Assert.That(second.Energy, Is.EqualTo(first.Energy).Within(1e-9));
        }

        private IEnumerator EmitFrames(int count)
        {
            for (var i = 0; i < count; i++) { tone.Emit(); yield return new WaitForSecondsRealtime(.02f); }
        }

        private static IEnumerator ReadRtp(IRemoteTrack track, Action<RtpSample> receive)
        {
            var stats = track.GetStats();
            yield return stats;
            Assert.That(stats.IsError, Is.False);
            var inbound = stats.Stats.Where(s => s.StatsCase == RtcStats.StatsOneofCase.InboundRtp)
                .Select(s => s.InboundRtp).FirstOrDefault(s => s.Stream.Kind == "audio");
            Assert.That(inbound, Is.Not.Null);
            receive(new RtpSample { Packets = inbound.Received.PacketsReceived, Bytes = inbound.Inbound.BytesReceived,
                Energy = inbound.Inbound.TotalAudioEnergy });
        }

        [UnityTest, Explicit("Requires explicitly started local LiveKit and private Temp config")]
        public IEnumerator ServerRevocationAndRetirementRejectAlreadyIssuedRoomTokens()
        {
            var configPath = Path.GetFullPath("Temp/ChooGuardVoiceTestConfig.json");
            if (!File.Exists(configPath)) Assert.Ignore("Private local voice integration configuration is absent.");
            var config = JsonUtility.FromJson<VoiceServiceConfig>(File.ReadAllText(configPath));
            records = Path.Combine(Path.GetTempPath(), "cg-voice-revoke-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(records);
            var participant = new ParticipantState { ParticipantId = "revoked-game-connection", TeamId = "red", RoleId = "role-01", RegionId = "hall" };
            service = new GameObject("VoiceRevocationFixture").AddComponent<ServerVoiceService>();
            service.Configure(config, new WorldState { WorldId = "voice-revoke", ShiftId = "test", Participants = new[] { participant } }, records);
            yield return WaitReady(service);
            var old = service.Grant(participant, "team");
            var connected = new Room(); rooms.Add(connected);
            var join = connected.Connect(old.Url, old.Token, new LiveKit.RoomOptions()); yield return join;
            Assert.That(join.IsError, Is.False);
            service.RevokeConnection(participant.ParticipantId);
            Assert.That(service.Ready, Is.False);
            yield return WaitReady(service);
            var stale = new Room(); rooms.Add(stale);
            var denied = stale.Connect(old.Url, old.Token, new LiveKit.RoomOptions()); yield return denied;
            Assert.That(denied.IsError, Is.True);
            Assert.That(connected.ConnectionState, Is.EqualTo(ConnectionState.ConnDisconnected));
            var current = service.Grant(participant, "team");
            yield return service.RetireRooms();
            Assert.That(service.RetiredSuccessfully, Is.True);
            var afterShutdown = new Room(); rooms.Add(afterShutdown);
            var shutdownDenied = afterShutdown.Connect(current.Url, current.Token, new LiveKit.RoomOptions()); yield return shutdownDenied;
            Assert.That(shutdownDenied.IsError, Is.True);
        }

        [UnityTest, Explicit("Requires explicitly started local LiveKit and private Temp config")]
        public IEnumerator MismatchedEpochRecordMakesNoDeletionOfTheExistingVoiceRoom()
        {
            var configPath = Path.GetFullPath("Temp/ChooGuardVoiceTestConfig.json");
            if (!File.Exists(configPath)) Assert.Ignore("Private local voice integration configuration is absent.");
            var config = JsonUtility.FromJson<VoiceServiceConfig>(File.ReadAllText(configPath));
            records = Path.Combine(Path.GetTempPath(), "cg-voice-epoch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(records);
            var participant = new ParticipantState { ParticipantId = "epoch-check", TeamId = "red", RoleId = "role-01", RegionId = "hall" };
            var world = new WorldState { WorldId = "voice-epoch", ShiftId = "test", Participants = new[] { participant } };
            service = new GameObject("VoiceEpochFixture").AddComponent<ServerVoiceService>(); service.Configure(config, world, records);
            yield return WaitReady(service);
            var grant = service.Grant(participant, "team");
            var connected = new Room(); rooms.Add(connected);
            var join = connected.Connect(grant.Url, grant.Token, new LiveKit.RoomOptions()); yield return join; Assert.That(join.IsError, Is.False);
            var path = Path.Combine(records, "voice-rooms-v1.json"); var original = File.ReadAllText(path);
            var damaged = JsonUtility.FromJson<RoomStateFixture>(original); damaged.Epoch = Guid.NewGuid().ToString("N");
            File.WriteAllText(path, JsonUtility.ToJson(damaged));
            UnityEngine.Object.Destroy(service.gameObject); yield return null;
            service = new GameObject("VoiceBadEpochFixture").AddComponent<ServerVoiceService>();
            try
            {
                service.Configure(config, world, records);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(service.Ready, Is.False);
                Assert.That(service.Status, Does.Contain("기록 확인"));
                Assert.That(connected.ConnectionState, Is.EqualTo(ConnectionState.ConnConnected));
            }
            finally { File.WriteAllText(path, original); }
        }

        private static IEnumerator WaitReady(ServerVoiceService target)
        {
            var deadline = Time.realtimeSinceStartup + 25;
            while (!target.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(target.Ready, Is.True, target.Status);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (tone != null) { tone.Stop(); tone.Dispose(); tone = null; }
            foreach (var room in rooms) room.Disconnect();
            rooms.Clear();
            if (service != null) { yield return service.RetireRooms(); UnityEngine.Object.Destroy(service.gameObject); }
            yield return null;
            if (records != null && Directory.Exists(records)) Directory.Delete(records, true);
        }
    }
}
