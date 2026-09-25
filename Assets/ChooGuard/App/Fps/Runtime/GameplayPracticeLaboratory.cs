using System;
using System.Collections.Generic;
using ChooGuard.Application.Gameplay;
using ChooGuard.Contracts;
using ChooGuard.Contracts.Gameplay;
using TMPro;
using UnityEngine;

namespace ChooGuard.App.Fps.Runtime
{
    /// <summary>Purpose-built synthetic interaction geometry. Dimensions describe this game prop, never a railway component.</summary>
    public sealed class GameplayPracticeLaboratory : MonoBehaviour
    {
        public IReadOnlyList<FpsEntityBinding> Bindings => bindings;
        private readonly List<FpsEntityBinding> bindings = new List<FpsEntityBinding>();
        private readonly List<MeshFilter> walkGeometry = new List<MeshFilter>();
        private readonly List<Material> materials = new List<Material>();
        private Material floorMaterial, propMaterial, toolMaterial, warningMaterial;
        private TMP_FontAsset font;
        private WorldSession session;
        private readonly Dictionary<StableId, TMP_Text> stateLabels = new Dictionary<StableId, TMP_Text>();

        public void Attach(WorldSession world)
        {
            if (session != null) session.Committed -= OnCommitted;
            session = world ?? throw new ArgumentNullException(nameof(world));
            session.Committed += OnCommitted;
            foreach (var entity in session.Snapshot().Entities.Values) RefreshState(entity);
        }
        private void OnCommitted(WorldMutation mutation)
        {
            foreach (var entity in mutation.Entities) RefreshState(entity);
        }
        private void RefreshState(WorldEntity entity)
        {
            if (!stateLabels.TryGetValue(entity.Id, out var label)) return;
            label.text = entity.Kind == EntityKind.Surface
                ? "잔류 " + Truth(entity.Fact("dirty")) + " · 습윤 " + Truth(entity.Fact("wet")) + "\n소독/건조 승인 아님"
                : "연기 " + Truth(entity.Fact("smoke")) + " · 유입수 " + Truth(entity.Fact("water.present")) + "\n접근 표지 " + Truth(entity.Fact("cordoned"));
            label.color = entity.Fact("hazard") == RuleTruth.TRUE || entity.Fact("wet") == RuleTruth.TRUE ? new Color(1, .75f, .2f) : Color.white;
        }
        private static string Truth(RuleTruth value) => value == RuleTruth.TRUE ? "있음" : value == RuleTruth.FALSE ? "없음" : "미확인";

