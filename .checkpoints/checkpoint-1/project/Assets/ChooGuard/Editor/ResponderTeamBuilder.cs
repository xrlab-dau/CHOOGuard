using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Team prefabs for every unit that can answer a call (<see cref="Teams"/>), from Microsoft Rocketbox profession
    /// avatars (MIT) and one Objaverse hazmat suit (CC BY), marked the Korean way:
    /// <list type="bullet">
    /// <item>print overlays (등 인쇄: 119 구급대·구조대·소방, 기술자 소속) and vests (경찰 형광 조끼, 철도경찰 남색 조끼) are extra skinned meshes
    /// cut from the avatar's own torso, pushed out a few millimetres and mapped round the body (back centre u = 0.5,
    /// front opening u = 0/1, waist v = 0, shoulders v = 1) with the Korean lettering drawn by
    /// asset-library/research-public/2026-09-29/responder-assets/make_markings.py;</item>
    /// <item>the 119 rescue unit's orange suit is the paramedic coverall with its blue shifted to orange;</item>
    /// <item>the bomb technician's blast suit is the whole body (not head or hands) blown out 3.5–6 cm in olive fabric,
    /// with a full-face helmet on the head bone and 경찰특공대 EOD on the back.</item>
    /// </list>
    /// Everything is built in bind pose on the instance, so it follows the shared humanoid animations.
    /// </summary>
    public static class ResponderTeamBuilder
    {
        private const string MarkingRoot = EmergencySceneBuilder.ArtRoot + "/Crowd/Markings";
        private const string OverlayRoot = CrowdAssetBuilder.CrowdRoot + "/Overlays";
        private const string RecolorRoot = CrowdAssetBuilder.CrowdRoot + "/Recolors";
        private const string HazmatModel = EmergencySceneBuilder.ObjaverseRoot + "/HazmatSuit/HazmatSuit.fbx";
        private const string HelmetModel = EmergencySceneBuilder.ObjaverseRoot + "/EodHelmet/EodHelmet.obj";

        [MenuItem("ChooGuard/Emergency/Build responder teams")]
        public static void BuildMenu()
        {
            var male = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowdAssetBuilder.MaleControllerPath) ?? throw new FileNotFoundException(CrowdAssetBuilder.MaleControllerPath);
            var female = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CrowdAssetBuilder.FemaleControllerPath) ?? throw new FileNotFoundException(CrowdAssetBuilder.FemaleControllerPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CrowdCatalog>(CrowdAssetBuilder.CatalogPath) ?? throw new FileNotFoundException(CrowdAssetBuilder.CatalogPath);
            Build(catalog, male, female);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void Build(CrowdCatalog catalog, RuntimeAnimatorController male, RuntimeAnimatorController female)
        {
            CrowdAssetBuilder.EnsureFolder(OverlayRoot);
            CrowdAssetBuilder.EnsureFolder(RecolorRoot);
            GameObject Person(string avatar, string name, Action<GameObject, Animator, string> dress, bool orange = false)
            {
                bool isFemale = avatar.Contains("Female");
                return CrowdAssetBuilder.BuildPrefab(avatar, isFemale, isFemale ? female : male, "Team_" + name,
                    dress == null ? null : (root, animator) => dress(root, animator, name),
                    material: orange ? m => Orange(avatar, m) : null);
            }
            catalog.Firefighters = new[] { Person("Fire_Male_05", "Fire_Lead", Print("Back_Fire")), Person("Fire_Male_03", "Fire_Nozzle", Print("Back_Fire")) };
            catalog.Rescuers = new[] { Person("Medical_Male_02", "Rescue_Lead", Print("Back_Rescue"), true), Person("Medical_Female_03", "Rescue_Member", Print("Back_Rescue"), true) };
            catalog.HazmatTeam = new[] { Hazmat("Hazmat_Lead", male), Hazmat("Hazmat_Member", male) };
            catalog.Paramedics = new[] { Person("Medical_Male_02", "Ems_Lead", Print("Back_Ems")), Person("Medical_Female_03", "Ems_Member", Print("Back_Ems")) };
            catalog.Police = new[] { Person("Police_Male_06", "RailwayPolice_Lead", Vest("Vest_RailwayPolice")), Person("Police_Male_07", "RailwayPolice_Member", Vest("Vest_RailwayPolice")) };
            catalog.PatrolPolice = new[] { Person("Police_Male_03", "Patrol_Officer", Vest("Vest_Police")) };
            catalog.BombSquad = new[] { Person("Military_Male_02", "Eod_Technician", BombSuit), Person("Police_Male_02", "Swat_Officer", Print("Back_Eod")) };
            catalog.FacilityStaff = new[] { Person("Construction_Male_02", "Facility_Staff", Print("Back_Facility")) };
            catalog.ElevatorTechnicians = new[] { Person("Construction_Male_05", "Elevator_Technician", Print("Back_Elevator")) };
            catalog.GasTechnicians = new[] { Person("Construction_Male_05", "Gas_Technician", Print("Back_Gas")) };
            catalog.Electricians = new[] { Person("Construction_Male_02", "Electrician", Print("Back_Electric")) };
            catalog.TrainCrew = new[] { Person("Pilot_Male_01", "Train_Crew", null) };
            Debug.Log("CG_TEAMS_BUILT fire=" + catalog.Firefighters.Length + " rescue=" + catalog.Rescuers.Length + " hazmat=" + catalog.HazmatTeam.Length +
                      " ems=" + catalog.Paramedics.Length + " police=" + catalog.Police.Length + "+" + catalog.PatrolPolice.Length + " eod=" + catalog.BombSquad.Length);
        }

        // ── 인쇄와 조끼 ─────────────────────────────────────────────────────

        private static Action<GameObject, Animator, string> Print(string marking) =>
            (root, animator, name) => Overlay(root, animator, name + "_" + marking, marking, .006f, false, .10f, .96f);

        private static Action<GameObject, Animator, string> Vest(string marking) =>
            (root, animator, name) => Overlay(root, animator, name + "_" + marking, marking, .012f, true, .02f, .90f);

        /// <summary>
        /// A band cut from the torso of the body mesh (triangles whose vertices all ride the hips, spine, chest or
        /// shoulders and lie between <paramref name="bottom"/> and <paramref name="top"/> of the hips-to-neck height),
        /// pushed <paramref name="offset"/> m out along the normals and mapped round the body. Opaque for a vest, alpha-clipped
        /// print otherwise. Triangles across the front opening are left out.
        /// </summary>
        private static SkinnedMeshRenderer Overlay(GameObject root, Animator animator, string assetName, string marking, float offset, bool opaque, float bottom, float top,
            IReadOnlyList<Vector3> positions = null)
        {
            var body = MainBody(root);
            var mesh = body.sharedMesh;
            var frame = BodyFrame(body, animator);
            var vertices = positions ?? mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            var torso = BoneIndices(body, animator, HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
                HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder);
            float low = frame.Hips + (frame.Neck - frame.Hips) * bottom, high = frame.Hips + (frame.Neck - frame.Hips) * top;
            var map = new Dictionary<int, int>();
            var outVertices = new List<Vector3>();
            var outUv = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            foreach (int sub in BodySubmeshes(body))
            {
                var tris = mesh.GetTriangles(sub);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    bool keep = true;
                    float minU = 1, maxU = 0;
                    for (int k = 0; k < 3 && keep; k++)
                    {
                        int v = tris[t + k];
                        float h = Vector3.Dot(vertices[v], frame.Up);
                        keep = torso.Contains(weights[v].boneIndex0) && h >= low && h <= high;
                        float u = frame.U(vertices[v]);
                        minU = Mathf.Min(minU, u);
                        maxU = Mathf.Max(maxU, u);
                    }
                    if (!keep || maxU - minU > .5f) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t + k];
                        if (!map.TryGetValue(v, out int index))
                        {
                            index = outVertices.Count;
                            map[v] = index;
                            outVertices.Add(vertices[v] + normals[v] * offset);
                            outUv.Add(new Vector2(frame.U(vertices[v]), (Vector3.Dot(vertices[v], frame.Up) - low) / (high - low)));
                            outWeights.Add(weights[v]);
                        }
                        triangles.Add(index);
                    }
                }
            }
            var overlay = SaveMesh(assetName, mesh, outVertices, outUv, outWeights, triangles);
            var material = MarkingMaterial(marking, opaque);
            return AttachSkin(body, overlay, material, opaque ? "조끼" : "인쇄");
        }

        // ── 폭발물 방호복 ────────────────────────────────────────────────────

        /// <summary>Blast suit: the body blown out (torso 6 cm, legs 4.5 cm, arms 3.5 cm) in olive fabric, helmet on the head bone, print on the back.</summary>
        private static void BombSuit(GameObject root, Animator animator, string name)
        {
            var body = MainBody(root);
            var mesh = body.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            var weights = mesh.boneWeights;
            var skip = BoneIndices(body, animator, HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.LeftHand, HumanBodyBones.RightHand);
            foreach (var finger in Enum.GetValues(typeof(HumanBodyBones)).Cast<HumanBodyBones>().Where(b => b.ToString().Contains("Proximal") || b.ToString().Contains("Intermediate") || b.ToString().Contains("Distal")))
                skip.UnionWith(BoneIndices(body, animator, finger));
            var torso = BoneIndices(body, animator, HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
                HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder);
            var legs = BoneIndices(body, animator, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg,
                HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes);
            var map = new Dictionary<int, int>();
            var outVertices = new List<Vector3>();
            var outUv = new List<Vector2>();
            var outWeights = new List<BoneWeight>();
            var triangles = new List<int>();
            var uv = mesh.uv;
            var inflated = (Vector3[])vertices.Clone();
            for (int v = 0; v < vertices.Length; v++)
            {
                int bone = weights[v].boneIndex0;
                float push = torso.Contains(bone) ? .06f : legs.Contains(bone) ? .045f : .035f;
                inflated[v] = vertices[v] + normals[v] * push;
            }
            foreach (int sub in BodySubmeshes(body))
            {
                var tris = mesh.GetTriangles(sub);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    if (skip.Contains(weights[tris[t]].boneIndex0) || skip.Contains(weights[tris[t + 1]].boneIndex0) || skip.Contains(weights[tris[t + 2]].boneIndex0)) continue;
                    for (int k = 0; k < 3; k++)
                    {
                        int v = tris[t + k];
                        if (!map.TryGetValue(v, out int index))
                        {
                            index = outVertices.Count;
                            map[v] = index;
                            outVertices.Add(inflated[v]);
                            outUv.Add(uv[v] * 4f);
                            outWeights.Add(weights[v]);
                        }
                        triangles.Add(index);
                    }
                }
            }
            var suit = SaveMesh(name + "_BlastSuit", mesh, outVertices, outUv, outWeights, triangles);
            var fabric = SuitMaterial();
            AttachSkin(body, suit, fabric, "폭발물 방호복");
            // 등 인쇄는 부푼 옷 위에(같은 정점 위치를 부푼 쪽으로 넘긴다).
            Overlay(root, animator, name + "_Back_Eod", "Back_Eod", .008f, false, .10f, .96f, inflated);
            // 헬멧: 머리뼈에 붙인다(바인드 자세에서 몸 앞(+Z)을 보게, 머리를 감싸도록 조금 아래로).
            var helmet = AssetDatabase.LoadAssetAtPath<GameObject>(HelmetModel) ?? throw new FileNotFoundException(HelmetModel);
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            var worn = (GameObject)PrefabUtility.InstantiatePrefab(helmet);
            worn.name = "방호 헬멧";
            worn.transform.SetPositionAndRotation(head.position + Vector3.down * .13f + Vector3.forward * .01f, Quaternion.Euler(0, HelmetYaw, 0));
            worn.transform.localScale = Vector3.one * 1.12f;
            worn.transform.SetParent(head, true);
        }

        /// <summary>Turn of the converted helmet so its visor faces the body's front (checked in capture).</summary>
        private const float HelmetYaw = 0f;

        private static Material SuitMaterial()
        {
            const string path = CrowdAssetBuilder.MaterialRoot + "/Team_BlastSuit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var set = EmergencySceneBuilder.PbrRoot + "/Fabric031_1K/";
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(set + "Fabric031_1K-JPG_Color.jpg"));
            material.SetColor("_BaseColor", new Color(.42f, .45f, .30f));
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(set + "Fabric031_1K-JPG_NormalGL.jpg");
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP");
            material.SetFloat("_Smoothness", .18f);
            material.SetFloat("_Metallic", 0);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ── 구조대 주황 ──────────────────────────────────────────────────────

        /// <summary>The paramedic coverall's blue shifted to 119 rescue orange (other slots unchanged).</summary>
        private static Material Orange(string avatar, Material source)
        {
            var basic = CrowdAssetBuilder.Material(avatar, source);
            if (source == null || !source.name.EndsWith("_body", StringComparison.Ordinal)) return basic;
            var colour = basic.GetTexture("_BaseMap") as Texture2D;
            string outPath = RecolorRoot + "/" + avatar + "_rescue_body_color.png";
            if (colour != null && !File.Exists(outPath))
            {
                var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(colour)));
                var pixels = image.GetPixels();
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color.RGBToHSV(pixels[i], out float h, out float s, out float v);
                    if (h < .50f || h > .76f || s < .2f) continue;
                    pixels[i] = Color.HSVToRGB(.065f, Mathf.Min(1, s * 1.05f), Mathf.Min(1, v * 1.35f + .08f));
                }
                image.SetPixels(pixels);
                File.WriteAllBytes(outPath, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(outPath);
            }
            string path = CrowdAssetBuilder.MaterialRoot + "/" + avatar + "_Rescue_" + source.name + ".mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (variant == null)
            {
                variant = new Material(basic);
                AssetDatabase.CreateAsset(variant, path);
            }
            variant.CopyPropertiesFromMaterial(basic);
            variant.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(outPath));
            EditorUtility.SetDirty(variant);
            return variant;
        }

        // ── 화학보호복(외부 모델) ────────────────────────────────────────────

        [Serializable] private sealed class HumanoidInfo { public float unityScale = 1; }

        private static GameObject Hazmat(string name, RuntimeAnimatorController controller)
        {
            var importer = AssetImporter.GetAtPath(HazmatModel) as ModelImporter ?? throw new FileNotFoundException(HazmatModel);
            // 키 1.80 m 로 맞추는 배율(blender_convert.py 가 변형된 메시로 잰 값).
            importer.globalScale = JsonUtility.FromJson<HumanoidInfo>(File.ReadAllText(Path.ChangeExtension(HazmatModel, ".json"))).unityScale;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            // .meta 에 남은 옛 T 자세(humanDescription.skeleton)는 모델을 다시 내보내면 어긋난다(뼈대 부모 배율 0.01 과
            // cm 단위 뼈 위치가 m 단위 옛 자세로 덮여 몸이 접힌다): 지금 모델의 자세로 다시 쓴다.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(HazmatModel);
            var description = importer.humanDescription;
            description.skeleton = model.GetComponentsInChildren<Transform>(true)
                .Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray();
            importer.humanDescription = description;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(HazmatModel).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("휴머노이드로 읽히지 않습니다: " + HazmatModel);
            var material = EmergencySceneBuilder.JsonMaterial("HazmatSuit", "m0");
            return CrowdAssetBuilder.BuildPrefab("HazmatSuit", false, controller, "Team_" + name, modelPath: HazmatModel, material: _ => material);
        }

        // ── 공통 ─────────────────────────────────────────────────────────────

        private static SkinnedMeshRenderer MainBody(GameObject root) =>
            root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.sharedMesh != null ? r.sharedMesh.vertexCount : 0).First();

        /// <summary>Submeshes painted with the avatar's body (clothes), not head, hair or see-through parts.</summary>
        private static IEnumerable<int> BodySubmeshes(SkinnedMeshRenderer body)
        {
            var materials = body.sharedMaterials;
            for (int i = 0; i < body.sharedMesh.subMeshCount; i++)
                if (i < materials.Length && materials[i] != null && materials[i].name.Contains("_body")) yield return i;
        }

        private static HashSet<int> BoneIndices(SkinnedMeshRenderer body, Animator animator, params HumanBodyBones[] wanted)
        {
            var set = new HashSet<int>();
            foreach (var b in wanted)
            {
                var t = animator.GetBoneTransform(b);
                int i = t != null ? Array.IndexOf(body.bones, t) : -1;
                if (i >= 0) set.Add(i);
            }
            return set;
        }

        /// <summary>The body's own frame in mesh space (bind pose): up, front, the hips and neck heights, and u round the torso.</summary>
        private sealed class Frame
        {
            public Vector3 Up, Front, Right, Axis;
            public float Hips, Neck;
            public float U(Vector3 p)
            {
                var h = p - Axis;
                h -= Up * Vector3.Dot(h, Up);
                float a = Mathf.Atan2(-Vector3.Dot(h, Right), Vector3.Dot(h, Front));
                return Mathf.Repeat(a / (2 * Mathf.PI), 1f);
            }
        }

        private static Frame BodyFrame(SkinnedMeshRenderer body, Animator animator)
        {
            var mesh = body.sharedMesh;
            Vector3 BindPosition(HumanBodyBones b)
            {
                var t = animator.GetBoneTransform(b);
                int i = Array.IndexOf(body.bones, t);
                return i >= 0 ? (Vector3)mesh.bindposes[i].inverse.GetColumn(3) : body.transform.InverseTransformPoint(t.position);
            }
            var up = body.transform.InverseTransformDirection(Vector3.up).normalized;
            var front = body.transform.InverseTransformDirection(Vector3.forward).normalized;
            var hips = BindPosition(HumanBodyBones.Hips);
            var neck = BindPosition(HumanBodyBones.Neck);
            return new Frame { Up = up, Front = front, Right = Vector3.Cross(up, front), Axis = hips, Hips = Vector3.Dot(hips, up), Neck = Vector3.Dot(neck, up) };
        }

        private static Mesh SaveMesh(string name, Mesh source, List<Vector3> vertices, List<Vector2> uv, List<BoneWeight> weights, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = source.bindposes;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = OverlayRoot + "/" + name + ".asset";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static SkinnedMeshRenderer AttachSkin(SkinnedMeshRenderer body, Mesh mesh, Material material, string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            var skin = go.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.bones = body.bones;
            skin.rootBone = body.rootBone;
            skin.sharedMaterials = new[] { material };
            skin.localBounds = body.localBounds;
            skin.updateWhenOffscreen = false;
            skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return skin;
        }

        private static Material MarkingMaterial(string marking, bool opaque)
        {
            string texturePath = MarkingRoot + "/" + marking + ".png";
            var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter ?? throw new FileNotFoundException(texturePath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = !opaque;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
            string path = CrowdAssetBuilder.MaterialRoot + "/Marking_" + marking + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", opaque ? .28f : .35f);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_AlphaClip", opaque ? 0 : 1);
            material.SetFloat("_Cutoff", .5f);
            if (opaque) material.DisableKeyword("_ALPHATEST_ON"); else material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = opaque ? -1 : (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
