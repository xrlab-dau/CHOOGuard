using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChooGuard.App.Fps.Emergency;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace ChooGuard.Editor
{
    /// <summary>
    /// Builds URP materials, humanoid animator controllers and person prefabs from the imported Rocketbox avatars,
    /// then writes the crowd catalog the emergency session spawns from. The fall and the kneeling treatment come from the
    /// Quaternius Universal Animation Library (CC0), retargeted through the humanoid rig.
    /// </summary>
    public static class CrowdAssetBuilder
    {
        public const string CrowdRoot = EmergencySceneBuilder.ArtRoot + "/Crowd";
        public const string MaterialRoot = CrowdRoot + "/Materials";
        public const string PrefabRoot = CrowdRoot + "/Prefabs";
        public const string CatalogPath = CrowdRoot + "/CrowdCatalog.asset";
        public const string MaleControllerPath = CrowdRoot + "/Person_Male.controller";
        public const string FemaleControllerPath = CrowdRoot + "/Person_Female.overrideController";
        public const string LyingClipPath = CrowdRoot + "/Person_Lying.anim";
        public const string SlumpedClipPath = CrowdRoot + "/Person_Slumped.anim";
        public const string UniversalAnimationPath = "Assets/ChooGuard/ThirdParty/Animations/Quaternius/UAL1_Standard.fbx";

        private static readonly string[] FemalePassengers = { "Female_Adult_01", "Female_Adult_04", "Female_Adult_05", "Female_Adult_08", "Female_Adult_12", "Female_Adult_14", "Business_Female_01" };
        private static readonly string[] MalePassengers = { "Male_Adult_02", "Male_Adult_07", "Male_Adult_08", "Male_Adult_09", "Male_Adult_14", "Male_Adult_16" };
        private const string Colleague = "Business_Male_02";
        private static readonly string[] Firefighters = { "Fire_Male_05", "Fire_Male_03" };
        private static readonly string[] Police = { "Police_Male_07", "Police_Male_06" };
        private static readonly string[] Paramedics = { "Medical_Male_02" };

        [MenuItem("ChooGuard/Emergency/Build crowd prefabs")]
        public static void Build()
        {
            EnsureFolder(MaterialRoot);
            EnsureFolder(PrefabRoot);
            ImportUniversalAnimations();
            var male = BuildMaleController();
            var female = BuildFemaleOverride(male);
            var catalog = AssetDatabase.LoadAssetAtPath<CrowdCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<CrowdCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.FemalePassengers = FemalePassengers.Select(n => BuildPrefab(n, true, female)).ToArray();
            catalog.MalePassengers = MalePassengers.Select(n => BuildPrefab(n, false, male)).ToArray();
            catalog.Colleague = BuildPrefab(Colleague, false, male);
            catalog.Firefighters = Firefighters.Select(n => BuildPrefab(n, false, male)).ToArray();
            catalog.Police = Police.Select(n => BuildPrefab(n, false, male)).ToArray();
            catalog.Paramedics = Paramedics.Select(n => BuildPrefab(n, false, male)).ToArray();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log("CG_CROWD_BUILT prefabs=" + (catalog.FemalePassengers.Length + catalog.MalePassengers.Length + 1 + Firefighters.Length + Police.Length + Paramedics.Length));
        }

        private static AnimationClip Clip(string name)
        {
            var path = RocketboxImportRules.AnimationRoot + name + ".max.fbx";
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview", StringComparison.Ordinal));
            return clip != null ? clip : throw new FileNotFoundException("애니메이션 없음: " + path);
        }

        /// <summary>
        /// The Universal Animation Library (Standard, no root motion) as a humanoid, so its clips play on Rocketbox avatars.
        /// Height and turn stay in the pose but travel is dropped, so a person collapses where they stood and the body stays
        /// over the agent position paramedics walk to. The kneeling take (stand, kneel, work, stand up) is split into its
        /// three parts; the working loop runs between frames 47 and 101, the pair of frames whose poses differ least
        /// (summed muscle difference 4.4 over 95 muscles, measured 2026-09-27).
        /// </summary>
        private static void ImportUniversalAnimations()
        {
            var importer = AssetImporter.GetAtPath(UniversalAnimationPath) as ModelImporter ?? throw new FileNotFoundException(UniversalAnimationPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in importer.defaultClipAnimations)
            {
                // 테이크 이름은 "Armature|Death01" 꼴이다. 뒷부분만 클립 이름으로 쓴다.
                string name = take.takeName.Substring(take.takeName.LastIndexOf('|') + 1);
                if (name != "Fixing_Kneeling") { clips.Add(UniversalClip(take, name, take.firstFrame, take.lastFrame)); continue; }
                clips.Add(UniversalClip(take, "Kneel_Down", 0, 47));
                clips.Add(UniversalClip(take, "Kneel_Treat_Loop", 47, 101));
                clips.Add(UniversalClip(take, "Kneel_Up", 118, take.lastFrame));
            }
            importer.clipAnimations = clips.ToArray();
            importer.SaveAndReimport();
        }

        private static ModelImporterClipAnimation UniversalClip(ModelImporterClipAnimation take, string name, float first, float last)
        {
            bool loop = name.EndsWith("_Loop", StringComparison.Ordinal);
            return new ModelImporterClipAnimation
            {
                takeName = take.takeName,
                name = name,
                firstFrame = first,
                lastFrame = last,
                loopTime = loop,
                loopPose = loop,
                lockRootRotation = true,
                keepOriginalOrientation = true,
                lockRootHeightY = true,
                keepOriginalPositionY = true,
                lockRootPositionXZ = false,
            };
        }

        private static AnimationClip Universal(string name)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(UniversalAnimationPath).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);
            return clip != null ? clip : throw new FileNotFoundException("애니메이션 없음: " + UniversalAnimationPath + " · " + name);
        }

        private static AnimatorController BuildMaleController()
        {
            if (File.Exists(MaleControllerPath)) AssetDatabase.DeleteAsset(MaleControllerPath);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(MaleControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = "Pace", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
            controller.AddParameter("Idle", AnimatorControllerParameterType.Int);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Phone", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Cough", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Film", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Listen", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Down", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Slump", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Collapse", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Treat", AnimatorControllerParameterType.Bool);
            // 부축하는 손(SupportHands)이 기본 층의 IK 단계에서 어깨를 잡는다. 다른 사람은 IK 목표 무게가 0 이라 자세가 바뀌지 않는다.
            var layers = controller.layers;
            layers[0].iKPass = true;
            controller.layers = layers;
            var machine = controller.layers[0].stateMachine;

            // 서 있기(이동 = 허브)와 걷기·뛰기를 다른 상태로 둔다. 11.7초짜리 서기 동작을 1.2초 걷기와 한 나무에 섞으면 주기가
            // 평균되어 걸음이 절반 속도로 느려지고 발이 미끄러진다. Speed 는 걸음새(1 걷기, 2 뛰기), Pace 는 걷기 상태의 재생
            // 배속이다. PersonBody 가 사람마다 두 동작이 실제로 나아가는 속도에 맞춰 둘을 정한다(발이 땅에 붙게). 동작 자체에는
            // 전진이 없다(RocketboxImportRules).
            var locomotion = State(machine, PersonBody.Locomotion, "m_idle_neutral_01");
            machine.defaultState = locomotion;
            var tree = new BlendTree { name = "걷기·뛰기", blendParameter = "Speed", blendType = BlendTreeType.Simple1D, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Clip("m_walk_neutral_01"), 1f);
            tree.AddChild(Clip("m_run_neutral_01"), 2f);
            var walking = State(machine, PersonBody.Walking, tree);
            walking.speedParameter = "Pace";
            walking.speedParameterActive = true;
            Link(locomotion, walking, .2f, ("Speed", AnimatorConditionMode.Greater, .1f));
            Link(walking, locomotion, .25f, ("Speed", AnimatorConditionMode.Less, .05f));

            var look = State(machine, "둘러보기", "m_idle_look_around_01");
            var nervous = State(machine, "불안", "m_idle_nervous_01");
            var phone = State(machine, "통화", "m_cell_phone_talk_01");
            var cough = State(machine, "기침", "m_idle_cough_01");
            var crouchIn = State(machine, "숙이기", "m_crouch_in");
            var crouchIdle = State(machine, "숙인 채", "m_crouch_idle");
            var crouchOut = State(machine, "일어서기", "m_crouch_out");
            var listen = State(machine, "수긍", "m_gestic_listen_accept_01");
            var wave = State(machine, "손짓", "m_wave_01");
            var film = State(machine, "촬영", "m_take_picture");
            // 의자 동작은 PersonBody 가 원점을 옮기며 직접 재생한다. 전이를 두지 않는다.
            State(machine, PersonBody.SitDown, "m_sit_down_chair_01");
            var sitIdle = State(machine, PersonBody.SitIdle, "m_sit_chair_idle_neutral_01");
            State(machine, PersonBody.StandUp, "m_sit_stand_up_chair_01");

            // 서서 하는 동작은 서 있을 때 시작하고, 걷기 시작하면 바로 걷기로 간다.
            Link(locomotion, look, .3f, ("Idle", AnimatorConditionMode.Equals, 1), ("Speed", AnimatorConditionMode.Less, .1f));
            Link(look, locomotion, .3f, ("Idle", AnimatorConditionMode.NotEqual, 1));
            Link(look, walking, .2f, ("Speed", AnimatorConditionMode.Greater, .1f));
            Link(locomotion, nervous, .3f, ("Idle", AnimatorConditionMode.Equals, 2), ("Speed", AnimatorConditionMode.Less, .1f));
            Link(nervous, locomotion, .3f, ("Idle", AnimatorConditionMode.NotEqual, 2));
            Link(nervous, walking, .2f, ("Speed", AnimatorConditionMode.Greater, .1f));
            Link(locomotion, phone, .35f, ("Phone", AnimatorConditionMode.If, 0), ("Speed", AnimatorConditionMode.Less, .1f));
            Link(phone, locomotion, .3f, ("Phone", AnimatorConditionMode.IfNot, 0));
            Link(phone, walking, .2f, ("Speed", AnimatorConditionMode.Greater, .1f));
            Link(film, locomotion, .3f, ("Film", AnimatorConditionMode.IfNot, 0));
            Link(locomotion, film, .3f, ("Film", AnimatorConditionMode.If, 0), ("Speed", AnimatorConditionMode.Less, .1f));
            Link(film, walking, .2f, ("Speed", AnimatorConditionMode.Greater, .1f));
            // 기침·숙이기·수긍·손짓은 걷던 중에도 받는다(전과 같이).
            foreach (var from in new[] { locomotion, walking })
            {
                Link(from, cough, .25f, ("Cough", AnimatorConditionMode.If, 0));
                Link(from, crouchIn, .2f, ("Crouch", AnimatorConditionMode.If, 0));
                Link(from, listen, .2f, ("Listen", AnimatorConditionMode.If, 0));
                Link(from, wave, .2f, ("Wave", AnimatorConditionMode.If, 0));
            }
            Link(cough, locomotion, .25f, ("Cough", AnimatorConditionMode.IfNot, 0));
            ExitLink(crouchIn, crouchIdle, .15f);
            Link(crouchIdle, crouchOut, .2f, ("Crouch", AnimatorConditionMode.IfNot, 0));
            ExitLink(crouchOut, locomotion, .2f);
            ExitLink(listen, locomotion, .25f);
            ExitLink(wave, locomotion, .25f);
            // 쓰러짐: 어떤 동작 중에도 그 자리에서 무너져(Death01) 넘어진 모습 그대로 바닥에 눕는다(의자에 앉은 사람에게는
            // 쓰지 않는다). 무너지는 동작은 한 번만 — 누운 뒤에도 Down 이 켜져 있으니 시작은 방아쇠(Collapse)로 한다.
            var falling = machine.AddState("쓰러지는 중");
            falling.motion = Universal("Death01");
            var fall = machine.AddAnyStateTransition(falling);
            fall.hasExitTime = false;
            fall.duration = .15f;
            fall.canTransitionToSelf = false;
            fall.AddCondition(AnimatorConditionMode.If, 0, "Collapse");
            var down = machine.AddState("쓰러짐");
            down.motion = BuildLyingClip((AnimationClip)falling.motion);
            var lie = falling.AddTransition(down);
            lie.hasExitTime = true;
            lie.exitTime = 1f;
            lie.duration = 0;
            Link(falling, locomotion, .5f, ("Down", AnimatorConditionMode.IfNot, 0));
            Link(down, locomotion, .8f, ("Down", AnimatorConditionMode.IfNot, 0));
            // 구급 처치: 쓰러진 사람 곁에 무릎 꿇고, 두 손으로 처치를 이어 가다, 끝나면 일어선다.
            var kneel = State(machine, "무릎 꿇기", Universal("Kneel_Down"));
            var treat = State(machine, "처치", Universal("Kneel_Treat_Loop"));
            var rise = State(machine, "무릎 펴기", Universal("Kneel_Up"));
            Link(locomotion, kneel, .3f, ("Treat", AnimatorConditionMode.If, 0), ("Speed", AnimatorConditionMode.Less, .1f));
            ExitLink(kneel, treat, .15f);
            Link(kneel, rise, .3f, ("Treat", AnimatorConditionMode.IfNot, 0));
            Link(treat, rise, .3f, ("Treat", AnimatorConditionMode.IfNot, 0));
            ExitLink(rise, locomotion, .25f);
            // 앉은 채 쓰러짐: 의자·객실 좌석에서 의식을 잃은 사람은 앞으로 늘어진다.
            var slump = machine.AddState("앉은 채 늘어짐");
            slump.motion = BuildSlumpedClip();
            var sag = machine.AddAnyStateTransition(slump);
            sag.hasExitTime = false;
            sag.duration = .8f;
            sag.canTransitionToSelf = false;
            sag.AddCondition(AnimatorConditionMode.If, 0, "Slump");
            Link(slump, sitIdle, .6f, ("Slump", AnimatorConditionMode.IfNot, 0));
            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// Lying where they fell: the last frame of the fall held still, so the fall ends in it without a blend. Rocketbox has
        /// no such clip. Travel is not part of the fall, so the body lies over the agent position.
        /// </summary>
        private static AnimationClip BuildLyingClip(AnimationClip fall)
        {
            var clip = new AnimationClip { name = "Person_Lying" };
            foreach (var binding in AnimationUtility.GetCurveBindings(fall))
            {
                // 근육·몸 위치(RootT/Q)·손발 IK 목표만 마지막 프레임 값으로 옮긴다. 이동량(Motion)은 두지 않는다.
                if (binding.type != typeof(Animator) || binding.propertyName.StartsWith("Motion", StringComparison.Ordinal)) continue;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1, AnimationUtility.GetEditorCurve(fall, binding).Evaluate(fall.length)));
            }
            // 몸 방향·높이를 넘어짐과 같은 기준(원본 기준, 자세에 굽기)으로 읽어야 넘어진 그 방향 그대로 눕는다.
            var from = AnimationUtility.GetAnimationClipSettings(fall);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            settings.loopBlendOrientation = from.loopBlendOrientation;
            settings.loopBlendPositionY = from.loopBlendPositionY;
            settings.loopBlendPositionXZ = from.loopBlendPositionXZ;
            settings.keepOriginalOrientation = from.keepOriginalOrientation;
            settings.keepOriginalPositionY = from.keepOriginalPositionY;
            settings.keepOriginalPositionXZ = from.keepOriginalPositionXZ;
            settings.heightFromFeet = from.heightFromFeet;
            settings.orientationOffsetY = from.orientationOffsetY;
            settings.level = from.level;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (File.Exists(LyingClipPath)) AssetDatabase.DeleteAsset(LyingClipPath);
            AssetDatabase.CreateAsset(clip, LyingClipPath);
            return clip;
        }

        /// <summary>
        /// Seated and unconscious: the first frame of the Rocketbox sit-idle clip (same seat origin as the sitting states) with
        /// the spine, neck and head bent forward. Negative Front-Back/Nod values flex forward (checked in side renders).
        /// </summary>
        private static AnimationClip BuildSlumpedClip()
        {
            var sitting = Clip("m_sit_chair_idle_neutral_01");
            var clip = new AnimationClip { name = "Person_Slumped" };
            foreach (var binding in AnimationUtility.GetCurveBindings(sitting))
            {
                // 근육·몸 위치(RootT/Q)·손발 IK 목표만 첫 프레임 값으로 옮긴다. 이동량(Motion)은 두지 않는다.
                if (binding.type != typeof(Animator) || binding.propertyName.StartsWith("Motion", StringComparison.Ordinal)) continue;
                AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Constant(0, 1, AnimationUtility.GetEditorCurve(sitting, binding).Evaluate(0)));
            }
            void Set(string property, float value) =>
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), property), AnimationCurve.Constant(0, 1, value));
            Set("Spine Front-Back", -.8f);
            Set("Chest Front-Back", -.8f);
            Set("UpperChest Front-Back", -.5f);
            Set("Neck Nod Down-Up", -1f);
            Set("Head Nod Down-Up", -.7f);
            Set("Head Tilt Left-Right", .4f);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            if (File.Exists(SlumpedClipPath)) AssetDatabase.DeleteAsset(SlumpedClipPath);
            AssetDatabase.CreateAsset(clip, SlumpedClipPath);
            return clip;
        }

        private static AnimatorOverrideController BuildFemaleOverride(AnimatorController male)
        {
            if (File.Exists(FemaleControllerPath)) AssetDatabase.DeleteAsset(FemaleControllerPath);
            var over = new AnimatorOverrideController(male) { name = "Person_Female" };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            over.GetOverrides(pairs);
            var map = new Dictionary<string, string>
            {
                ["m_idle_neutral_01"] = "f_idle_neutral_02",
                ["m_walk_neutral_01"] = "f_walk_neutral_01",
                ["m_run_neutral_01"] = "f_run_neutral_01",
                ["m_idle_look_around_01"] = "f_idle_look_around_01",
                ["m_take_picture"] = "f_take_picture",
                ["m_sit_down_chair_01"] = "f_sit_down_chair_01",
                ["m_sit_chair_idle_neutral_01"] = "f_sit_chair_idle_neutral_01",
                ["m_sit_stand_up_chair_01"] = "f_sit_stand_up_chair_01",
            };
            for (int i = 0; i < pairs.Count; i++)
                if (map.TryGetValue(pairs[i].Key.name, out var female)) pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(pairs[i].Key, Clip(female));
            over.ApplyOverrides(pairs);
            AssetDatabase.CreateAsset(over, FemaleControllerPath);
            return over;
        }

        private static AnimatorState State(AnimatorStateMachine machine, string name, string clip) => State(machine, name, Clip(clip));

        private static AnimatorState State(AnimatorStateMachine machine, string name, Motion motion)
        {
            var state = machine.AddState(name);
            state.motion = motion;
            return state;
        }

        private static void Link(AnimatorState from, AnimatorState to, float duration, params (string parameter, AnimatorConditionMode mode, float threshold)[] conditions)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = duration;
            foreach (var c in conditions) transition.AddCondition(c.mode, c.threshold, c.parameter);
        }

        private static void ExitLink(AnimatorState from, AnimatorState to, float duration)
        {
            var transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = .92f;
            transition.duration = duration;
        }

        private static GameObject BuildPrefab(string avatar, bool female, RuntimeAnimatorController controller)
        {
            var modelPath = RocketboxImportRules.AvatarRoot + avatar + "/" + avatar + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath) ?? throw new FileNotFoundException(modelPath);
            var root = new GameObject(avatar);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "몸";
                instance.transform.SetParent(root.transform, false);
                var animator = instance.GetComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Material(avatar, m)).ToArray();
                    renderer.updateWhenOffscreen = false;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                var agent = root.AddComponent<NavMeshAgent>();
                agent.radius = .28f;
                agent.height = 1.75f;
                agent.speed = 1.3f;
                agent.acceleration = 6f;
                agent.angularSpeed = 300;
                agent.stoppingDistance = .3f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0, .88f, 0);
                capsule.height = 1.76f;
                capsule.radius = .24f;
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.isKinematic = true;
                rigidbody.useGravity = false;
                var body = root.AddComponent<PersonBody>();
                body.Animator = animator;
                body.Female = female;
                var path = PrefabRoot + "/" + avatar + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Material Material(string avatar, Material source)
        {
            if (source == null) return null;
            var slot = source.name;
            var folder = RocketboxImportRules.AvatarRoot + avatar + "/";
            Texture2D Find(string suffix)
            {
                foreach (var extension in new[] { ".jpg", ".png" })
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + slot + suffix + extension);
                    if (texture != null) return texture;
                }
                // 몇몇 아바타는 파란 작업복처럼 색 변형 접미사를 쓴다(m154_body_color_blue).
                var guid = AssetDatabase.FindAssets(slot + suffix + " t:Texture2D", new[] { folder.TrimEnd('/') }).FirstOrDefault();
                return guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            }
            var path = MaterialRoot + "/" + avatar + "_" + slot + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var color = Find("_color");
            var normal = Find("_normal");
            material.SetTexture("_BaseMap", color);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", slot.EndsWith("_head", StringComparison.Ordinal) ? .32f : .22f);
            bool cutout = slot.EndsWith("_opacity", StringComparison.Ordinal);
            material.SetFloat("_AlphaClip", cutout ? 1 : 0);
            material.SetFloat("_Cutoff", .42f);
            material.SetFloat("_Cull", cutout ? 0 : 2);
            if (cutout) material.EnableKeyword("_ALPHATEST_ON"); else material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = cutout ? (int)UnityEngine.Rendering.RenderQueue.AlphaTest : -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