        public GameplayNavigation Build(WorldSnapshot snapshot, TMP_FontAsset koreanFont, WorldSnapshot authoredLayout = null)
        {
            if (bindings.Count != 0) throw new InvalidOperationException("Laboratory already built.");
            font = koreanFont;
            floorMaterial = Material(new Color(.16f, .22f, .26f));
            propMaterial = Material(new Color(.36f, .56f, .61f));
            toolMaterial = Material(new Color(.96f, .68f, .24f));
            warningMaterial = Material(new Color(.75f, .27f, .18f));
            var layout = authoredLayout ?? snapshot;
            float minX = -23, maxX = 23, minZ = -7, maxZ = 41;
            foreach (var actor in layout.Actors.Values)
            {
                var position = layout.Entities[actor.Id].Position;
                minX = Mathf.Min(minX, position.X - 2); maxX = Mathf.Max(maxX, position.X + 2);
                minZ = Mathf.Min(minZ, position.Z - 2); maxZ = Mathf.Max(maxZ, position.Z + 2);
            }
            // Only this explicitly synthetic floor expands to its authored population; real geometry is never inferred.
            float centreX = (minX + maxX) * .5f, centreZ = (minZ + maxZ) * .5f;
            float width = maxX - minX, depth = maxZ - minZ;
            Solid("합성 실습 바닥", new Vector3(centreX, -.2f, centreZ), new Vector3(width, .4f, depth), floorMaterial, true);
            Solid("실습실 서쪽 벽", new Vector3(minX, 1.5f, centreZ), new Vector3(.3f, 3, depth), propMaterial, true);
            Solid("실습실 동쪽 벽", new Vector3(maxX, 1.5f, centreZ), new Vector3(.3f, 3, depth), propMaterial, true);
            Solid("실습실 북쪽 벽", new Vector3(centreX, 1.5f, maxZ), new Vector3(width, 3, .3f), propMaterial, true);
            Solid("실습실 남쪽 벽", new Vector3(centreX, 1.5f, minZ), new Vector3(width, 3, .3f), propMaterial, true);
            Solid("실제 경로 우회 장애물", new Vector3(-20, 1, 17), new Vector3(2, 2, 7), propMaterial, true);
            Label(transform, "독립 합성 조작 실습실\n실제 역사 형상·철도 작업 인증 아님", new Vector3(7, 2.7f, 5.8f), 1.1f);
            foreach (var entity in layout.Entities.Values)
                if (entity.Kind != EntityKind.Actor) BuildEntity(entity);
            foreach (var binding in bindings)
                if (snapshot.Entities.TryGetValue(new StableId(binding.EntityId), out var current) && binding.Body != null)
                    binding.Body.position = Vector(current.Position);
            var lightObject = new GameObject("실습실 조명", typeof(Light)); lightObject.transform.SetParent(transform, false);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(45, -35, 0);
            Physics.SyncTransforms();
            var nav = gameObject.AddComponent<GameplayNavigation>();
            byte[] baked = GameplayNavigationBaker.BakeGeometry(null, walkGeometry, out var digest);
            nav.BeginGeometry(digest);
            if (!nav.RegisterFloor(new FloorNavigationBinding { FloorId = "synthetic-lab-floor", GeometryRevision = digest }, baked, out var reason))
                throw new InvalidOperationException("실습실 실제 이동면 생성 실패: " + reason);
            return nav;
        }

