// Read-only on the scene: re-export missing mesh-NNNN.bin files of an old AuditWorldGeometry snapshot from the mesh
// assets they came from (same WGEO format as AuditWorldGeometry). A file is kept only when its SHA-256 equals the one
// recorded in world-geometry.json, so a restored snapshot is byte-identical; changed or deleted assets stay missing.
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class RestoreAuditMeshes
{
    public static string Main(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Expected the audit directory.");
        string dir = Path.GetFullPath(args[0]);
        var data = JObject.Parse(File.ReadAllText(Path.Combine(dir, "world-geometry.json")));
        int restored = 0, mismatch = 0, noAsset = 0, present = 0;
        foreach (JObject item in data["meshes"])
        {
            string file = (string)item["file"], want = (string)item["sha256"], asset = (string)item["asset"], name = (string)item["name"];
            if (file == null || want == null) continue;
            string path = Path.Combine(dir, file);
            if (File.Exists(path)) { present++; continue; }
            Mesh mesh = string.IsNullOrEmpty(asset) ? null
                : AssetDatabase.LoadAllAssetsAtPath(asset).OfType<Mesh>().FirstOrDefault(m => m.name == name);
            if (mesh == null) { noAsset++; continue; }
            string tmp = path + ".tmp";
            var positions = mesh.vertices;
            var indices = mesh.triangles;
            using (var writer = new BinaryWriter(File.Create(tmp)))
            {
                writer.Write(new byte[] { 87, 71, 69, 79 });
                writer.Write(positions.Length); writer.Write(indices.Length);
                foreach (var p in positions) { writer.Write(p.x); writer.Write(p.y); writer.Write(p.z); }
                foreach (int index in indices) writer.Write(index);
            }
            string got;
            using (var source = File.OpenRead(tmp))
            using (var sha = SHA256.Create())
                got = BitConverter.ToString(sha.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            if (got == want) { File.Move(tmp, path); restored++; }
            else { File.Delete(tmp); mismatch++; }
        }
        string summary = "restored=" + restored + " mismatch=" + mismatch + " noAsset=" + noAsset + " present=" + present;
        Debug.Log("RESTORE_AUDIT_MESHES " + dir + " " + summary);
        return summary;
    }
}
