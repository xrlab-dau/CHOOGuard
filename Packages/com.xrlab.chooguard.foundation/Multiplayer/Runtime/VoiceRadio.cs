using System;
using System.Collections;
using System.Collections.Generic;
using LiveKit;
using LiveKit.Proto;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer
{
    public sealed class VoiceRadio : MonoBehaviour
    {
        private sealed class Channel
        {
            public VoiceGrant Grant;
            public Room Room;
            public IDisposable Source;
            public LocalAudioTrack Track;
            public bool Ready, Connecting, Closed;
        }
        private readonly Dictionary<string, Channel> channels = new Dictionary<string, Channel>();
        private readonly PushToTalkGate gate = new PushToTalkGate();
        private PlatformAudio audioDevice;
        private NetworkFieldRuntime owner;
        private string selected, status = "무전 연결 대기";
        private bool captureStarting, transmitting;
        private bool fixtureMode;
        private float fixtureClock;
        public bool HasChannels => channels.Count == 2 && channels["team"].Ready && channels["command"].Ready &&
            channels["team"].Room.ConnectionState == ConnectionState.ConnConnected && channels["command"].Room.ConnectionState == ConnectionState.ConnConnected;
        public string Status => status;
        public bool Transmitting => transmitting;

        public void Configure(NetworkFieldRuntime field, bool protocolFixture = false) { owner = field; fixtureMode = protocolFixture; }

        public void ApplyGrant(VoiceGrant grant)
        {
            if (grant == null || grant.Channel != "team" && grant.Channel != "command") return;
            if (channels.TryGetValue(grant.Channel, out var previous))
            {
                if (previous.Grant.Room == grant.Room && !previous.Closed && (previous.Connecting || previous.Ready ||
                    previous.Room.ConnectionState == ConnectionState.ConnReconnecting)) return;
                Close(previous);
            }
            StopTransmission();
            if (!fixtureMode && audioDevice == null)
            {
                // SDK 2.0.0's ADM starts capture in its constructor. Stop it before any track exists.
                var created = new PlatformAudio();
                try { created.StopRecording(); audioDevice = created; }
                catch { created.Dispose(); throw; }
            }
            var channel = new Channel { Grant = grant, Room = new Room(), Connecting = true };
            channels[grant.Channel] = channel;
            channel.Room.Reconnecting += Interrupted;
            channel.Room.Disconnected += Interrupted;
            channel.Room.Reconnected += Reconnected;
            StartCoroutine(Connect(channel));
        }

        private IEnumerator Connect(Channel channel)
        {
            var connect = channel.Room.Connect(channel.Grant.Url, channel.Grant.Token, new LiveKit.RoomOptions { AutoSubscribe = true });
            yield return connect;
            if (channel.Closed) { channel.Room.Disconnect(); channel.Source?.Dispose(); yield break; }
            if (connect.IsError) { channel.Connecting = false; status = "무전 연결 실패"; yield break; }
            if (channel.Grant.CanPublish)
            {
                if (fixtureMode)
                {
                    var source = new ProtocolAudioSource(); channel.Source = source;
                    channel.Track = LocalAudioTrack.CreateAudioTrack("ptt-fixture-tone", source, channel.Room);
                }
                else
                {
                    var source = new PlatformAudioSource(audioDevice, AudioProcessingOptions.Default); channel.Source = source;
                    channel.Track = LocalAudioTrack.CreateAudioTrack("ptt-microphone", source, channel.Room);
                }
                ((ILocalTrack)channel.Track).SetMute(true);
                var publish = channel.Room.LocalParticipant.PublishTrack(channel.Track, new TrackPublishOptions {
                    Source = TrackSource.SourceMicrophone, AudioEncoding = new AudioEncoding { MaxBitrate = 32000 } });
                yield return publish;
                if (channel.Closed) { channel.Room.Disconnect(); channel.Source?.Dispose(); yield break; }
                if (publish.IsError) { channel.Connecting = false; status = "무전 송신 준비 실패"; yield break; }
                ((ILocalTrack)channel.Track).SetMute(true);
            }
            channel.Connecting = false; channel.Ready = true;
            status = "Q 팀 무전 · V 지휘 채널";
        }

        private void Update()
        {
            if (owner == null || !owner.Connected || !owner.LocalInputEnabled || !fixtureMode && !Application.isFocused)
            { if (gate.Held) StopTransmission(); return; }
            if (fixtureMode)
            {
                fixtureClock += Time.unscaledDeltaTime;
                while (fixtureClock >= .02f)
                {
                    fixtureClock -= .02f;
                    if (transmitting && selected != null && channels.TryGetValue(selected, out var selectedChannel))
                        (selectedChannel.Source as ProtocolAudioSource)?.Emit20Milliseconds();
                }
                return;
            }
            if (Input.GetKeyDown(KeyCode.Q)) Press("team");
            if (Input.GetKeyDown(KeyCode.V)) Press("command");
            if (selected == "team" && Input.GetKeyUp(KeyCode.Q) || selected == "command" && Input.GetKeyUp(KeyCode.V)) StopTransmission();
        }

        public void Press(string name)
        {
            StopTransmission();
            if (!channels.TryGetValue(name, out var channel)) return;
            selected = name;
            gate.SetAvailability(channel.Grant.CanPublish && channel.Ready,
                channel.Room.ConnectionState == ConnectionState.ConnConnected, fixtureMode || Application.isFocused,
                owner != null && owner.Connected && owner.LocalInputEnabled);
            var ticket = gate.Press();
            if (gate.CanTransmit(ticket)) StartCoroutine(StartCapture(channel, ticket));
            else status = channel.Grant.CanPublish ? "무전 연결 대기" : "지휘 채널은 수신 전용입니다";
        }

        private IEnumerator StartCapture(Channel channel, int ticket)
        {
            while (captureStarting)
            { if (!gate.CanTransmit(ticket)) yield break; yield return null; }
            if (!gate.CanTransmit(ticket)) yield break;
            if (fixtureMode)
            {
                ((ProtocolAudioSource)channel.Source).Start(); ((ILocalTrack)channel.Track).SetMute(false); transmitting = true;
                status = "프로토콜 시험 음성 송신 중";
                yield break;
            }
            var device = audioDevice;
            captureStarting = true;
            try { yield return device.StartRecording(); }
            finally { captureStarting = false; }
            if (!gate.CanTransmit(ticket) || channel.Room.ConnectionState != ConnectionState.ConnConnected || !isActiveAndEnabled)
            { device.StopRecording(); yield break; }
            ((ILocalTrack)channel.Track).SetMute(false);
            transmitting = true;
            status = selected == "team" ? "팀 무전 송신 중" : "지휘 채널 송신 중";
        }

        public void StopTransmission()
        {
            gate.Release();
            transmitting = false;
            status = HasChannels ? "Q 팀 무전 · V 지휘 채널" : "무전 연결 대기";
            var failed = false;
            foreach (var channel in channels.Values)
                if (channel.Track != null)
                    try { ((ILocalTrack)channel.Track).SetMute(true); if (fixtureMode) (channel.Source as ProtocolAudioSource)?.Stop(); }
                    catch (Exception) { failed = true; }
            try { audioDevice?.StopRecording(); }
            catch (Exception) { failed = true; }
            if (failed)
            {
                foreach (var channel in channels.Values) Close(channel);
                audioDevice?.Dispose(); audioDevice = null;
                status = "무전 종료 오류 · 음성 연결을 닫았습니다";
                Debug.LogError("Voice transmit cleanup failed; rooms and capture device were closed.");
            }
        }
        private void Interrupted(Room room)
        {
            StopTransmission(); status = "무전 연결 중단";
            foreach (var channel in channels.Values) if (channel.Room == room) { channel.Ready = false; channel.Connecting = false; }
        }
        private void Reconnected(Room room)
        {
            StopTransmission(); status = "무전 재연결됨 · 버튼을 다시 누르세요";
            foreach (var channel in channels.Values) if (channel.Room == room) channel.Ready = true;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) StopTransmission(); }
        private void OnDisable() => StopTransmission();
        private void OnDestroy() => DisconnectFromGame();
        public void DisconnectFromGame()
        {
            StopTransmission();
            foreach (var channel in channels.Values) Close(channel);
            channels.Clear(); audioDevice?.Dispose(); audioDevice = null;
        }
        private void Close(Channel channel)
        {
            if (channel.Closed) return;
            channel.Ready = false; channel.Connecting = false; channel.Closed = true;
            channel.Room.Reconnecting -= Interrupted;
            channel.Room.Disconnected -= Interrupted;
            channel.Room.Reconnected -= Reconnected;
            channel.Room.Disconnect();
            channel.Source?.Dispose();
        }
    }
}