        private void BuildEntity(WorldEntity entity)
        {
            var root = new GameObject(entity.Label); root.transform.SetParent(transform, false); root.transform.position = Vector(entity.Position);
            var binding = root.AddComponent<FpsEntityBinding>(); binding.Label = entity.Label; binding.HandReach = 1.4f;
            binding.WorkPointId = "main"; binding.GripTolerance = .06f; binding.SupportProbeDistance = .12f;
            // These are positions on this authored y=0 laboratory floor; generic model adapters must author their own.
            var stance = new Vector3(entity.Position.X, 0, entity.Position.Z + (entity.Position.Z >= 4 ? .75f : -.75f));
            if (entity.Id.Value == "practice-surface") stance = new Vector3(4.25f, 0, 3.6f);
            if (entity.Id.Value == "practice-old-part" || entity.Id.Value == "practice-socket") stance = new Vector3(-.7f, 0, 2.3f);
            binding.ApproachPoint = Frame(transform, "실제 바닥 작업 자세 " + entity.Id.Value, stance);
            bool marker = entity.Kind == EntityKind.Zone || entity.Kind == EntityKind.Exit;
            bool socket = entity.Kind == EntityKind.Socket;
            bool portable = entity.Fact("portable") == RuleTruth.TRUE;
            Vector3 size = marker ? new Vector3(.65f, .06f, .65f) : socket ? new Vector3(.55f, .06f, .55f) :
                entity.Kind == EntityKind.Surface ? new Vector3(.8f, .12f, .5f) :
                entity.Kind == EntityKind.Tool ? new Vector3(.12f, .12f, .28f) : new Vector3(.4f, .25f, .35f);
            Vector3 offset = marker ? Vector3.up * .03f : socket ? Vector3.down * .16f :
                entity.Position.Y < .2f ? Vector3.up * size.y * .5f : Vector3.zero;
            var bodyMesh = Primitive(root.transform, "접촉 실물", offset, size, entity.Kind == EntityKind.SuspiciousItem || entity.Kind == EntityKind.PersonalItem ? warningMaterial : portable ? toolMaterial : propMaterial);
            var surface = bodyMesh.GetComponent<Collider>();
            Rigidbody body = null;
            bool hinge = entity.DefinitionId == "practice-hinge" || socket && entity.Fact("isolation.procedure.known") == RuleTruth.TRUE;
            if (portable || hinge)
            {
                body = root.AddComponent<Rigidbody>(); body.mass = 1; body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.useGravity = !hinge;
            }
            binding.Configure(entity.Id.Value, surface, body);
            if (entity.Kind == EntityKind.Surface || entity.Kind == EntityKind.Zone)
                stateLabels.Add(entity.Id, Label(root.transform, "", Vector3.up * .9f, .5f));
            binding.Verb = portable && entity.Fact("installed") == RuleTruth.FALSE ? ActionVerb.PickUp : ActionVerb.Observe;
            if (!portable && !marker) walkGeometry.Add(bodyMesh.GetComponent<MeshFilter>());
            if (entity.Position.Y >= .8f && !socket && !entity.ParentId.HasValue)
            {
                float top = entity.Position.Y + offset.y - size.y * .5f;
                Solid("실습 받침 " + entity.Id.Value, new Vector3(entity.Position.X, top * .5f, entity.Position.Z),
                    new Vector3(Mathf.Max(.48f, size.x + .04f), top, Mathf.Max(.48f, size.z + .04f)), floorMaterial, true);
            }
            if (socket)
            {
                Solid("소켓 받침 " + entity.Id.Value, new Vector3(entity.Position.X, .4f, entity.Position.Z), new Vector3(.6f, .8f, .6f), floorMaterial, true);
                binding.Socket = Frame(root.transform, "삽입 기준", Vector3.zero);
                binding.SocketPositionTolerance = .08f; binding.SocketAngleTolerance = 15;
                foreach (var fact in entity.Facts)
                    if (fact.Key.StartsWith("accepts:", StringComparison.Ordinal) && fact.Value == RuleTruth.TRUE) binding.CompatiblePartDefinition = fact.Key.Substring(8);
                binding.Verb = ActionVerb.Install;
            }
            if (hinge)
            {
                binding.ConfigureHinge(new Vector3(-.2f, 0, 0), Vector3.up, 0, 90);
                binding.Hinge.autoConfigureConnectedAnchor = false;
                binding.Hinge.connectedAnchor = root.transform.TransformPoint(binding.Hinge.anchor);
                binding.Verb = socket ? ActionVerb.Isolate : ActionVerb.Open;
            }
            if (entity.Kind == EntityKind.Tool)
            {
                binding.ToolHead = Frame(root.transform, "실물 공구 헤드", new Vector3(0, 0, .14f));
                binding.ToolHeadRadius = .055f;
            }
            if (entity.Kind == EntityKind.Part && entity.Fact("waste.permitted") != RuleTruth.TRUE)
            {
                binding.ExtractionFrame = Frame(transform, "고정 추출 기준 " + entity.Id.Value, Vector(entity.Position));
                binding.ExtractionFrame.rotation = Quaternion.Euler(0, 180, 0); binding.ExtractionMetres = .2f;
                var points = new List<FpsEntityBinding.WorkPoint>();
                int index = 0;
                foreach (var fact in entity.Facts)
                    if (fact.Key.StartsWith("required.fastener:", StringComparison.Ordinal) && fact.Value == RuleTruth.TRUE)
                    {
                        string id = fact.Key.Substring(18);
                        points.Add(Point(binding, id, new Vector3(-.1f + index++ * .2f, 0, -.19f), new Vector3(.1f, .1f, .035f), "practice-driver", 0, 180, 0));
                    }
                foreach (var sample in entity.Measurements)
                    if (sample.Key.StartsWith("sample:", StringComparison.Ordinal))
                        points.Add(Point(binding, sample.Key.Substring(7), new Vector3(0, .06f, -.19f), new Vector3(.08f, .04f, .035f), "practice-meter", 0, 0, .5f));
                points.Add(Point(binding, "contact", new Vector3(.12f, .06f, -.19f), new Vector3(.06f, .04f, .035f), null, 0, 0, 0));
                binding.WorkPoints = points.ToArray();
                if (points.Count > 0) { binding.WorkPointId = points[0].Id; binding.ToolId = "practice-driver"; }
            }
            if (entity.Kind == EntityKind.Surface)
            {
                var points = new List<FpsEntityBinding.WorkPoint>(); int index = 0;
                foreach (var value in entity.Measurements)
                    if (value.Key.StartsWith("soil:", StringComparison.Ordinal))
                        points.Add(Point(binding, value.Key.Substring(5), new Vector3(-.2f + index++ * .4f, .075f, 0), new Vector3(.36f, .02f, .45f), "practice-cloth", .35f, 0, 0));
                binding.WorkPoints = points.ToArray(); binding.Verb = ActionVerb.Clean; binding.ToolId = "practice-cleaning-tool";
                if (points.Count > 0) binding.WorkPointId = points[0].Id;
            }
            if (entity.Id.Value == "practice-facility")
            {
                var points = new List<FpsEntityBinding.WorkPoint>();
                var names = new[] { "serial", "spec", "gauge", "body", "fit", "unfit" };
                for (int i = 0; i < names.Length; i++)
                    points.Add(Point(binding, names[i], new Vector3(-.12f + i % 3 * .12f, -.04f + i / 3 * .08f, -.19f),
                        new Vector3(.08f, .06f, .025f), null, 0, 0, 0));
                binding.WorkPoints = points.ToArray(); binding.WorkPointId = "serial";
            }
            if (entity.Kind == EntityKind.Container)
            {
                binding.Socket = Frame(root.transform, "투입 기준", Vector3.up * (size.y * .5f + .13f));
                binding.SocketPositionTolerance = .18f; binding.SocketAngleTolerance = 35;
                foreach (var fact in entity.Facts)
                    if (fact.Key.StartsWith("accepts:", StringComparison.Ordinal) && fact.Value == RuleTruth.TRUE) binding.CompatiblePartDefinition = fact.Key.Substring(8);
            }
            if (entity.Fact("waste.permitted") == RuleTruth.TRUE) binding.RecipientId = "practice-bin";
            if (entity.Id.Value == "practice-service-supply") binding.RecipientId = "practice-passenger";
            if (entity.Id.Value == "practice-tag") binding.RecipientId = "practice-colleague";
            binding.InitializeGeometry(); bindings.Add(binding);
        }

