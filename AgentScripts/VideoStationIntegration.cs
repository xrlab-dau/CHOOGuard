using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ChooGuard.Editor;

public static class VideoStationIntegration
{
    const string Generated = "Assets/ChooGuard/Art/StationInterior/VideoIntegration";
    const string Records = ".planning/2026-09-23-video-twin";
    const string RootName = "영상복원 · 조명 및 원본 접속";
    const float Angle = 16.2f;
    static Vector3 W(float u, float v, float y) => Quaternion.Euler(0, Angle, 0) * new Vector3(v, y, u);

    sealed class SourceMesh
    {
        public string name;
        public string asset;
        public long fileId;
    }

    public static void Main(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Integrate in Edit mode.");
        string phase = args.Length > 0 ? args[0] : "all";
        if (GameObject.Find("영상복원 · 1층") == null || GameObject.Find("영상복원 · 2층") == null)
            throw new InvalidOperationException("Both complete floor builders must be present before replacing the old hall.");
        Directory.CreateDirectory(Generated);
        Directory.CreateDirectory(Records);
        AssetDatabase.Refresh();
        if (phase == "all" || phase == "openings") Openings();
        var oldHall = GameObject.Find("부산역 2층 맞이방 · 신축");
        if (oldHall != null) UnityEngine.Object.DestroyImmediate(oldHall);
        if (phase == "all" || phase == "lighting") Lighting();
        Physics.SyncTransforms();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("VIDEO_STATION_INTEGRATION " + phase + ": original FBX retained; inferred registration, not surveyed.");
    }

