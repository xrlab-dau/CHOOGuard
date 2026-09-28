using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ImportObservedSurface
{
    public static string Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected observed-surface manifest path.");
        var manifest = JObject.Parse(File.ReadAllText(args[0]));
        if ((string)manifest["schema"] != "chooguard.observed-surface.v1")
            throw new InvalidDataException("Unsupported observed-surface schema.");
        var parent = GameObject.Find("FPSWorld");
        if (!parent) throw new InvalidOperationException("FPSWorld is absent.");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (!shader) throw new InvalidOperationException("URP Unlit shader is absent.");
        string rootName = (string)manifest["rootName"];
        string directory = (string)manifest["assetDirectory"];
        if (!directory.StartsWith("Assets/ChooGuard/Art/StationInterior/", StringComparison.Ordinal))
            throw new InvalidDataException("Surface assets must remain inside StationInterior.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var previous = parent.transform.Find(rootName)?.gameObject;
        var staging = new GameObject(rootName + " · 준비");
        staging.transform.SetParent(parent.transform, true);
        int vertices = 0, triangles = 0, planarColliders = 0;
        try
        {
            int partIndex = 0;
            foreach (var part in (JArray)manifest["parts"])
            {
                using var reader = new BinaryReader(File.OpenRead((string)part["buffer"]));
                if (new string(reader.ReadChars(4)) != "SGM1")
                    throw new InvalidDataException("Invalid surface buffer header.");
                int vertexCount = checked((int)reader.ReadUInt32());
                int indexCount = checked((int)reader.ReadUInt32());
                if (vertexCount <= 0 || vertexCount > 5000000 || indexCount <= 0 || indexCount > 30000000 || indexCount % 3 != 0)
                    throw new InvalidDataException("Invalid surface buffer counts.");
                if (reader.BaseStream.Length != 12L + vertexCount * 20L + indexCount * 4L)
                    throw new InvalidDataException("Truncated surface buffer.");
                var positions = new Vector3[vertexCount];
                var uv = new Vector2[vertexCount];
                var indices = new int[indexCount];
                for (int i = 0; i < vertexCount; i++)
                    positions[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < vertexCount; i++)
                    uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                for (int i = 0; i < indexCount; i++)
                {
                    int index = reader.ReadInt32();
                    if (index < 0 || index >= vertexCount) throw new InvalidDataException("Invalid surface vertex index.");
                    indices[i] = index;
                }
                bool planar = (bool?)part["planarCollider"] ?? false;
                if (planar)
                {
                    var plane = (JArray)part["planeUnity"];
                    if (plane == null || plane.Count != 4)
                        throw new InvalidDataException("Planar collision requires its measured plane.");
                    var normal = new Vector3((float)plane[0], (float)plane[1], (float)plane[2]);
                    if (Mathf.Abs(normal.sqrMagnitude - 1f) > .001f)
                        throw new InvalidDataException("Collision plane normal must be normalized.");
                    foreach (var position in positions)
                        if (Mathf.Abs(Vector3.Dot(normal, position) + (float)plane[3]) > .001f)
                            throw new InvalidDataException("Raw nonplanar surfaces cannot become collision meshes.");
                }
                string meshPath = directory + "/ObservedInterior-" + partIndex + ".asset";
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (!mesh)
                {
                    mesh = new Mesh { name = "영상 복원 표면 · " + partIndex };
                    AssetDatabase.CreateAsset(mesh, meshPath);
                }
                mesh.Clear();
                mesh.indexFormat = vertexCount <= 65535 ? IndexFormat.UInt16 : IndexFormat.UInt32;
                mesh.vertices = positions;
                mesh.uv = uv;
                mesh.triangles = indices;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                EditorUtility.SetDirty(mesh);
                // Texture reimport can reload newly created .asset files; persist populated buffers first.
                AssetDatabase.SaveAssetIfDirty(mesh);
                string texturePath = (string)part["texture"];
                var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                if (!importer) throw new InvalidDataException("Surface texture is absent: " + texturePath);
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 4096;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
                string materialPath = directory + "/ObservedInterior-" + partIndex + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                material.shader = shader;
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Cull", 2);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                var child = new GameObject((string)part["name"] ?? "촬영 구간 " + partIndex);
                child.transform.SetParent(staging.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = child.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                if (planar)
                {
                    child.AddComponent<MeshCollider>().sharedMesh = mesh;
                    planarColliders++;
                }
                vertices += vertexCount;
                triangles += indexCount / 3;
                partIndex++;
            }
            staging.name = rootName;
            string prefabPath = directory + "/ObservedInterior.prefab";
            if (!PrefabUtility.SaveAsPrefabAsset(staging, prefabPath))
                throw new IOException("Could not persist the observed-surface prefab.");
            if (previous) UnityEngine.Object.DestroyImmediate(previous);
            EditorSceneManager.MarkSceneDirty(staging.scene);
            return new JObject { ["root"] = rootName, ["vertices"] = vertices, ["triangles"] = triangles,
                ["rawMeshColliders"] = 0, ["planarColliders"] = planarColliders,
                ["prefab"] = prefabPath, ["sourceModelModified"] = false, ["wholeSceneSaved"] = false }.ToString();
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(staging);
            throw;
        }
    }
}
