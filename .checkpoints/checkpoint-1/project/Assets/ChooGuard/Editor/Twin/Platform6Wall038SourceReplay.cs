#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChooGuard.Editor.Twin
{
    // Root-review draft: a fresh exact original wall only, before any asset write.
    public static class Platform6Wall038SourceReplay
    {
        public const string Owner="부산역 공식2층 도면 건축구조_4f4bf4272e3b/wall/PLAN2F-WALL-038";
        public const string Source="Assets/ChooGuard/Art/StationInterior/OfficialFloorplan/Architecture2F_4f4bf4272e3b/PLAN2F-WALL-038.asset";
        const string SourceGuid="8974925d04c56434298997455ea13afa";
        const string SourceSha="f74d0d7101ec56b801ec981e4e500e7ae75cd450e0cf33024788f8b99301ed54";
        const string SourceComplete="3021b3a74a204cebfda72a37d6c092f964dcd2c7a972eea115d4328557629064";
        const string Package=".planning/2026-10-09-interior-99-reconstruction/plan-to-model-execution/full2f-wall-mesh-package-v2.json";
        const string PackageSha="a7e5c39519bad1465a17058c84547d2012ff972d0def02f062cea987572f27ba";
        static void Need(bool ok,string why){if(!ok)throw new InvalidDataException("Exact WALL038 replay: "+why);}
        public static Mesh ResolveFreshOriginal(Mesh generated,string owner,Matrix4x4 world)
        {
            Need(owner==Owner&&generated!=null&&!EditorUtility.IsPersistent(generated),"fresh exact source owner required");
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)Need(float.IsFinite(world[r,c])&&Mathf.Abs(world[r,c]-(r==c?1f:0f))<.000001f,"world-baked source frame changed");
            Need(OfficialFloorplanArchitectureRegistry.Hash(Package)==PackageSha&&OfficialFloorplanArchitectureRegistry.Hash(Source)==SourceSha&&AssetDatabase.AssetPathToGUID(Source)==SourceGuid,"original56 package/source identity changed");
            Need(ArchitecturalCorrectionRegistry.CompleteChannelDigest(generated)==SourceComplete,"fresh source complete channels changed");
            Need(ArchitecturalCorrectionRegistry.HasCorrection(Source),"exact WALL038 terminal registration missing");
            var result=ArchitecturalCorrectionRegistry.ResolveForRebuild(generated,Source,Owner,world);
            Need(result!=null&&result!=generated&&EditorUtility.IsPersistent(result)&&!EditorUtility.IsDirty(result)&&AssetDatabase.GetAssetPath(result)!=Source,"saved reviewed terminal required");
            return result;
        }
    }
}
#endif
