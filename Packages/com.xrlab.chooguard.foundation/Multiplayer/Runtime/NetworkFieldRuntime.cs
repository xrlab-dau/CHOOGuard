using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using ChooGuard.Foundation.Simulation;

namespace ChooGuard.Foundation.Multiplayer
{
    [Serializable] public sealed class FieldInput
    {
        public float X, Z, Yaw;
        public bool Enabled;
        public string[] LoadedRegions = Array.Empty<string>();
    }
    [Serializable] public sealed class FieldView
    {
        public ObservedState Observed;
        public Point3 Position;
        public string TeamId, RoleId;
        public bool Instructor;
        public int ProtocolVersion = 1;
        public string SpatialProfileId = "", RegionId, FrameId, PortalId, MovementStatus;
        public Point3 LocalPosition;
        public string[] RequiredRegions = Array.Empty<string>();
        public ObservedPortalState[] Portals = Array.Empty<ObservedPortalState>();
        public WorldPhysicalView Physical;
        public FoundationTrainingMode TrainingMode;
    }
    [Serializable] public sealed class ObservedPortalState { public string PortalId; public bool Open; }

    /// <summary>Pure guidance projection contract for practice vs evaluation modes.
    /// In Practice mode, requested guidance exposes approved synthetic procedure hints and evidence.
    /// In Evaluation mode, procedure hint fields are strictly excluded from the response.</summary>
    [Serializable] public sealed class TrainingGuidanceState
    {
        public FoundationTrainingMode Mode;
        public string WorldId = "";
        public string ParticipantId = "";
        public string RoleId = "";
        public string CurrentRegionId = "";
        public string TargetRegionId = "";
        public bool Available;
        public string UnavailableReason = "";
        public bool ProcedureHintsVisible;
        public string ProcedureHint = "";
        public string RecommendedAction = "";
        public string EvidenceBasis = "";
        public string SyntheticLabel = "";
    }

    /// <summary>Route/current-location display contract for the connected-world navigation UI.
    /// Every value is derived from the authored synthetic profile; nothing here is surveyed or
    /// facility-accepted. <see cref="Synthetic"/> is always true by construction.</summary>
    [Serializable] public sealed class RouteDisplayState
    {
        public string RegionId = "", RegionLabel = "", FrameId = "", GeometryStatus = "", Classification = "";
        public bool Synthetic = true;
        public string SyntheticLabel = "";
        public string TargetRegionId = "", TargetRegionLabel = "";
        public string[] RouteRegionIds = Array.Empty<string>(), RouteLabels = Array.Empty<string>();
        public string[] RoutePortalIds = Array.Empty<string>(), RouteGeometryStatuses = Array.Empty<string>();
        public bool Available;
        public string UnavailableReason = "";
        public bool CrossesFrame, CrossesLevel;
        public string RouteSyntheticLabel = "";
        public FoundationTrainingMode TrainingMode;
        public bool ProcedureHintsVisible;
        public string ProcedureHint = "";
        public string GuidanceEvidenceBasis = "";
    }
    [Serializable] public sealed class ProbeStep
    {
        public float AtSeconds;
        public string CommandId, TargetId, Argument = "";
        public CommandKind Kind;
        public long ExpectedRevision;
    }
    [Serializable] public sealed class ReportRow
    {
        public string ReportId = "", FromParticipantId = "", ToTeamId = "", EntityId = "", RegionId = "", FrameId = "";
        public long ObservedRevision, Sequence;
        public string[] AcknowledgedBy = Array.Empty<string>();
        public bool AcknowledgedByLocal;
    }
    [Serializable] public sealed class EvacueeRow
    {
        public string EntityId = "", RegionId = "", LeaderId = "";
        public long Revision;
        public bool Unclaimed, LedByLocal;
    }
    [Serializable] public sealed class PendingCommandRow
    {
        public string CommandId = "", TargetId = "", Argument = "", RegionId = "";
        public CommandKind Kind;
        public long ExpectedRevision;
    }
    [Serializable] public sealed class CommandResultRow
    {
        public string CommandId = "", TargetId = "", Argument = "", Text = "";
        public CommandKind Kind;
        public CommandCode Code;
        public bool Accepted;
        public long Sequence;
    }

    /// <summary>Client report/acknowledgement/escort/hand-off screen state. Rows come only from the latest
    /// server-projected view and results only from server receipts for commands this client sent; nothing is
    /// shown optimistically. Commands can target only reports and entities present in that view.</summary>
    public sealed class FieldCommandPanel
    {
        public const int ResultCapacity = 8;
        private readonly List<PendingCommandRow> pending = new List<PendingCommandRow>();
        private readonly List<CommandResultRow> results = new List<CommandResultRow>();
        private FieldView view;
        public string ParticipantId { get; }
        public string TeamId => view?.TeamId ?? "";
        public ReportRow[] Reports { get; private set; } = Array.Empty<ReportRow>();
        public EvacueeRow[] Evacuees { get; private set; } = Array.Empty<EvacueeRow>();
        public string[] ReportableIncidentIds { get; private set; } = Array.Empty<string>();
        public PendingCommandRow[] Pending => pending.ToArray();
        public CommandResultRow[] Results => results.ToArray();
        public int IgnoredViews { get; private set; }
        public int IgnoredReports { get; private set; }
        public int IgnoredReceipts { get; private set; }

        public FieldCommandPanel(string participantId)
        {
            if (string.IsNullOrEmpty(participantId)) throw new ArgumentException("A local participant is required.", nameof(participantId));
            ParticipantId = participantId;
        }