        private FpsEntityBinding.WorkPoint Point(FpsEntityBinding binding, string id, Vector3 position, Vector3 size, string tool, float stroke, float rotation, float probe)
        {
            var point = Primitive(binding.transform, "작업점 " + id, position, size, warningMaterial);
            var frame = Frame(binding.transform, "방향 " + id, position);
            // Forward-facing contact is an authored convenience of the unloaded fixture, not a real SOP.
            return new FpsEntityBinding.WorkPoint { Id = id, Surface = point.GetComponent<Collider>(), Frame = frame, Visual = point.transform,
                CompatibleToolDefinition = tool, StrokeMetres = stroke, RotationDegrees = rotation, RotationDirection = 1,
                ContactRadius = .06f, AlignmentDegrees = 35, ProbeSeconds = probe, Axis = Vector3.forward };
        }
        private GameObject Solid(string name, Vector3 position, Vector3 size, Material material, bool navigation)
        {
            var go = Primitive(transform, name, position, size, material);
            if (navigation) walkGeometry.Add(go.GetComponent<MeshFilter>());
            return go;
        }
        private static GameObject Primitive(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition; go.transform.localScale = size; go.GetComponent<MeshRenderer>().sharedMaterial = material; return go;
        }
        private static Transform Frame(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform;
        }
        private TMP_Text Label(Transform parent, string value, Vector3 position, float size)
        {
            var go = new GameObject("실습 표기", typeof(TextMeshPro)); go.transform.SetParent(parent, false); go.transform.localPosition = position;
            var text = go.GetComponent<TextMeshPro>(); text.font = font; text.text = value; text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(3, 1); text.richText = false;
            text.enableWordWrapping = true; text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        private Material Material(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("실습 형상 렌더링 셰이더가 없습니다.");
            var material = new Material(shader) { color = color }; materials.Add(material); return material;
        }
        private void OnDestroy()
        {
            if (session != null) session.Committed -= OnCommitted;
            foreach (var material in materials) if (material != null) Destroy(material);
        }
        private static Vector3 Vector(WorldPoint point) => new Vector3(point.X, point.Y, point.Z);
    }
}