    static void Openings()
    {
        // Original shell plus the platform-detail layer whose pieces pierce the reconstructed floors.
        var filters = new List<MeshFilter>();
        foreach (string layer in new[] { "FPSWorld/공식 자료 부산역 역사/MainShell", "FPSWorld/공식 자료 부산역 역사/Layer0PlatformsDetails" })
        {
            var source = GameObject.Find(layer);
            if (source == null) throw new InvalidOperationException("Reviewed original layer is missing: " + layer);
            filters.AddRange(source.GetComponentsInChildren<MeshFilter>(true));
        }
        string manifestPath = Records + "/source-mesh-restore.json";
        var originals = File.Exists(manifestPath)
            ? JsonConvert.DeserializeObject<List<SourceMesh>>(File.ReadAllText(manifestPath)) : new List<SourceMesh>();
        bool added = false;
        foreach (var filter in filters)
        {
            if (originals.Any(x => x.name == filter.name)) continue;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(filter.sharedMesh, out string guid, out long id);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith(Generated, StringComparison.Ordinal)) throw new InvalidOperationException("Cannot infer original mesh from a generated cut: " + filter.name);
            originals.Add(new SourceMesh { name = filter.name, asset = path, fileId = id });
            added = true;
        }
        // Persist recovery data BEFORE the first mutation, including if disk/Editor execution is interrupted.
        if (added) File.WriteAllText(manifestPath, JsonConvert.SerializeObject(originals, Formatting.Indented));
        // Bridge branch from VideoStationConnections: P(-64.5,39.65,7) to ray-confirmed source deck world(35,7,-60).
        // Walk probe: a1.2m parapet (front face t≈-1.25) and the concourse facade (back face t≈2.23, +0.546/m across)
        // cross the corridor obliquely. The door spans exactly that band inside the4m corridor; floor(y7) and the
        // source member aty11.4 stay intact. Door size is inferred from the corridor, not surveyed.
        Vector3 branchStart = W(-64.5f, 39.65f, 7), sourceDeck = new Vector3(35, 7, -60);
        Vector3 along = Vector3.ProjectOnPlane(sourceDeck - branchStart, Vector3.up).normalized;
        Vector3 doorCentre = sourceDeck + along * .45f + Vector3.up * 2.165f;
        var apertures = new List<Aperture> {
            Station("1F filmed retail wing", -4f, 23f, -51.5f, -35f, -.05f, 4.6f),
            Station("2F exit 9 entry connection", -74f, -52f, 0f, 32f, 7.1f, 11.3f),
            Station("Shared descending escalator shaft", 3.4f, 10.3f, -33.5f, -19.5f, .15f, 10.9f),
            // Inferred 1F secondary exits (VideoFloorOne.SecondaryExit at Facade(u)+0.25). Player probes hit the
            // original facade at the door line; door-sized passages only, dimensions inferred.
            Station("1F exit 5 door", -55.45f, -52.35f, -42.22f, -39.72f, -.05f, 2.75f),
            Station("1F exit 2 door", 23.45f, 26.55f, -43.5f, -41f, -.05f, 2.75f),
            Station("1F exit 1 door", 67.85f, 70.95f, -33.22f, -30.72f, -.05f, 2.75f),
            // boarding-gate-probe.json: original track wall at v11.5-12.4 (y7.45-8.5) between the 2F floor and the
            // original overtrack-concourse deck (y7.00, flood-fill connected to the bridge-door concourse).
            Station("2F boarding gate to original overtrack concourse", -40.05f, -35.95f, 10.4f, 13.2f, 7.05f, 10.15f),
            new Aperture { name = "2F bridge branch into source overtrack concourse",
                centre = new[] { doorCentre.x, doorCentre.y, doorCentre.z }, size = new[] { 4f, 4.27f, 6.1f },
                yaw = Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg }
        };
        // Rule-classified original-model intrusions (source-intrusion-cuts.json): 1m strips bounded by measured cells.
        var intrusions = JObject.Parse(File.ReadAllText(Records + "/source-intrusion-cuts.json"));
        foreach (JObject c in (JArray)intrusions["cuts"])
        {
            var strip = Station((string)c["name"], (float)c["u0"], (float)c["u1"], (float)c["v0"], (float)c["v1"], (float)c["y0"], (float)c["y1"]);
            strip.allLayers = true;
            apertures.Add(strip);
        }
        var changes = new List<object>();
        foreach (var filter in filters)
        {
            var record = originals.Single(x => x.name == filter.name);
            var original = AssetDatabase.LoadAllAssetsAtPath(record.asset).OfType<Mesh>().Single(mesh => {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long id);
                return id == record.fileId;
            });
            FpsSourceOpening.Assign(filter, original);
            // Superseded per-box chain assets from earlier runs.
            for (int i = 0; i < 16; i++) AssetDatabase.DeleteAsset(Generated + "/" + filter.name + "_opening" + i + ".asset");
            string path = Generated + "/" + filter.name + "_openings.asset";
            AssetDatabase.DeleteAsset(path);
            var meshBounds = GeometryUtility.CalculateBounds(original.vertices, filter.transform.localToWorldMatrix);
            // Platform details only receive the intrusion strips; the authored openings were designed for the shell.
            bool platformDetail = filter.transform.parent != null && filter.transform.parent.name == "Layer0PlatformsDetails";
            var touching = new List<(Bounds, Vector3)>();
            var names = new List<string>();
            foreach (var opening in apertures)
            {
                if (platformDetail && !opening.allLayers) continue;
                var box = new Bounds(new Vector3(opening.centre[0], opening.centre[1], opening.centre[2]),
                    new Vector3(opening.size[0], opening.size[1], opening.size[2]));
                var rotation = Quaternion.Euler(0, opening.yaw, 0);
                var reach = new Bounds(box.center, Vector3.zero);
                for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
                    reach.Encapsulate(box.center + rotation * Vector3.Scale(box.extents, new Vector3(x, y, z)));
                if (!reach.Intersects(meshBounds)) continue;
                touching.Add((box, new Vector3(0, opening.yaw, 0)));
                names.Add(opening.name);
            }
            if (touching.Count > 0)
            {
                var cut = FpsSourceOpening.Subtract(filter, touching, original.name + "_AuthoredOpenings");
                AssetDatabase.CreateAsset(cut, path);
                FpsSourceOpening.Assign(filter, cut);
            }
            changes.Add(new { target = filter.name, originalAsset = record.asset, originalFileId = record.fileId,
                generatedAsset = touching.Count > 0 ? path : record.asset, openingsApplied = names, vertices = filter.sharedMesh.vertexCount });
        }
        File.WriteAllText(Records + "/source-opening-receipt.json", JsonConvert.SerializeObject(new {
            status = "APPLIED_INFERRED_REGISTRATION", surveyed = false, originalAssetsModified = false,
            reason = "Reconcile exterior-only source shell with video-recovered entry, retail wing, shared stairwell, inferred exits and bridge branch, and remove rule-classified original-model intrusions (source-intrusion-cuts.json); visible and collision apertures are identical.",
            apertures, changes
        }, Formatting.Indented));
    }

    sealed class Aperture
    {
        public string name;
        public float[] centre, size;
        public float yaw;
        public bool allLayers;
    }

    // Station-axis box in (u,v,y); size order matches the rotated local (v,y,u) basis used by W.
    static Aperture Station(string name, float u0, float u1, float v0, float v1, float y0, float y1)
    {
        Vector3 c = W((u0 + u1) * .5f, (v0 + v1) * .5f, (y0 + y1) * .5f);
        return new Aperture { name = name, centre = new[] { c.x, c.y, c.z },
            size = new[] { v1 - v0, y1 - y0, u1 - u0 }, yaw = Angle };
    }

    static void Lighting()
    {
        var previous = GameObject.Find(RootName);
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous);
        var root = new GameObject(RootName);
        // Per-pixel lighting is required for the large batched floor meshes: Forward's per-renderer
        // light limit selected fixtures near the batch centre, not the viewer. Deferred lights opaque
        // surfaces per pixel. Forward+ rendered the same image but raced Editor preview renders
        // (URP ZBinningJob InvalidOperationException, measured 4-70 per lighting pass; Deferred 0/4).
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline != null)
        {
            var settings = new SerializedObject(pipeline);
            settings.FindProperty("m_ReflectionProbeBlending").boolValue=true;
            settings.FindProperty("m_ReflectionProbeBoxProjection").boolValue=true;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var renderers = settings.FindProperty("m_RendererDataList");
            for (int i = 0; i < renderers.arraySize; i++)
            {
                var data = renderers.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
                if (data == null) continue;
                var rendererSettings = new SerializedObject(data);
                rendererSettings.FindProperty("m_RenderingMode").intValue = (int)RenderingMode.Deferred;
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            // Deferred has no MSAA; cameras below use SMAA instead.
            pipeline.msaaSampleCount = 1;
            EditorUtility.SetDirty(pipeline);
        }
        foreach(var fixture in GameObject.Find("영상복원 · 1층").GetComponentsInChildren<Light>())
        {
            fixture.renderMode=LightRenderMode.ForcePixel;
            fixture.intensity=12;
        }
        // Photometric intensities and sparse fixture grouping are visual inference, not a lux survey.
        for(float u=-24;u<=66;u+=9)
        foreach(float cross in new[]{-32f,-20f,-8f})
            Fixture(root.transform,u,cross,10.85f,12,9,false);
        for(float u=-68;u<=-32;u+=12)
        foreach(float cross in new[]{-25f,-8f,6f})
            Fixture(root.transform,u,cross,14.9f-.0575f*u-.025f*cross-.8f,80,22,true);
        var oldProbes = GameObject.Find("부산역 역사 내부 · 반사 프로브");
        if (oldProbes != null) UnityEngine.Object.DestroyImmediate(oldProbes);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.52f, .57f, .65f);
        RenderSettings.ambientEquatorColor = new Color(.36f, .38f, .40f);
        RenderSettings.ambientGroundColor = new Color(.19f, .18f, .17f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.reflectionIntensity = .75f;
        var daylight = GameObject.Find("공간 조명");
        if (daylight != null && daylight.TryGetComponent<Light>(out var light))
        {
            light.intensity = .85f;
            light.shadows = LightShadows.Soft;
            EditorUtility.SetDirty(light);
        }
        var volume = GameObject.Find("맞이방 포스트 볼륨");
        if (volume != null && volume.TryGetComponent<Volume>(out var v) && v.sharedProfile != null)
        {
            if (v.sharedProfile.TryGet<ColorAdjustments>(out var ca))
            {
                ca.postExposure.Override(0);
                ca.contrast.Override(4);
                ca.saturation.Override(0);
                EditorUtility.SetDirty(ca);
            }
            if (v.sharedProfile.TryGet<Bloom>(out var bloom))
            {
                bloom.threshold.Override(1.2f);
                bloom.intensity.Override(.08f);
                EditorUtility.SetDirty(bloom);
            }
            EditorUtility.SetDirty(v.sharedProfile);
        }
        foreach (var location in new[] {
            new {name="High hall",u=-53f,v=-10f,y=12f,size=new Vector3(58,22,62)},
            new {name="Low retail arcade",u=-5f,v=-16f,y=9f,size=new Vector3(51,5,75)},
            new {name="Ground retail",u=8f,v=-31f,y=2.2f,size=new Vector3(45,5,57)}
        })
        {
            var go = new GameObject(location.name + " · reflected interior");
            go.transform.SetParent(root.transform, false);
            go.transform.position = W(location.u, location.v, location.y);
            var probe = go.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.resolution = 128;
            probe.size = location.size;
            probe.boxProjection = true;
            probe.intensity = .75f;
            probe.RenderProbe();
        }
        foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) continue;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
        }
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.sceneViewState.showImageEffects = true;
    }

    static void Fixture(Transform parent,float u,float v,float y,float intensity,float range,bool spot)
    {
        var go=new GameObject(spot?"High hall downlight":"Arcade luminaire group");
        go.transform.SetParent(parent,false);go.transform.position=W(u,v,y);
        go.transform.rotation=Quaternion.Euler(90,0,0);
        var light=go.AddComponent<Light>();light.type=spot?LightType.Spot:LightType.Point;
        light.intensity=intensity;light.range=range;light.spotAngle=110;
        light.color=new Color(1,.96f,.88f);light.renderMode=LightRenderMode.ForcePixel;
        light.shadows=LightShadows.None;
    }
}
