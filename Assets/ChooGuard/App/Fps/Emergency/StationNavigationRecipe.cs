using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>One shared current-source recipe; owns no original scene object or mesh.</summary>
    public static class StationNavigationRecipe
    {
        public static NavMeshBuildSettings Settings()
        {
            var settings=NavMesh.GetSettingsByID(0);
            settings.agentRadius=.28f; settings.agentHeight=1.8f;
            settings.agentClimb=.35f; settings.agentSlope=36;
            settings.overrideVoxelSize=true; settings.voxelSize=.12f;
            settings.overrideTileSize=true; settings.tileSize=256; settings.minRegionArea=6;
            return settings;
        }

        public static bool HasSurface(int vertexCount,uint[] indexCounts)
        {
            if(vertexCount<0||indexCounts==null)throw new ArgumentException("Invalid source mesh buffer metadata");
            bool indexed=indexCounts.Any(n=>n>0);
            if(indexed&&vertexCount==0)throw new InvalidOperationException("Indexed source geometry has no vertices");
            return indexed;
        }

        public static bool Excluded(Component source,Transform train,Transform namedBelts)
        {
            if(source==null)return false;
            var t=source.transform;
            if(train!=null&&t.IsChildOf(train)||namedBelts!=null&&t.IsChildOf(namedBelts))return true;
            if(source.GetComponentInParent<FirstPersonResponder>()!=null||source.GetComponentInParent<PersonBody>()!=null)return true;
            var door=source.GetComponentInParent<Facilities.StationDoor>();
            return door!=null&&door.PassableByPeople||source is Collider collider&&(collider.isTrigger||collider is CharacterController);
        }

        public static List<NavMeshBuildSource> Collect(Scene scene,Bounds bounds,Transform train,Transform namedBelts,
            IReadOnlyList<StationNavigationGeometry.Lane> lanes, bool sceneRootsOnly=false)
        {
            if(!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Current station scene must be loaded");
            var sources=new List<NavMeshBuildSource>();
            // Same physics geometry/layer/default-area contract as the canonical editor bake;
            // scene-root collection prevents additive session actors from becoming source geometry.
            if(sceneRootsOnly)
                foreach(var root in scene.GetRootGameObjects())
                {
                    var part=new List<NavMeshBuildSource>();
                    NavMeshBuilder.CollectSources(root.transform,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),part);
                    sources.AddRange(part);
                }
            else NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            sources.RemoveAll(s=>Excluded(s.component,train,namedBelts));
            for(int i=sources.Count-1;i>=0;i--)
            {
                if(sources[i].shape!=NavMeshBuildSourceShape.Mesh)continue;
                if(!(sources[i].sourceObject is Mesh mesh))throw new InvalidOperationException("Missing collected source mesh");
                var counts=Enumerable.Range(0,mesh.subMeshCount).Select(sub=>mesh.GetIndexCount(sub)).ToArray();
                if(!HasSurface(mesh.vertexCount,counts))sources.RemoveAt(i);
            }
            foreach(var lane in lanes)
            {
                sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.ModifierBox,
                    transform=Matrix4x4.TRS(lane.BoxCentre,lane.BoxRotation,Vector3.one),size=lane.BoxSize,area=1});
                if(lane.StairsSize!=Vector3.zero)sources.Add(new NavMeshBuildSource{shape=NavMeshBuildSourceShape.ModifierBox,
                    transform=Matrix4x4.TRS(lane.StairsCentre,lane.BoxRotation,Vector3.one),size=lane.StairsSize,area=StationWorld.StairsArea});
            }
            return sources;
        }
    }
}
