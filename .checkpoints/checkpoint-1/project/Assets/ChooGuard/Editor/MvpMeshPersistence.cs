using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace ChooGuard.Editor
{
    /// <summary>Persist procedural meshes while preserving asset GUIDs and refreshing GPU buffers.</summary>
    public static class MvpMeshPersistence
    {
        public static Mesh Store(string assetPath,Mesh generated)
        {
            if(generated==null)throw new ArgumentNullException(nameof(generated));
            if(string.IsNullOrEmpty(assetPath))throw new ArgumentException("A mesh asset path is required.",nameof(assetPath));
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if(existing==null)
            {
                generated.RecalculateBounds();generated.UploadMeshData(false);
                AssetDatabase.CreateAsset(generated,assetPath);EditorUtility.SetDirty(generated);return generated;
            }
            if(existing==generated){existing.RecalculateBounds();existing.UploadMeshData(false);EditorUtility.SetDirty(existing);return existing;}
            // CopySerialized left stale rendered geometry in this scene's controlled comparison.
            // Assign channels explicitly so Unity invalidates and uploads the native mesh buffers.
            existing.Clear(false);existing.name=generated.name;existing.indexFormat=generated.indexFormat;
            existing.vertices=generated.vertices;
            var normals=generated.normals;if(normals.Length>0)existing.normals=normals;
            var tangents=generated.tangents;if(tangents.Length>0)existing.tangents=tangents;
            var colors=generated.colors;if(colors.Length>0)existing.colors=colors;
            var uv=new List<Vector4>();
            for(int channel=0;channel<8;channel++)
            {
                uv.Clear();generated.GetUVs(channel,uv);if(uv.Count>0)existing.SetUVs(channel,uv);
            }
            existing.subMeshCount=generated.subMeshCount;
            for(int sub=0;sub<generated.subMeshCount;sub++)
                existing.SetIndices(generated.GetIndices(sub),generated.GetTopology(sub),sub,false);
            existing.RecalculateBounds();existing.UploadMeshData(false);EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);return existing;
        }
    }
}