        public void ApplyView(FieldView projected)
        {
            if (projected?.Observed == null || projected.Observed.ParticipantId != ParticipantId || string.IsNullOrEmpty(projected.TeamId) ||
                view != null && (projected.Observed.WorldId != view.Observed.WorldId || projected.Observed.ShiftId != view.Observed.ShiftId))
            { IgnoredViews++; return; }
            view = projected;
            var reports = projected.Observed.Reports ?? Array.Empty<TeamReport>();
            IgnoredReports += reports.Count(r => r == null || r.ToTeamId != projected.TeamId);
            Reports = reports.Where(r => r != null && r.ToTeamId == projected.TeamId).OrderBy(r => r.Sequence).Select(r => new ReportRow {
                ReportId = r.ReportId ?? "", FromParticipantId = r.FromParticipantId ?? "", ToTeamId = r.ToTeamId, EntityId = r.EntityId ?? "",
                RegionId = r.RegionId ?? "", FrameId = r.FrameId ?? "", ObservedRevision = r.ObservedRevision, Sequence = r.Sequence,
                AcknowledgedBy = (r.AcknowledgedBy ?? Array.Empty<string>()).ToArray(),
                AcknowledgedByLocal = (r.AcknowledgedBy ?? Array.Empty<string>()).Contains(ParticipantId) }).ToArray();
            var entities = projected.Observed.Entities ?? Array.Empty<EntityState>();
            Evacuees = entities.Where(e => e != null && e.Kind == EntityKind.Evacuee).OrderBy(e => e.EntityId, StringComparer.Ordinal)
                .Select(e => new EvacueeRow { EntityId = e.EntityId, RegionId = e.RegionId ?? "", LeaderId = e.LeaderId ?? "", Revision = e.Revision,
                    Unclaimed = string.IsNullOrEmpty(e.LeaderId), LedByLocal = e.LeaderId == ParticipantId }).ToArray();
            ReportableIncidentIds = entities.Where(e => e != null && e.Kind == EntityKind.Incident && e.Active)
                .Select(e => e.EntityId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        /// <summary>Report payload: entity id, observed revision and recipient team. The protocol has no region field;
        /// the server derives the report region from its authoritative target, and the report row shows that value.</summary>
        public WorldCommand Report(string incidentId, string recipientTeamId = "", string commandId = null)
        {
            var target = ObservedEntity(incidentId);
            return target == null || target.Kind != EntityKind.Incident || !target.Active ? null :
                Command(CommandKind.Report, target.EntityId, recipientTeamId ?? "", target.Revision, commandId);
        }

        public WorldCommand Acknowledge(string reportId, string commandId = null) =>
            Reports.Any(r => r.ReportId == reportId) ? Command(CommandKind.AcknowledgeReport, reportId, "", 0, commandId) : null;

        public WorldCommand Claim(string evacueeId, string commandId = null)
        {
            var row = Evacuees.SingleOrDefault(e => e.EntityId == evacueeId);
            return row == null || !row.Unclaimed ? null : Command(CommandKind.ClaimEvacuee, row.EntityId, "", row.Revision, commandId);
        }

        public WorldCommand HandOff(string evacueeId, string toParticipantId, string commandId = null)
        {
            var row = Evacuees.SingleOrDefault(e => e.EntityId == evacueeId);
            return row == null || !row.LedByLocal || string.IsNullOrEmpty(toParticipantId) || toParticipantId == ParticipantId ? null :
                Command(CommandKind.HandOffEvacuee, row.EntityId, toParticipantId, row.Revision, commandId);
        }

        /// <summary>R key: acknowledge the oldest shown team report the local participant has not acknowledged.</summary>
        public WorldCommand AcknowledgeNext(string commandId = null)
        {
            var next = Reports.FirstOrDefault(r => !r.AcknowledgedByLocal);
            return next == null ? null : Acknowledge(next.ReportId, commandId);
        }

        /// <summary>H key: hand the first evacuee the server shows as led by the local participant to <paramref name="toParticipantId"/>.</summary>
        public WorldCommand HandOffLed(string toParticipantId, string commandId = null)
        {
            var led = Evacuees.FirstOrDefault(e => e.LedByLocal);
            return led == null ? null : HandOff(led.EntityId, toParticipantId, commandId);
        }

        /// <summary>Call only after the command has actually been sent to the server.</summary>
        public void Track(WorldCommand command)
        {
            if (command == null || command.ParticipantId != ParticipantId || pending.Any(p => p.CommandId == command.CommandId)) return;
            var region = command.Kind == CommandKind.AcknowledgeReport ? Reports.SingleOrDefault(r => r.ReportId == command.TargetId)?.RegionId
                : ObservedEntity(command.TargetId)?.RegionId;
            pending.Add(new PendingCommandRow { CommandId = command.CommandId, Kind = command.Kind, TargetId = command.TargetId ?? "",
                Argument = command.Argument ?? "", ExpectedRevision = command.ExpectedRevision, RegionId = region ?? "" });
        }

        /// <summary>Shows a server receipt only when it answers a command this client sent in the current shift.</summary>
        public bool ApplyReceipt(CommandReceipt receipt)
        {
            var sent = receipt == null ? null : pending.SingleOrDefault(p => p.CommandId == receipt.CommandId);
            if (sent == null || receipt.ParticipantId != ParticipantId || view == null ||
                receipt.WorldId != view.Observed.WorldId || receipt.ShiftId != view.Observed.ShiftId)
            { IgnoredReceipts++; return false; }
            pending.Remove(sent);
            results.Add(new CommandResultRow { CommandId = sent.CommandId, Kind = sent.Kind, TargetId = sent.TargetId, Argument = sent.Argument,
                Code = receipt.Code, Accepted = receipt.Code == CommandCode.Accepted, Sequence = receipt.Sequence,
                Text = (receipt.Code == CommandCode.Accepted ? "서버 승인" : "서버 거부(" + receipt.Code + ")") + ": " + KindLabel(sent.Kind) + " " + sent.TargetId +
                    (string.IsNullOrEmpty(sent.Argument) ? "" : " → " + sent.Argument) });
            if (results.Count > ResultCapacity) results.RemoveAt(0);
            return true;
        }

        public string[] RenderLines()
        {
            var lines = new List<string> { "보고·수신확인·인계 · 서버 승인 상태만 표시" };
            lines.AddRange(Reports.Select(r => "팀 보고 " + r.ReportId + ": " + r.EntityId + " / " + r.RegionId + " · 관측 rev " + r.ObservedRevision +
                " · 보고자 " + r.FromParticipantId + " · 수신확인 " + (r.AcknowledgedBy.Length == 0 ? "없음" : string.Join(", ", r.AcknowledgedBy))));
            lines.AddRange(Evacuees.Select(e => "대피자 " + e.EntityId + " / " + e.RegionId + " · " +
                (e.Unclaimed ? "인솔자 없음" : e.LedByLocal ? "내가 인솔 중" : "인솔자 " + e.LeaderId) + " · rev " + e.Revision));
            lines.AddRange(pending.Select(p => "서버 확인 대기: " + KindLabel(p.Kind) + " " + p.TargetId));
            lines.AddRange(results.Select(r => r.Text));
            return lines.ToArray();
        }

        public static string KindLabel(CommandKind kind) =>
            kind == CommandKind.Report ? "보고" : kind == CommandKind.AcknowledgeReport ? "수신확인" :
            kind == CommandKind.ClaimEvacuee ? "인솔" : kind == CommandKind.HandOffEvacuee ? "인계" : kind.ToString();

        private EntityState ObservedEntity(string entityId) =>
            view?.Observed?.Entities?.SingleOrDefault(e => e != null && e.EntityId == entityId);

        private WorldCommand Command(CommandKind kind, string targetId, string argument, long revision, string commandId)
        {
            if (view?.Observed == null) return null;
            return new WorldCommand { WorldId = view.Observed.WorldId, ShiftId = view.Observed.ShiftId, ParticipantId = ParticipantId,
                TeamId = view.TeamId, CommandId = string.IsNullOrEmpty(commandId) ? Guid.NewGuid().ToString("N") : commandId,
                Kind = kind, TargetId = targetId, Argument = argument, ExpectedRevision = revision };
        }
    }

    [Serializable] public sealed class VoiceProbeStep { public float AtSeconds; public string Channel; public bool Held; }
    [Serializable] public sealed class ProbeWaypoint { public Point3 Position; public string RegionId, TargetId; public int Operations; public float WaitSeconds; }
    [Serializable] public sealed class ProbePlan { public ProbeStep[] Steps; public VoiceProbeStep[] VoiceSteps;
        public ProbeWaypoint[] Waypoints; public bool ExitWhenRouteComplete; public float ExitAfterSeconds = 10; }

    /// <summary>Real NGO protocol adapter. Clients receive only projected FieldView, never WorldState.</summary>
    public sealed class NetworkFieldRuntime : MonoBehaviour
    {
        private const string InputChannel = "cg.field.input.v1", CommandChannel = "cg.field.command.v1",
            ViewChannel = "cg.field.view.v1", ReceiptChannel = "cg.field.receipt.v1";
        private const string VoiceRequestChannel = "cg.voice.request.v1", VoiceGrantChannel = "cg.voice.grant.v1";
        private const string SnapshotChannel = "cg.field.snapshot.v3";
        private const float TickSeconds = .05f;
        private readonly Dictionary<ulong, string> identities = new Dictionary<ulong, string>();
        private readonly Dictionary<ulong, FieldInput> inputs = new Dictionary<ulong, FieldInput>();
        private readonly Dictionary<ulong, double> lastInput = new Dictionary<ulong, double>();
        private readonly Dictionary<ulong, Queue<double>> arrivals = new Dictionary<ulong, Queue<double>>();
        private NetworkManager network;
        private UnityTransport transport;
        private AuthoritativeShift shift;
        private ShiftJournal journal;
        private SimulationJournal simulationJournal;
        private FoundationWorldSimulation simulation;
        private readonly SnapshotAssembler snapshotAssembler = new SnapshotAssembler();
        private readonly SnapshotFreshness snapshotFreshness = new SnapshotFreshness();
        private bool snapshotStale;
        private long snapshotSerial;
        public GameObject BodyPrefab;
        public Shader SmokeShader;
        private WorldBodyView bodyView;
        private WorldSmokeView smokeView;
        private string cameraFrame;
        private Vector3 cameraLocal;
        private RuntimeMetricAccumulator metrics;
        private string metricsPath;
        private string phaseProfilePath;
        private double metricsClosedAt;
        private SessionAdmission admission;
        private JoinCredential credential;
        private FieldView view;
        private Camera cameraView;
        private string status = "근무 연결 준비", evidencePath;
        private bool controls, server, focused;
        private float pitch, yaw, accumulator, inputClock;
        private double startedAt;
        private ProbePlan probe;
        private readonly ProbeStepCursor probeCursor = new ProbeStepCursor();
        private long sentBytes, receivedBytes;
        private ServerVoiceService voiceService;
        private VoiceRadio voiceRadio;
        private float voiceRequestClock;
        private bool stopping, shutdownRunning, quitAllowed;
        private float exitAfterSeconds;
        private int nextVoiceProbe;
        private string previousVoiceEvidence;
        private ConnectedWorldRuntime connectedWorld;
        private readonly Dictionary<ulong, string> movementStatus = new Dictionary<ulong, string>();
        private float checkpointClock;
        private int nextWaypoint, waypointOperations;
        private double waypointArrivedAt = -1, probeCommandAt;
        private string pendingProbeCommand, navigationTarget;
        private bool navigationMenu;
        private bool spatialPersistenceFault;
        private Vector2 navigationScroll;
        private FieldCommandPanel commandPanel;
        private string handOffTarget = "";
        public FieldView CurrentView => view;
        public FieldCommandPanel CommandPanel => commandPanel;
        public bool Connected => network != null && network.IsConnectedClient;
        public bool LocalInputEnabled => controls && snapshotFreshness.IsCurrent(Time.realtimeSinceStartupAsDouble);

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            focused = Application.isFocused;
            startedAt = Time.realtimeSinceStartupAsDouble;
            float.TryParse(Argument("--cg-exit-after", "0"), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out exitAfterSeconds);
            cameraView = Camera.main;
            connectedWorld = GetComponent<ConnectedWorldRuntime>();
            var mode = Argument("--cg-mode");
            evidencePath = Argument("--cg-evidence");
            if (connectedWorld != null) yield return connectedWorld.Prepare(mode == "server");
            if (string.IsNullOrEmpty(mode)) yield break;
            try { StartProtocol(mode); }
            catch (Exception exception)
            {
                status = "근무 연결 실패";
                Debug.LogError("Foundation multiplayer startup failed: " + exception.GetType().Name + ": " + exception.Message);
                if (Application.isBatchMode) Application.Quit(2);
            }
        }

        private void StartProtocol(string mode)
        {
            if (mode != "server" && mode != "client") throw new ArgumentException("Expected server or client mode.");
            server = mode == "server";
            network = gameObject.AddComponent<NetworkManager>();
            transport = gameObject.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            network.NetworkConfig.TickRate = 20;
            network.NetworkConfig.ConnectionApproval = true;
            network.NetworkConfig.EnableSceneManagement = false;
            network.NetworkConfig.ForceSamePrefabs = false;
            network.NetworkConfig.ClientConnectionBufferTimeout = 10;
            var address = Argument("--cg-address", "127.0.0.1");
            var listen = Argument("--cg-listen", "127.0.0.1");
            if (!ushort.TryParse(Argument("--cg-port", "7777"), out var port) || port == 0)
                throw new ArgumentException("Invalid transport port.");
            var certificate = Argument("--cg-certificate");
            var ca = Argument("--cg-ca");
            var encrypted = server ? !string.IsNullOrEmpty(certificate) : !string.IsNullOrEmpty(ca);
            var endpoint = server ? listen : address;
            if (!encrypted && (!IPAddress.TryParse(endpoint, out var ip) || !IPAddress.IsLoopback(ip)))
                throw new InvalidOperationException("A non-loopback connection requires a validated encrypted transport.");
            transport.UseEncryption = encrypted;
            if (encrypted)
            {
                if (server) transport.SetServerSecrets(File.ReadAllText(certificate), File.ReadAllText(Required("--cg-private-key")));
                else transport.SetClientSecrets(Required("--cg-hostname"), File.ReadAllText(ca));
            }
            transport.SetConnectionData(true, address, port, listen);
            network.OnClientConnectedCallback += OnConnected;
            network.OnClientDisconnectCallback += OnDisconnected;
            if (server)
            {
                var config = JsonUtility.FromJson<ServerSessionConfig>(File.ReadAllText(Required("--cg-session")));
                admission = new SessionAdmission(config);
                connectedWorld?.ValidateSession(config.World);
                var physicalProfile = Argument("--cg-simulation");
                if (!string.IsNullOrEmpty(physicalProfile))
                {
                    if (connectedWorld == null) throw new InvalidDataException("Physical simulation requires the connected world.");
                    simulation = new FoundationWorldSimulation(connectedWorld.Definition, connectedWorld.RegionViews,
                        JsonUtility.FromJson<FoundationSimulationProfile>(File.ReadAllText(physicalProfile)), config.World);
                    simulationJournal = new SimulationJournal(Required("--cg-records"));
                    shift = simulationJournal.Open(simulation.InitialState, ServerCanSee, simulation.CanOperate);
                    simulation.Restore(shift.ExportCheckpoint());
                }
                else
                {
                    journal = new ShiftJournal(Required("--cg-records"));
                    shift = journal.Open(config.World, ServerCanSee, target => connectedWorld == null ||
                        connectedWorld.CanOperate(target, shift.ExportCheckpoint().Participants));
                    if (connectedWorld != null)
                    { connectedWorld.ValidateSession(shift.ExportCheckpoint()); connectedWorld.ApplyServerState(shift.ExportCheckpoint()); }
                }
                var restoredRoster = shift.ExportCheckpoint().Participants;
                if (!restoredRoster.OrderBy(p => p.ParticipantId).Select(RosterKey).SequenceEqual(
                    config.World.Participants.OrderBy(p => p.ParticipantId).Select(RosterKey)))
                    throw new InvalidDataException("Session admission roster differs from the restored checkpoint.");
                foreach (var participant in restoredRoster) shift.SetInputEnabled(participant.ParticipantId, false);
                network.ConnectionApprovalCallback = Approve;
                if (!network.StartServer()) throw new InvalidOperationException("Dedicated server failed to start.");
                Register(InputChannel, ReceiveInput);
                Register(CommandChannel, ReceiveCommand);
                Register(VoiceRequestChannel, ReceiveVoiceRequest);
                var voiceConfig = Argument("--cg-voice-config");
                if (!string.IsNullOrEmpty(voiceConfig))
                {
                    voiceService = gameObject.AddComponent<ServerVoiceService>();
                    voiceService.Configure(JsonUtility.FromJson<VoiceServiceConfig>(File.ReadAllText(voiceConfig)),
                        config.World, Required("--cg-records"));
                    Application.wantsToQuit += MayQuit;
                }
                status = "근무 서버 연결 가능";
                Evidence("server_started", new { });
            }
            else
            {
                credential = JsonUtility.FromJson<JoinCredential>(File.ReadAllText(Required("--cg-credential")));
                if (credential == null) throw new InvalidDataException("Invalid join credential file.");
                commandPanel = new FieldCommandPanel(credential.ParticipantId);
                network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(credential));
                var plan = Argument("--cg-probe");
                if (!string.IsNullOrEmpty(plan)) probe = JsonUtility.FromJson<ProbePlan>(File.ReadAllText(plan));
                if (!network.StartClient()) throw new InvalidOperationException("Client failed to start.");
                Register(ViewChannel, ReceiveView);
                Register(SnapshotChannel, ReceiveSnapshot);
                Register(ReceiptChannel, ReceiveReceipt);
                Register(VoiceGrantChannel, ReceiveVoiceGrant);
                voiceRadio = gameObject.AddComponent<VoiceRadio>();
                voiceRadio.Configure(this, Environment.GetCommandLineArgs().Contains("--cg-voice-fixture"));
                bodyView = gameObject.AddComponent<WorldBodyView>(); bodyView.Configure(BodyPrefab, connectedWorld);
                smokeView = gameObject.AddComponent<WorldSmokeView>(); smokeView.Configure(SmokeShader, cameraView, connectedWorld);
                status = "근무 연결 중";
            }
            metricsPath = Argument("--cg-metrics");
            phaseProfilePath = server ? Argument("--cg-phase-profile") : null;
            if (simulation != null) simulation.ProfilePhases = !string.IsNullOrEmpty(phaseProfilePath);
            if (!string.IsNullOrEmpty(phaseProfilePath)) Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(phaseProfilePath)));
            if (!string.IsNullOrEmpty(metricsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(metricsPath)));
                metricsClosedAt = Time.realtimeSinceStartupAsDouble;
                if (server) metrics = new RuntimeMetricAccumulator("server", metricsClosedAt, DateTime.UtcNow,
                    simulation?.Tick ?? 0, sentBytes, receivedBytes, Application.isBatchMode, SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null);
            }
        }

        private void Register(string channel, CustomMessagingManager.HandleNamedMessageDelegate handler) =>
            network.CustomMessagingManager.RegisterNamedMessageHandler(channel, handler);

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false; response.CreatePlayerObject = false; response.Pending = false;
            response.Reason = "Admission denied";
            if (stopping) return;
            if (request.Payload == null || request.Payload.Length > 2048 || identities.Count >= 20) return;
            JoinCredential ticket;
            try { ticket = JsonUtility.FromJson<JoinCredential>(new UTF8Encoding(false, true).GetString(request.Payload)); }
            catch (Exception) { return; }
            if (!admission.Allows(ticket) || identities.Values.Contains(ticket.ParticipantId)) return;
            identities.Add(request.ClientNetworkId, ticket.ParticipantId);
                inputs[request.ClientNetworkId] = new FieldInput();
            lastInput[request.ClientNetworkId] = Time.realtimeSinceStartupAsDouble;
            response.Approved = true; response.Reason = "";
        }

        private void OnConnected(ulong client)
        {
            if (server)
            {
                if (!identities.ContainsKey(client)) { network.DisconnectClient(client); return; }
                // Admission does not establish that the first observation and its region scenes are ready.
                shift.SetInputEnabled(identities[client], false);
                SendView(client);
                Evidence("participant_joined", new ParticipantEvent { ParticipantId = identities[client] });
            }
            else if (client == network.LocalClientId)
            {
                controls = Application.isBatchMode || focused; status = "근무 연결됨";
                if (!Application.isBatchMode) Cursor.lockState = controls ? CursorLockMode.Locked : CursorLockMode.None;
            }
        }

        private void OnDisconnected(ulong client)
        {
            if (server && identities.TryGetValue(client, out var participant))
            {
                shift.SetInputEnabled(participant, false);
                identities.Remove(client); inputs.Remove(client); lastInput.Remove(client); arrivals.Remove(client); movementStatus.Remove(client);
                if (!stopping) voiceService?.RevokeConnection(participant);
                Evidence("participant_left", new ParticipantEvent { ParticipantId = participant });
            }
            else if (!server)
            { controls = false; voiceRadio?.DisconnectFromGame(); Cursor.lockState = CursorLockMode.None; status = "연결이 끊겼습니다. 같은 초대로 다시 참가하세요."; }
        }

        private bool Permit(ulong sender, FastBufferReader reader)
        {
            var remaining = reader.Length - reader.Position;
            if (!identities.ContainsKey(sender) || remaining <= 0 || remaining > 4096) { network.DisconnectClient(sender); return false; }
            if (!arrivals.TryGetValue(sender, out var queue)) arrivals[sender] = queue = new Queue<double>();
            var now = Time.realtimeSinceStartupAsDouble;
            while (queue.Count > 0 && now - queue.Peek() > 1) queue.Dequeue();
            if (queue.Count >= 80) { network.DisconnectClient(sender); return false; }
            queue.Enqueue(now); receivedBytes += remaining;
            return true;
        }

        private void ReceiveInput(ulong sender, FastBufferReader reader)
        {
            if (!Permit(sender, reader)) return;
            try
            {
                reader.ReadValueSafe(out string text);
                var input = JsonUtility.FromJson<FieldInput>(text);
                if (input == null || !Finite(input.X) || !Finite(input.Z) || !Finite(input.Yaw)) throw new InvalidDataException();
                if (connectedWorld != null && (input.LoadedRegions == null || input.LoadedRegions.Length > 13 ||
                    input.LoadedRegions.Any(id => !connectedWorld.Definition.Regions.Any(r => r.Id == id)))) throw new InvalidDataException();
                var movement = Vector2.ClampMagnitude(new Vector2(input.X, input.Z), 1);
                input.X = movement.x; input.Z = movement.y;
                inputs[sender] = input; lastInput[sender] = Time.realtimeSinceStartupAsDouble;
                var ready=connectedWorld==null || connectedWorld.Definition.RequiredRegions(shift.Participant(identities[sender]).RegionId).All(id=>input.LoadedRegions.Contains(id));
                shift.SetInputEnabled(identities[sender], input.Enabled && !spatialPersistenceFault && ready);
                if (simulation!=null) movementStatus[sender]=ready ? "" : "현재 구역을 불러오는 중입니다";
            }
            catch (Exception) { network.DisconnectClient(sender); }
        }

        private void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!Permit(sender, reader)) return;
            try
            {
                reader.ReadValueSafe(out string text);
                var command = JsonUtility.FromJson<WorldCommand>(text);
                var receipt = shift.Submit(identities[sender], command);
                if (connectedWorld != null && simulation == null && receipt.Code == CommandCode.Accepted)
                    connectedWorld.ApplyServerState(shift.ExportCheckpoint());
                Send(ReceiptChannel, sender, JsonUtility.ToJson(receipt));
                Evidence("receipt", receipt);
            }
            catch (Exception exception)
            {
                Debug.LogError("Server command processing stopped: " + exception.GetType().Name);
                network.DisconnectClient(sender);
            }
        }

        private void ReceiveView(ulong sender, FastBufferReader reader)
        {
            var remaining = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || remaining <= 0 || remaining > 65536) return;
            reader.ReadValueSafe(out string text); receivedBytes += remaining;
            view = JsonUtility.FromJson<FieldView>(text);
            commandPanel?.ApplyView(view);
            connectedWorld?.ApplyView(view);
            snapshotFreshness.Received(Time.realtimeSinceStartupAsDouble);
            StartClientMetrics();
            Evidence("view", view);
        }
        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            // NGO has consumed the named-message hash before invoking this handler.
            // Length still includes that prefix; only Position..Length belongs to our fragment.
            var remaining = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || remaining < SnapshotFragments.HeaderBytes ||
                remaining > SnapshotFragments.HeaderBytes + SnapshotFragments.FragmentPayloadBytes) return;
            try
            {
                var packet = new byte[remaining]; reader.ReadBytesSafe(ref packet, packet.Length); receivedBytes += packet.Length;
                if (snapshotAssembler.Accept(packet, (long)(Time.realtimeSinceStartupAsDouble * 1000), out var payload, out _) != FragmentResult.Complete) return;
                var projected = JsonUtility.FromJson<FieldView>(SnapshotPayload.Decode(payload));
                if (projected?.Observed == null || projected.Physical == null || projected.ProtocolVersion != 3 || projected.Observed.ParticipantId != credential.ParticipantId ||
                    projected.Observed.WorldId != credential.WorldId || projected.Observed.ShiftId != credential.ShiftId) throw new InvalidDataException("Invalid participant snapshot.");
                connectedWorld.ApplyView(projected); view = projected; commandPanel?.ApplyView(view); snapshotFreshness.Received(Time.realtimeSinceStartupAsDouble);
                StartClientMetrics();
                bodyView?.Apply(view); smokeView?.Apply(view); Evidence("view", view);
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidDataException || e is OverflowException)
            { Debug.LogError("Invalid server snapshot: " + e.GetType().Name); controls = false; network.Shutdown(); }
        }
        private void ReceiveReceipt(ulong sender, FastBufferReader reader)
        {
            var remaining = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || remaining <= 0 || remaining > 4096) return;
            reader.ReadValueSafe(out string text); receivedBytes += remaining;
            var receipt = JsonUtility.FromJson<CommandReceipt>(text);
            commandPanel?.ApplyReceipt(receipt);
            if (receipt.CommandId == pendingProbeCommand)
            {
                if (receipt.Code == CommandCode.Accepted) { waypointOperations++; pendingProbeCommand = null; }
                else { Evidence("waypoint_failed", receipt); pendingProbeCommand = null; }
            }
            status = receipt.Code == CommandCode.Accepted ? "행동 확인됨" : "행동 확인: " + receipt.Code;
            Evidence("receipt", receipt);
        }

        [Serializable] private sealed class VoiceRequest { public string Channel; }
        private void ReceiveVoiceRequest(ulong sender, FastBufferReader reader)
        {
            if (!Permit(sender, reader) || voiceService == null || !voiceService.Ready) return;
            try
            {
                reader.ReadValueSafe(out string text);
                var request = JsonUtility.FromJson<VoiceRequest>(text);
                if (request == null || request.Channel != "team" && request.Channel != "command") throw new ArgumentException();
                var grant = voiceService.Grant(shift.Participant(identities[sender]), request.Channel);
                if (grant != null) Send(VoiceGrantChannel, sender, JsonUtility.ToJson(grant));
            }
            catch (Exception) { network.DisconnectClient(sender); }
        }
        private void ReceiveVoiceGrant(ulong sender, FastBufferReader reader)
        {
            var remaining = reader.Length - reader.Position;
            if (sender != NetworkManager.ServerClientId || remaining <= 0 || remaining > 8192) return;
            reader.ReadValueSafe(out string text); receivedBytes += remaining;
            voiceRadio.ApplyGrant(JsonUtility.FromJson<VoiceGrant>(text));
            // Never put room tokens or API credentials in diagnostic records.
            Evidence("voice_grant_received", new ParticipantEvent { ParticipantId = credential.ParticipantId });
        }

        private void Update()
        {
            if (exitAfterSeconds > 0 && Time.realtimeSinceStartupAsDouble - startedAt >= exitAfterSeconds)
            { exitAfterSeconds = 0; Application.Quit(); }
            if (network == null || !network.IsListening) return;
            if (server)
            {
                var frameDeltaMs = Time.unscaledDeltaTime * 1000.0;
                accumulator += Time.unscaledDeltaTime;
                var ticks = 0;
                var drainStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                while (ServerCatchupBudget.CanContinue(accumulator, ticks,
                    (System.Diagnostics.Stopwatch.GetTimestamp()-drainStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency))
                {
                    ticks++;
                    accumulator -= TickSeconds;
                    var tickStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                    TickServer();
                    RecordSimulationTickMetrics((System.Diagnostics.Stopwatch.GetTimestamp()-tickStarted)*1000.0/System.Diagnostics.Stopwatch.Frequency);
                }
                RecordFrameMetrics(frameDeltaMs);
                return;
            }
            RecordFrameMetrics(Time.unscaledDeltaTime*1000);
            if (!Connected) return;
            var stale=!snapshotFreshness.IsCurrent(Time.realtimeSinceStartupAsDouble);
            if (stale && !snapshotStale) { voiceRadio?.StopTransmission(); status="현장 상태 수신 대기 · 이동을 멈췄습니다"; }
            else if (!stale && snapshotStale) status="현장 상태 수신됨";
            snapshotStale=stale;
            if (voiceRadio != null)
            {
                var diagnostic = JsonUtility.ToJson(new VoiceEvidence { Connected = voiceRadio.HasChannels,
                    Transmitting = voiceRadio.Transmitting, Status = voiceRadio.Status });
                if (diagnostic != previousVoiceEvidence)
                { previousVoiceEvidence = diagnostic; Evidence("voice_state", JsonUtility.FromJson<VoiceEvidence>(diagnostic)); }
            }
            voiceRequestClock += Time.unscaledDeltaTime;
            if (voiceRadio != null && !voiceRadio.HasChannels && voiceRequestClock >= 2)
            {
                voiceRequestClock = 0;
                foreach (var channel in new[] { "team", "command" })
                    Send(VoiceRequestChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(new VoiceRequest { Channel = channel }));
            }
            if (!Application.isBatchMode && Input.GetKeyDown(KeyCode.Escape)) SetControls(!controls);
            if (!Application.isBatchMode && Input.GetKeyDown(KeyCode.N)) { navigationMenu = !navigationMenu; SetControls(!navigationMenu); }
            if (controls && !Application.isBatchMode)
            {
                yaw += Input.GetAxisRaw("Mouse X") * 2;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * 2, -80, 80);
                if (Input.GetKeyDown(KeyCode.E)) InteractClosest();
                // Commands need active input on the server, so panel actions are keys while controls are active.
                if (commandPanel != null && Input.GetKeyDown(KeyCode.R)) SubmitPanelCommand(commandPanel.AcknowledgeNext());
                if (commandPanel != null && Input.GetKeyDown(KeyCode.H)) SubmitPanelCommand(commandPanel.HandOffLed((handOffTarget ?? "").Trim()));
            }
            inputClock += Time.unscaledDeltaTime;
            if (inputClock >= TickSeconds)
            {
                inputClock %= TickSeconds;
                var direction = ProbeMovement();
                Send(InputChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(new FieldInput {
                    Enabled = LocalInputEnabled, X = probe?.Waypoints != null ? direction.x : controls && !Application.isBatchMode ? Input.GetAxisRaw("Horizontal") : 0,
                    Z = probe?.Waypoints != null ? direction.y : controls && !Application.isBatchMode ? Input.GetAxisRaw("Vertical") : 0,
                    Yaw = probe?.Waypoints != null ? 0 : yaw, LoadedRegions = connectedWorld?.LoadedRegionIds ?? Array.Empty<string>() }));
            }
            if (cameraView != null && view != null)
            {
                var frame = view.Physical?.Frames.SingleOrDefault(f => f.FrameId == view.FrameId);
                if (frame != null)
                {
                    if (cameraFrame != frame.FrameId) { cameraFrame = frame.FrameId; cameraLocal = Vector(view.LocalPosition); }
                    cameraLocal = Vector3.Lerp(cameraLocal, Vector(view.LocalPosition), 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime));
                    cameraView.transform.position = Vector(frame.ToWorld(Point(cameraLocal))) + Vector3.up * 1.6f;
                }
                else cameraView.transform.position = Vector3.Lerp(cameraView.transform.position, Vector(view.Position) + Vector3.up * 1.6f, 1 - Mathf.Exp(-20 * Time.unscaledDeltaTime));
                cameraView.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            if (probe != null && view != null)
            {
                var elapsed = (float)(Time.realtimeSinceStartupAsDouble - startedAt);
                probeCursor.Drain(probe.Steps, elapsed, TrySubmit);
                if (voiceRadio != null && probe.VoiceSteps != null)
                    while (nextVoiceProbe < probe.VoiceSteps.Length && probe.VoiceSteps[nextVoiceProbe].AtSeconds <= elapsed)
                    {
                        var step = probe.VoiceSteps[nextVoiceProbe++];
                        if (step.Held) voiceRadio.Press(step.Channel); else voiceRadio.StopTransmission();
                    }
                if (elapsed >= probe.ExitAfterSeconds) Application.Quit();
            }
        }

        private void TickServer()
        {
            if (simulation != null) { TickSimulation(); return; }
            if (spatialPersistenceFault)
            { foreach (var client in identities.Keys.ToArray()) if (network.ConnectedClients.ContainsKey(client)) SendView(client); return; }
            var snapshot = connectedWorld == null ? null : shift.ExportCheckpoint();
            var closed = snapshot == null ? null : connectedWorld.ClosedPortals(snapshot);
            foreach (var client in identities.Keys.ToArray())
            {
                if (!network.ConnectedClients.ContainsKey(client)) continue;
                var input = inputs[client];
                if (Time.realtimeSinceStartupAsDouble - lastInput[client] > .5)
                { input.Enabled = false; shift.SetInputEnabled(identities[client], false); }
                var actor = shift.Participant(identities[client]);
                if (!shift.Paused && input.Enabled)
                {
                    var offset = Quaternion.Euler(0, input.Yaw, 0) * new Vector3(input.X, 0, input.Z) * (3 * TickSeconds);
                    if (connectedWorld != null)
                    {
                        var previous = ActorPose(actor);
                        if (GroundedWorldMotor.TryMove(connectedWorld.Definition, previous, offset,
                            new HashSet<string>(input.LoadedRegions), closed, out var next))
                        {
                            shift.SetServerPose(actor.ParticipantId, next);
                            movementStatus[client] = "";
                            if (next.RegionId != previous.RegionId || next.FrameId != previous.FrameId || next.PortalId != previous.PortalId)
                            {
                                if (!SaveSpatialCheckpoint()) shift.SetServerPose(actor.ParticipantId, previous);
                                else Evidence("region_transition", next);
                            }
                        }
                        else movementStatus[client] = input.LoadedRegions.Contains(actor.RegionId) ? "통로 경계 또는 바닥 지지 확인 중" : "현재 구역 로딩 중";
                    }
                    else
                    {
                        var next = Vector(actor.Position) + offset;
                        if (!Physics.CheckCapsule(next + Vector3.up * .4f, next + Vector3.up * 1.4f, .3f, ~0, QueryTriggerInteraction.Ignore))
                            shift.SetServerPosition(actor.ParticipantId, actor.RegionId, Point(next));
                    }
                    var current = shift.Participant(actor.ParticipantId);
                    var forward = Quaternion.Euler(0, input.Yaw, 0) * Vector3.forward;
                    shift.DiscoverNearby(actor.ParticipantId, point => Vector3.Dot(forward,
                        (Vector(point) - Vector(current.Position)).normalized) >= .5f);
                }
                SendView(client);
            }
            checkpointClock += TickSeconds;
            if (connectedWorld != null && checkpointClock >= 2)
            { checkpointClock = 0; if (!spatialPersistenceFault) SaveSpatialCheckpoint(); }
        }
        private void TickSimulation()
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double discoveryMilliseconds = 0, projectionMilliseconds = 0, persistMilliseconds = 0;
            var movement = new List<ServerMovementInput>();
            foreach (var client in identities.Keys.ToArray())
            {
                if (!network.ConnectedClients.ContainsKey(client)) continue;
                var input = inputs[client];
                if (Time.realtimeSinceStartupAsDouble - lastInput[client] > .5) { input.Enabled = false; shift.SetInputEnabled(identities[client], false); }
                movement.Add(new ServerMovementInput { ParticipantId = identities[client], Enabled = input.Enabled,
                    LoadedRegions = input.LoadedRegions, Velocity = Point(Quaternion.Euler(0, input.Yaw, 0) * new Vector3(input.X, 0, input.Z) * 3) });
            }
            SimulationTickReport report = null;
            if (!spatialPersistenceFault && !simulation.TryAdvance(shift, movement.ToArray(), out report))
            {
                spatialPersistenceFault = true; shift.FaultPersistence();
                foreach (var id in identities.Keys) movementStatus[id] = "세계 계산을 정지했습니다 · 서버 복구 필요";
                Debug.LogError("World simulation transaction failed: " + report.Failure);
            }
            foreach (var client in identities.Keys.ToArray())
            {
                if (!network.ConnectedClients.ContainsKey(client)) continue;
                var actor = shift.Participant(identities[client]); var forward = Quaternion.Euler(0, inputs[client].Yaw, 0) * Vector3.forward;
                var phase = System.Diagnostics.Stopwatch.StartNew();
                shift.DiscoverNearby(actor.ParticipantId, point => Vector3.Dot(forward, (Vector(point) - Vector(actor.Position)).normalized) >= .5f);
                discoveryMilliseconds += phase.Elapsed.TotalMilliseconds; phase.Restart();
                SendView(client);
                projectionMilliseconds += phase.Elapsed.TotalMilliseconds;
            }
            checkpointClock += TickSeconds;
            if (checkpointClock >= 2 && !spatialPersistenceFault)
            { var phase = System.Diagnostics.Stopwatch.StartNew(); checkpointClock = 0; SaveSpatialCheckpoint(); persistMilliseconds = phase.Elapsed.TotalMilliseconds; }
            if (!string.IsNullOrEmpty(phaseProfilePath) && report != null)
            {
                var measured = new NativeTickPhaseRecord { Tick=simulation.Tick, Paused=shift.Paused, NpcCount=simulation.NpcCount,
                    Clients=network.ConnectedClients.Count, ActiveIncidents=simulation.ActiveIncidentCount,
                    TotalUntilProfileWriteMs=timer.Elapsed.TotalMilliseconds, CoordinatorMs=report.TotalMilliseconds,
                    ReadMs=report.ReadMilliseconds, BeforeMotionMs=report.BeforeMotionMilliseconds,
                    MotionMs=report.MotionMilliseconds, FireMs=report.FireMilliseconds, OpticsMs=report.OpticsMilliseconds,
                    CheckpointMs=report.CheckpointMilliseconds, MotionCaptureMs=report.MotionCaptureMilliseconds,
                    CrowdEncodeMs=report.CrowdEncodeMilliseconds, MotionEncodeMs=report.MotionEncodeMilliseconds,
                    PhysicalEncodeMs=report.EncodeTiming?.TotalMilliseconds??0, EncodeCrowdValidationMs=report.EncodeTiming?.CrowdValidationMilliseconds??0,
                    AuthorityMs=report.AuthorityMilliseconds, PhysicalDecodeMs=report.DecodeTiming?.TotalMilliseconds??0,
                    DecodeCrowdValidationMs=report.DecodeTiming?.CrowdValidationMilliseconds??0,
                    DiscoveryMs=discoveryMilliseconds, ProjectionSendMs=projectionMilliseconds, PersistMs=persistMilliseconds,
                    PhysicalCheckpointCharacters=report.PhysicalCheckpointCharacters };
                try { File.AppendAllText(phaseProfilePath, JsonUtility.ToJson(measured)+"\n"); }
                catch (IOException) { phaseProfilePath=null; Debug.LogError("Phase profile output failed."); }
            }
        }

        [Serializable] private sealed class NativeTickPhaseRecord
        {
            public int Schema=1,NpcCount,Clients,ActiveIncidents,PhysicalCheckpointCharacters;
            public long Tick; public bool Paused;
            public double TotalUntilProfileWriteMs,CoordinatorMs,ReadMs,BeforeMotionMs,MotionMs,FireMs,OpticsMs,
                CheckpointMs,MotionCaptureMs,CrowdEncodeMs,MotionEncodeMs,PhysicalEncodeMs,EncodeCrowdValidationMs,
                AuthorityMs,PhysicalDecodeMs,DecodeCrowdValidationMs,DiscoveryMs,ProjectionSendMs,PersistMs;
        }

        private void SendView(ulong client)
        {
            var participant = identities[client];
            var actor = shift.Participant(participant);
            var projected = new FieldView { Observed = shift.Observe(participant), Position = actor.Position,
                TeamId = actor.TeamId, RoleId = actor.RoleId, Instructor = actor.IsInstructor,
                ProtocolVersion = simulation != null ? 3 : connectedWorld == null ? 1 : 2, SpatialProfileId = connectedWorld?.Definition.ProfileId ?? "",
                RegionId = actor.RegionId, FrameId = actor.FrameId, PortalId = actor.PortalId, LocalPosition = actor.LocalPosition,
                RequiredRegions = connectedWorld?.Definition.RequiredRegions(actor.RegionId) ?? Array.Empty<string>(),
                Portals = simulation != null ? simulation.ProjectPortals(actor, p => ServerCanSeePortal(actor, p)) : connectedWorld == null ? Array.Empty<ObservedPortalState>() : ConnectedWorldRuntime.ProjectPortals(
                    connectedWorld.Definition, shift.ExportCheckpoint(), actor, p => ServerCanSeePortal(actor, p)),
                MovementStatus = movementStatus.TryGetValue(client, out var movement) ? movement : "",
                TrainingMode = simulation != null ? simulation.TrainingMode : FoundationTrainingMode.Practice };
            if (simulation != null)
            {
                projected.Physical = simulation.Project(shift.ReadSimulation(), actor, point => ServerCanSeePoint(actor, point),projected.Observed.Entities);
                var payload = SnapshotPayload.Encode(JsonUtility.ToJson(projected));
                foreach (var packet in SnapshotFragments.Split(++snapshotSerial, payload))
                    using (var writer = new FastBufferWriter(packet.Length, Allocator.Temp))
                    { writer.WriteBytesSafe(packet); sentBytes += packet.Length; network.CustomMessagingManager.SendNamedMessage(SnapshotChannel, client, writer, NetworkDelivery.Unreliable); }
            }
            else Send(ViewChannel, client, JsonUtility.ToJson(projected));
        }

        public void Submit(ProbeStep step) => TrySubmit(step);

        private bool TrySubmit(ProbeStep step)
        {
            if (!Connected || view?.Observed == null) return false;
            var pause=view.Instructor && step.Kind==CommandKind.PauseShift;
            var resume=view.Instructor && step.Kind==CommandKind.ResumeShift && snapshotFreshness.IsCurrent(Time.realtimeSinceStartupAsDouble);
            if (!LocalInputEnabled && !pause && !resume) return false;
            Send(CommandChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(new WorldCommand {
                WorldId = view.Observed.WorldId, ShiftId = view.Observed.ShiftId, ParticipantId = credential.ParticipantId,
                TeamId = view.TeamId, CommandId = string.IsNullOrEmpty(step.CommandId) ? Guid.NewGuid().ToString("N") : step.CommandId,
                Kind = step.Kind, TargetId = step.TargetId ?? "", Argument = step.Argument ?? "", ExpectedRevision = step.ExpectedRevision }));
            return true;
        }

        /// <summary>Sends a report-panel command and tracks it so only the matching server receipt is shown.</summary>
        public bool SubmitPanelCommand(WorldCommand command)
        {
            if (commandPanel == null || !Connected || view?.Observed == null || !LocalInputEnabled) return false;
            if (command == null) { status = "현재 서버 상태로는 할 수 없는 행동입니다."; return false; }
            Send(CommandChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(command));
            commandPanel.Track(command);
            return true;
        }

        private void InteractClosest()
        {
            if (view?.Observed == null || cameraView == null) return;
            var target = view.Observed.Entities.Where(x => x.Kind != EntityKind.Train && x.Position.DistanceSquared(view.Position) <= 6.25 &&
                Vector3.Dot(cameraView.transform.forward, (Vector(x.Position) + Vector3.up - cameraView.transform.position).normalized) > .7f)
                .OrderBy(x => x.Position.DistanceSquared(view.Position)).FirstOrDefault();
            if (target == null) { status = "가까운 설비나 대피자를 바라보세요."; return; }
            if (commandPanel != null && target.Kind == EntityKind.Evacuee) { SubmitPanelCommand(commandPanel.Claim(target.EntityId)); return; }
            if (commandPanel != null && target.Kind == EntityKind.Incident) { SubmitPanelCommand(commandPanel.Report(target.EntityId)); return; }
            Submit(new ProbeStep { TargetId = target.EntityId, ExpectedRevision = target.Revision,
                Kind = target.Kind == EntityKind.Evacuee ? CommandKind.ClaimEvacuee : target.Kind == EntityKind.Incident ? CommandKind.Report : CommandKind.Operate });
        }

        private bool ServerCanSee(ParticipantState actor, EntityState target)
        {
            var from = Vector(actor.Position) + Vector3.up * 1.6f;
            var to = Vector(target.Position) + Vector3.up * .7f;
            if (simulation != null && !simulation.OpticallyVisible(Point(from), Point(to))) return false;
            if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<NetworkEntityAnchor>()?.EntityId == target.EntityId;
        }

        private bool ServerCanSeePortal(ParticipantState actor, ConnectedPortalDefinition portal)
        {
            var from = Vector(actor.Position) + Vector3.up * 1.6f;
            var to = Vector(portal.ClosurePoint) + Vector3.up * (portal.ClosureBottom + portal.ClosureHeight / 2);
            if (simulation != null && !simulation.OpticallyVisible(Point(from), Point(to))) return false;
            if (!Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore)) return true;
            return hit.collider.GetComponentInParent<ConnectedPortalBarrier>()?.PortalId == portal.Id;
        }
        private bool ServerCanSeePoint(ParticipantState actor, Point3 point)
        {
            var eye = actor.Position; eye.Y += 1.6f;
            return (simulation == null || simulation.OpticallyVisible(eye, point)) && !Physics.Linecast(Vector(eye), Vector(point), ~0, QueryTriggerInteraction.Ignore);
        }

        private void Send(string channel, ulong client, string text)
        {
            var capacity = Encoding.UTF8.GetByteCount(text) * 2 + 16;
            if (capacity > 65536) throw new InvalidDataException("Projected message exceeds its protocol bound.");
            using (var writer = new FastBufferWriter(capacity, Allocator.Temp))
            {
                writer.WriteValueSafe(text);
                sentBytes += writer.Length;
                network.CustomMessagingManager.SendNamedMessage(channel, client, writer,
                    channel == InputChannel && writer.Length <= 900 ? NetworkDelivery.UnreliableSequenced : NetworkDelivery.ReliableFragmentedSequenced);
            }
        }

        private void SetControls(bool enabled)
        {
            controls = enabled;
            if (!enabled) voiceRadio?.StopTransmission();
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            if (Connected) Send(InputChannel, NetworkManager.ServerClientId, JsonUtility.ToJson(new FieldInput { Enabled = LocalInputEnabled, Yaw = yaw,
                LoadedRegions = connectedWorld?.LoadedRegionIds ?? Array.Empty<string>() }));
        }
        private void OnApplicationFocus(bool value) { focused = value; if (!value && !server && probe == null) SetControls(false); }
        private void OnDisable() { controls = false; }
        private void OnDestroy()
        {
            Application.wantsToQuit -= MayQuit;
            if (network != null && network.IsListening) network.Shutdown();
            if (connectedWorld != null && shift != null && (journal != null || simulationJournal != null) && !spatialPersistenceFault) SaveSpatialCheckpoint();
            journal?.Dispose(); simulationJournal?.Dispose(); simulation?.Dispose();
            FlushMetrics();
        }

        private bool MayQuit()
        {
            if (quitAllowed || voiceService == null) return true;
            stopping = true;
            if (!shutdownRunning) StartCoroutine(ShutdownVoice());
            return false;
        }

        private IEnumerator ShutdownVoice()
        {
            shutdownRunning = true;
            yield return voiceService.RetireRooms();
            if (!voiceService.RetiredSuccessfully)
            {
                status = "음성 채널 종료 실패 · 서버 종료를 다시 시도하세요";
                shutdownRunning = false;
                Debug.LogError("Server quit deferred: voice room retirement failed.");
                yield break;
            }
            quitAllowed = true;
            Evidence("voice_rooms_retired", new ParticipantEvent { ParticipantId = "server" });
            Application.Quit();
        }

        /// <summary>Authored-synthetic banner text; never a facility acceptance claim.</summary>
        public const string SyntheticBanner = "합성 배치 · 실측/시설 인증 아님";

        private static string SyntheticLabel(ConnectedWorldDefinition definition, string geometryStatus) =>
            SyntheticBanner + " (" + (definition.Classification ?? "") + ", " + geometryStatus + ")";

        /// <summary>Pure projection of the observed current location onto one displayed state.
        /// Thirteen profile region IDs map one-to-one onto thirteen distinct states.</summary>
        public static RouteDisplayState ProjectCurrentLocation(ConnectedWorldDefinition definition, FieldView view)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (view == null) throw new ArgumentNullException(nameof(view));
            var state = new RouteDisplayState { Classification = definition.Classification ?? "",
                Synthetic = true, RouteSyntheticLabel = SyntheticLabel(definition, "route-not-requested") };
            var region = definition.Regions == null ? null : definition.Regions.SingleOrDefault(r => r != null && r.Id == view.RegionId);
            if (region == null)
            {
                state.RegionId = view.RegionId ?? "";
                state.SyntheticLabel = SyntheticLabel(definition, "unavailable");
                state.UnavailableReason = "알 수 없는 현재 구역 · 사용 불가";
                return state;
            }
            state.RegionId = region.Id; state.RegionLabel = region.Label; state.FrameId = region.FrameId;
            state.TrainingMode = view != null ? view.TrainingMode : FoundationTrainingMode.Practice;
            state.ProcedureHintsVisible = state.TrainingMode == FoundationTrainingMode.Practice;
            state.SyntheticLabel = SyntheticLabel(definition, region.GeometryStatus);
            state.UnavailableReason = "목적지 미선택 · 사용 불가";
            return state;
        }

        /// <summary>Pure projection of the observed location plus the route to <paramref name="targetRegionId"/>.
        /// Observed closed portals from the client view are the only closure input; an unreachable target
        /// yields an explicit unavailable state instead of an empty route line. Never invents a bypass.</summary>
        public static RouteDisplayState ProjectRoute(ConnectedWorldDefinition definition, FieldView view, string targetRegionId)
        {
            var state = ProjectCurrentLocation(definition, view);
            state.TargetRegionId = targetRegionId ?? "";
            if (string.IsNullOrEmpty(state.RegionId) || !definition.Regions.Any(r => r != null && r.Id == state.RegionId)) return state;
            var target = definition.Regions.SingleOrDefault(r => r != null && r.Id == targetRegionId);
            if (target == null)
            {
                state.UnavailableReason = "알 수 없는 목적지 · 사용 불가";
                return state;
            }
            state.TargetRegionLabel = target.Label;
            var observed = view.Portals ?? Array.Empty<ObservedPortalState>();
            var knownClosed = new HashSet<string>(observed.Where(p => p != null && !p.Open).Select(p => p.PortalId));
            var route = definition.Route(state.RegionId, target.Id, knownClosed);
            if (route.Length == 0)
            {
                state.RouteSyntheticLabel = SyntheticLabel(definition, "route-unreachable");
                state.UnavailableReason = "경로 없음 · 사용 불가 (차단된 포털 " +
                    knownClosed.Count + "개)";
                return state;
            }
            state.Available = true;
            state.UnavailableReason = "";
            state.RouteRegionIds = route;
            state.RouteLabels = route.Select(id => definition.Region(id).Label).ToArray();
            var portals = new List<ConnectedPortalDefinition>();
            for (var index = 0; index + 1 < route.Length; index++)
            {
                var from = route[index]; var to = route[index + 1];
                var portal = definition.Portals.SingleOrDefault(p => p != null &&
                    ((p.From == from && p.To == to) || (p.From == to && p.To == from)));
                if (portal == null) continue;
                portals.Add(portal);
                if (portal.FromPoint.Y != portal.ToPoint.Y) state.CrossesLevel = true;
                if (definition.Region(portal.From).FrameId != definition.Region(portal.To).FrameId) state.CrossesFrame = true;
            }
            state.RoutePortalIds = portals.Select(p => p.Id).ToArray();
            state.RouteGeometryStatuses = portals.Select(p => p.GeometryStatus).ToArray();
            state.RouteSyntheticLabel = SyntheticLabel(definition,
                state.GeometryStatus + " / " + string.Join(", ", state.RouteGeometryStatuses.Distinct()));
            if (state.ProcedureHintsVisible)
            {
                state.ProcedureHint = "다음 이동 구역: " + (route.Length > 1 ? definition.Region(route[1]).Label : state.TargetRegionLabel) + " (연습 힌트)";
                state.GuidanceEvidenceBasis = "synthetic_guidance_evidence_provisional";
            }
            else
            {
                state.ProcedureHint = "";
                state.GuidanceEvidenceBasis = "";
            }
            return state;
        }

        /// <summary>Pure projection of training guidance based on training mode.
        /// In Practice mode, requested guidance exposes approved evidence fields (procedure hint, recommended action).
        /// In Evaluation mode, procedure hint fields are strictly excluded.</summary>
        public static TrainingGuidanceState ProjectGuidance(ConnectedWorldDefinition definition, FieldView view, string targetRegionId = "")
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (view == null) throw new ArgumentNullException(nameof(view));
            var mode = view.TrainingMode;
            var target = string.IsNullOrEmpty(targetRegionId) ? view.RegionId : targetRegionId;
            var routeDisplay = ProjectRoute(definition, view, target);
            var state = new TrainingGuidanceState
            {
                Mode = mode,
                WorldId = view.Observed?.WorldId ?? "",
                ParticipantId = view.Observed?.ParticipantId ?? "",
                RoleId = view.RoleId ?? "",
                CurrentRegionId = view.RegionId ?? "",
                TargetRegionId = target ?? "",
                Available = routeDisplay.Available || (!string.IsNullOrEmpty(view.RegionId) && view.RegionId == target),
                UnavailableReason = routeDisplay.UnavailableReason,
                ProcedureHintsVisible = mode == FoundationTrainingMode.Practice,
                SyntheticLabel = routeDisplay.SyntheticLabel
            };

            if (state.ProcedureHintsVisible)
            {
                var nextStep = routeDisplay.RouteLabels.Length > 1 ? routeDisplay.RouteLabels[1] : (definition.Region(view.RegionId)?.Label ?? view.RegionId);
                state.ProcedureHint = "현장 이동 및 대응 절차: " + nextStep + " 방면 이동 (연습 모드 절차 안내)";
                state.RecommendedAction = "MoveTo:" + (routeDisplay.RouteRegionIds.Length > 1 ? routeDisplay.RouteRegionIds[1] : view.RegionId);
                state.EvidenceBasis = "synthetic_sop_reference_provisional_not_facility_accepted";
            }
            else
            {
                state.ProcedureHint = "";
                state.RecommendedAction = "";
                state.EvidenceBasis = "";
            }
            return state;
        }

        private void OnGUI()
        {
            if (Application.isBatchMode) return;
            GUILayout.BeginArea(new Rect(16, 16, 500, navigationMenu ? 650 : commandPanel != null ? 620 : 300), GUI.skin.box);
            GUILayout.Label("CHOOguard 공동 근무 · 검증 전 훈련 예시");
            GUILayout.Label(status);
            if (view != null)
            {
                GUILayout.Label(view.TeamId + " · " + view.RoleId + (view.TrainingMode == FoundationTrainingMode.Practice ? " · [연습 모드]" : " · [평가 모드]") + (view.Observed.Paused ? " · 교관이 근무를 정지했습니다" : ""));
                GUILayout.Label("WASD 이동 · 마우스 시점 · E 상호작용 · Esc 개인 메뉴");
                if (connectedWorld != null)
                {
                    var routeDisplay = ProjectRoute(connectedWorld.Definition, view, navigationTarget);
                    GUILayout.Label("현재: " + routeDisplay.RegionLabel + " · " + routeDisplay.FrameId + " · N 구역 길찾기");
                    GUILayout.Label(routeDisplay.SyntheticLabel);
                    if (!string.IsNullOrEmpty(view.MovementStatus)) GUILayout.Label(view.MovementStatus);
                    if (!string.IsNullOrEmpty(navigationTarget))
                    {
                        GUILayout.Label(routeDisplay.Available
                            ? "공개 연결: " + string.Join(" → ", routeDisplay.RouteLabels)
                            : "공개 연결: " + routeDisplay.UnavailableReason);
                        GUILayout.Label(routeDisplay.RouteSyntheticLabel);
                        if (routeDisplay.ProcedureHintsVisible && !string.IsNullOrEmpty(routeDisplay.ProcedureHint))
                        {
                            GUILayout.Label("절차 힌트: " + routeDisplay.ProcedureHint);
                        }
                    }
                    if (navigationMenu)
                    {
                        navigationScroll = GUILayout.BeginScrollView(navigationScroll, GUILayout.Height(370));
                        foreach (var region in connectedWorld.Definition.Regions)
                            if (GUILayout.Button(region.Label)) { navigationTarget = region.Id; navigationMenu = false; SetControls(true); }
                        GUILayout.EndScrollView();
                    }
                }
                if (voiceRadio != null) GUILayout.Label(voiceRadio.Status);
                if (commandPanel != null && !navigationMenu) DrawCommandPanel();
                else foreach (var report in view.Observed.Reports.TakeLast(3)) GUILayout.Label("팀 보고: " + report.EntityId + " / " + report.RegionId);
                if (!controls && GUILayout.Button("현장으로 복귀")) SetControls(true);
                if (view.Instructor && GUILayout.Button(view.Observed.Paused ? "근무 재개" : "근무 정지"))
                    Submit(new ProbeStep { Kind = view.Observed.Paused ? CommandKind.ResumeShift : CommandKind.PauseShift });
            }
            GUILayout.EndArea();
        }

        private void DrawCommandPanel()
        {
            var lines = commandPanel.RenderLines();
            GUILayout.Label(lines[0] + " · E 보고/인솔 · R 수신확인 · H 인계");
            foreach (var line in lines.Skip(1).Where(l => l.StartsWith("팀 보고", StringComparison.Ordinal)).TakeLast(4)) GUILayout.Label(line);
            foreach (var line in lines.Skip(1).Where(l => l.StartsWith("대피자", StringComparison.Ordinal)).Take(4)) GUILayout.Label(line);
            if (commandPanel.Evacuees.Any(e => e.LedByLocal))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(controls ? "인계 받을 참가자 ID (Esc 후 입력)" : "인계 받을 참가자 ID", GUILayout.Width(190));
                if (controls) GUILayout.Label(handOffTarget ?? "");
                else handOffTarget = GUILayout.TextField(handOffTarget ?? "", 64);
                GUILayout.EndHorizontal();
            }
            foreach (var line in lines.Skip(1).Where(l => l.StartsWith("서버 ", StringComparison.Ordinal)).TakeLast(4)) GUILayout.Label(line);
        }

        [Serializable] private sealed class ParticipantEvent { public string ParticipantId; }
        [Serializable] private sealed class WaypointEvidence { public int Index; public string RegionId, FrameId, TargetId;
            public Point3 Position; public int Operations; public string[] LoadedRegions; }
        private Vector2 ProbeMovement()
        {
            if (probe?.Waypoints == null || view == null || nextWaypoint >= probe.Waypoints.Length) return Vector2.zero;
            var waypoint = probe.Waypoints[nextWaypoint];
            var delta = new Vector2(waypoint.Position.X - view.Position.X, waypoint.Position.Z - view.Position.Z);
            if (delta.magnitude > .18f) return Vector2.ClampMagnitude(delta * 3, 1);
            if (waypoint.RegionId != view.RegionId) return Vector2.zero;
            if (waypointArrivedAt < 0) waypointArrivedAt = Time.realtimeSinceStartupAsDouble;
            if (Time.realtimeSinceStartupAsDouble - waypointArrivedAt < waypoint.WaitSeconds) return Vector2.zero;
            if (waypointOperations < waypoint.Operations)
            {
                var target = view.Observed.Entities.SingleOrDefault(e => e.EntityId == waypoint.TargetId);
                if (target != null && pendingProbeCommand == null && Time.realtimeSinceStartupAsDouble - probeCommandAt > .5)
                {
                    pendingProbeCommand = "walk-" + nextWaypoint + "-op-" + waypointOperations;
                    probeCommandAt = Time.realtimeSinceStartupAsDouble;
                    Submit(new ProbeStep { CommandId = pendingProbeCommand, Kind = CommandKind.Operate,
                        TargetId = waypoint.TargetId, ExpectedRevision = target.Revision });
                }
                return Vector2.zero;
            }
            Evidence("waypoint", new WaypointEvidence { Index = nextWaypoint, RegionId = view.RegionId, FrameId = view.FrameId,
                Position = view.Position, TargetId = waypoint.TargetId, Operations = waypointOperations,
                LoadedRegions = connectedWorld?.LoadedRegionIds ?? Array.Empty<string>() });
            nextWaypoint++; waypointOperations = 0; waypointArrivedAt = -1;
            if (nextWaypoint == probe.Waypoints.Length)
            { Evidence("route_complete", new WaypointEvidence { Index = nextWaypoint }); if (probe.ExitWhenRouteComplete) Application.Quit(); }
            return Vector2.zero;
        }
        private static SpatialPose ActorPose(ParticipantState p) => new SpatialPose { RegionId = p.RegionId, FrameId = p.FrameId,
            PortalId = p.PortalId, Position = p.Position, LocalPosition = p.LocalPosition };
        private bool SaveSpatialCheckpoint()
        {
            try { if (simulationJournal != null) simulationJournal.RecordBoundary(shift.ReadSimulation()); else journal.Checkpoint(shift.ExportCheckpoint()); return true; }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                spatialPersistenceFault = true; shift.FaultPersistence();
                foreach (var id in identities.Keys) movementStatus[id] = "기록 저장 오류 · 서버 복구가 필요합니다";
                Debug.LogError("Spatial checkpoint unavailable; the shift is frozen until server recovery.");
                return false;
            }
        }
        private void RecordSimulationTickMetrics(double elapsedMilliseconds)
        {
            if (metrics == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            metrics.RecordSimulationTick(now,elapsedMilliseconds,sentBytes,receivedBytes,server ? network.ConnectedClients.Count : Connected ? 1 : 0,
                server ? simulation?.NpcCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Evacuee) ?? 0,
                server ? simulation?.ActiveIncidentCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Incident) ?? 0,
                server ? simulation?.Tick ?? 0 : view?.Observed.SimulationTick ?? 0, server ? shift.Paused : view?.Observed.Paused ?? false,
                server ? Math.Max(0,accumulator) : 0);
            if (now-metricsClosedAt >= 5) FlushMetrics();
        }
        private void RecordFrameMetrics(double elapsedMilliseconds)
        {
            if (metrics == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            metrics.RecordFrame(now,elapsedMilliseconds,sentBytes,receivedBytes,server ? network.ConnectedClients.Count : Connected ? 1 : 0,
                server ? simulation?.NpcCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Evacuee) ?? 0,
                server ? simulation?.ActiveIncidentCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Incident) ?? 0,
                server ? simulation?.Tick ?? 0 : view?.Observed.SimulationTick ?? 0, server ? shift.Paused : view?.Observed.Paused ?? false,
                server ? Math.Max(0,accumulator) : 0);
            if (now-metricsClosedAt >= 5) FlushMetrics();
        }
        private void RecordMetrics(double elapsedMilliseconds)
        {
            if (metrics == null) return;
            var now = Time.realtimeSinceStartupAsDouble;
            metrics.Record(now,elapsedMilliseconds,sentBytes,receivedBytes,server ? network.ConnectedClients.Count : Connected ? 1 : 0,
                server ? simulation?.NpcCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Evacuee) ?? 0,
                server ? simulation?.ActiveIncidentCount ?? 0 : view?.Observed.Entities.Count(e=>e.Kind==EntityKind.Incident) ?? 0,
                server ? simulation?.Tick ?? 0 : view?.Observed.SimulationTick ?? 0, server ? shift.Paused : view?.Observed.Paused ?? false,
                server ? Math.Max(0,accumulator) : 0);
            if (now-metricsClosedAt >= 5) FlushMetrics();
        }
        private void StartClientMetrics()
        {
            if (server || metrics!=null || string.IsNullOrEmpty(metricsPath) || view?.Observed==null) return;
            metricsClosedAt=Time.realtimeSinceStartupAsDouble;
            metrics=new RuntimeMetricAccumulator("client",metricsClosedAt,DateTime.UtcNow,view.Observed.SimulationTick,sentBytes,receivedBytes,
                Application.isBatchMode,SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null);
        }
        private void FlushMetrics()
        {
            if (metrics == null || Time.realtimeSinceStartupAsDouble <= metricsClosedAt) return;
            try
            {
                var now=Time.realtimeSinceStartupAsDouble;
                File.AppendAllText(metricsPath,JsonUtility.ToJson(metrics.Close(now,DateTime.UtcNow))+"\n"); metricsClosedAt=now;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { metrics=null; Debug.LogError("Runtime measurement output failed: "+e.GetType().Name); }
        }
        [Serializable] private sealed class VoiceEvidence { public bool Connected, Transmitting; public string Status; }
        [Serializable] private sealed class EvidenceLine { public string Kind, Json; public double Time; public long SentBytes, ReceivedBytes; }
        private void Evidence(string kind, object value)
        {
            if (string.IsNullOrEmpty(evidencePath)) return;
            var line = new EvidenceLine { Kind = kind, Json = JsonUtility.ToJson(value), Time = Time.realtimeSinceStartupAsDouble,
                SentBytes = sentBytes, ReceivedBytes = receivedBytes };
            File.AppendAllText(evidencePath, JsonUtility.ToJson(line) + "\n");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Vector3 Vector(Point3 p) => new Vector3(p.X, p.Y, p.Z);
        private static Point3 Point(Vector3 p) => new Point3(p.x, p.y, p.z);
        private static string RosterKey(ParticipantState p) => p.ParticipantId + "/" + p.TeamId + "/" + p.RoleId + "/" + p.IsInstructor;
        private static string Required(string key) => !string.IsNullOrWhiteSpace(Argument(key)) ? Argument(key) : throw new ArgumentException("Missing launch option " + key);
        private static string Argument(string key, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
