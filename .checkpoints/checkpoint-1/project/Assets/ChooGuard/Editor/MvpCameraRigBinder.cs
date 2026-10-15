using System;
using System.Collections.Generic;
using ChooGuard.App.Mvp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.Editor
{
    public static class MvpCameraRigBinder
    {
        [MenuItem("ChooGuard/MVP/Bind Camera Rig")]
        public static void Bind()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Bind camera in edit mode.");
            var rig=UnityEngine.Object.FindFirstObjectByType<MvpOpenWorldCamera>(FindObjectsInactive.Include);
            if(rig==null||rig.WorldRoot==null)throw new InvalidOperationException("Open the MVP scene with its world camera first.");
            var data=MvpWorldSurfaceBuilder.Read();var h=data.heightfield;
            if(h==null||h.heights==null||h.heights.Length!=h.nx*h.nz)throw new InvalidOperationException("Valid city DEM is required.");
            Undo.RecordObject(rig,"Bind perspective camera");
            var profile=rig.GetComponent<MvpCameraSurfaceProfile>();if(profile==null)profile=Undo.AddComponent<MvpCameraSurfaceProfile>(rig.gameObject);
            Undo.RecordObject(profile,"Bind camera DEM");
            profile.WorldRoot=rig.WorldRoot;profile.CityRoot=rig.WorldRoot.Find("도시 공개지형");
            if(profile.CityRoot==null)throw new InvalidOperationException("City geometry root is missing.");
            profile.MinX=h.minX;profile.MinZ=h.minZ;profile.Step=h.step;profile.Width=h.nx;profile.Depth=h.nz;profile.Heights=(float[])h.heights.Clone();
            var floors=new List<MvpCameraSurfaceProfile.FloorSurface>();
            var station=UnityEngine.Object.FindFirstObjectByType<MvpStationView>(FindObjectsInactive.Include);
            if(station!=null&&station.FloorRoots!=null)foreach(var root in station.FloorRoots)
            {
                if(root==null)continue;
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.name!="공개 외곽 기반 바닥"&&filter.name!="안내도 기반 식당가와 발코니"&&filter.name!="국소 기준 계산 영역")continue;
                    var mesh=filter.sharedMesh;if(mesh==null)continue;var vertices=mesh.vertices;var indices=mesh.triangles;var triangles=new List<Vector3>();
                    for(int i=0;i+2<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];if(Vector3.Cross(b-a,c-a).y<=0)continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);}
                    floors.Add(new MvpCameraSurfaceProfile.FloorSurface{Root=filter.transform,Triangles=triangles.ToArray()});
                }
            }
            profile.Floors=floors.ToArray();
            rig.Surface=profile;if(rig.Camera==null)rig.Camera=rig.GetComponent<Camera>();
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(rig);EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("Camera bound: perspective, pointer anchor, camera-only DEM; scene save remains explicit.");
        }
    }
}
