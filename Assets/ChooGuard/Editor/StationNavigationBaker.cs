using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using ChooGuard.App.Fps;
using ChooGuard.App.Fps.World;
using DotRecast.Core;
using DotRecast.Detour;
using DotRecast.Detour.Io;
using DotRecast.Recast;
using DotRecast.Recast.Geom;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace ChooGuard.EditorTools
{
    // FpsStation 대합실의 보행 navmesh 를 굽는다.
    //
    // MvpTeamNavigationBuilder 를 쓰지 않는 이유: 그쪽은 MvpStationView.FloorRoots[1] 과
    // 메시 이름("공개 외곽 기반 바닥"·"국소 기준 계산 영역"), 높이 대역 5.35~6.8 에 묶여 있고
    // 바닥 메시가 정확히 2개가 아니면 던진다. FpsStation 의 역사 모델에는 맞지 않는다.
    //
    // 여기서는 이름으로 바닥과 장애물을 가르지 않는다. 대합실 높이 대역의 기하를 통째로 넣고
    // 걸을 수 있는 면은 Recast 가 경사·높이로 판단하게 둔다 — 이름 규칙은 모델이 바뀌면 깨진다.
    public static class StationNavigationBaker
    {
        private const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
        private const string DataPath="Assets/ChooGuard/Settings/StationNavigation/Concourse-v1.bytes";
        private const string RootName="역사 길찾기";

        // 대합실만 굽는다. 역사 전체를 넣으면 굽는 시간과 데이터가 쓸데없이 커지고,
        // 걸어 닿지도 않는 층이 경로 후보로 올라온다.
        private const float Radius=70f;     // 시작점 기준 수평 범위
        // 천장을 넣으면 위를 향한 면이 또 하나의 보행층으로 잡혀 navmesh 가 갈라진다.
        // 사람이 지나는 높이만 본다 — 발밑 조금 아래부터 머리 위 조금까지.
        private const float Below=1.5f;     // 시작점 아래로 이만큼
        private const float Above=2.5f;     // 시작점 위로 이만큼

        [MenuItem("ChooGuard/수직 슬라이스/역사 보행 navmesh 굽기")]
        public static void Bake()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {Debug.LogError("[길찾기] 재생 중에는 구울 수 없습니다.");return;}

            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            if(!scene.IsValid()){Debug.LogError("[길찾기] 씬을 열지 못했습니다 · "+ScenePath);return;}
            var responder=UnityEngine.Object.FindFirstObjectByType<FirstPersonResponder>();
            if(responder==null){Debug.LogError("[길찾기] 플레이어가 없어 대합실 범위를 정할 수 없습니다.");return;}

            var seed=responder.transform.position;
            var band=new Bounds(new Vector3(seed.x,seed.y+(Above-Below)*.5f,seed.z),
                                new Vector3(Radius*2f,Above+Below,Radius*2f));

            var verts=new List<float>();
            var tris=new List<int>();
            int meshes=0,skipped=0,unreadable=0,outOfBand=0;
            foreach(var filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var mesh=filter.sharedMesh;
                if(mesh==null){skipped++;continue;}
                // 읽을 수 없는 메시는 따로 세어 보고한다. 조용히 빠지면 승객이 벽을 통과한다.
                if(!mesh.isReadable){unreadable++;skipped++;continue;}
                var renderer=filter.GetComponent<Renderer>();
                if(renderer!=null&&!band.Intersects(renderer.bounds)){outOfBand++;skipped++;continue;}
                // 플레이어와 튜토리얼 산출물은 지형이 아니다. 승객이 소화기를 벽으로 여기면 안 된다.
                var root=filter.transform.root;
                if(root!=null&&(root.name.StartsWith("소화기 · ")||root.name.StartsWith("튜토리얼 · ")))
                {skipped++;continue;}
                if(filter.transform.IsChildOf(responder.transform)){skipped++;continue;}

                var points=mesh.vertices;
                var indices=mesh.triangles;
                if(points.Length==0||indices.Length==0){skipped++;continue;}
                int offset=verts.Count/3;
                foreach(var point in points)
                {
                    var world=filter.transform.TransformPoint(point);
                    verts.Add(world.x);verts.Add(world.y);verts.Add(world.z);
                }
                foreach(int index in indices)tris.Add(offset+index);
                meshes++;
            }
            if(meshes==0){Debug.LogError("[길찾기] 대합실 범위 안에 읽을 수 있는 메시가 없습니다.");return;}
            Debug.Log("[길찾기] 기하 수집 · 메시 "+meshes+"개 · 삼각형 "+(tris.Count/3)
                      +" · 제외 "+skipped+"개 · 대합실 밖 "+outOfBand+" · 읽기 불가 "+unreadable);
            if(unreadable>0)
                Debug.LogWarning("[길찾기] 읽을 수 없는 메시 "+unreadable
                                 +"개가 navmesh 에서 빠졌습니다. 그 구조물은 승객에게 장애물이 되지 않습니다. "
                                 +"임포트 설정의 Read/Write 를 켜야 반영됩니다.");

            // 사람 치수는 보행 격자 산정과 맞춘다(높이 1.7 · 반지름 0.3 · 단차 0.35).
            // 두 계산이 다른 사람을 가정하면 "격자로는 갈 수 있는데 navmesh 로는 못 가는" 곳이 생긴다.
            var geometry=new RcSampleInputGeomProvider(verts,tris);
            // 격자를 거칠게 잡는다. cs=.2 로 구웠더니 785㎡ 대합실에 폴리곤이 6,328개 나왔고
            // 시작 폴리곤이 1~4개짜리 섬이 되어 어디로도 경로가 나오지 않았다(2026-09-25).
            // 바닥 메시가 잘게 나뉘어 있어 촘촘한 격자가 그 조각을 그대로 옮겨 적은 것이다.
            var config=new RcConfig(RcPartition.WATERSHED,.3f,.2f,45,1.7f,.5f,.3f,
                                    8,20,12,1.3f,6,6,1,true,true,true,new RcAreaModification(1),true);
            var built=new RcBuilder().Build(geometry,
                new RcBuilderConfig(config,geometry.GetMeshBoundsMin(),geometry.GetMeshBoundsMax()),false);
            var poly=built.Mesh;var detail=built.MeshDetail;
            if(poly==null||poly.npolys==0){Debug.LogError("[길찾기] 걸을 수 있는 면이 하나도 나오지 않았습니다.");return;}
            for(int i=0;i<poly.npolys;i++)poly.flags[i]=1;

            var option=new DtNavMeshCreateParams
            {
                verts=poly.verts,vertCount=poly.nverts,polys=poly.polys,polyAreas=poly.areas,
                polyFlags=poly.flags,polyCount=poly.npolys,nvp=poly.nvp,
                detailMeshes=detail.meshes,detailVerts=detail.verts,detailVertsCount=detail.nverts,
                detailTris=detail.tris,detailTriCount=detail.ntris,
                walkableHeight=1.7f,walkableRadius=.3f,walkableClimb=.5f,
                bmin=poly.bmin,bmax=poly.bmax,cs=.3f,ch=.2f,buildBvTree=true,
            };
            var data=DtNavMeshBuilder.CreateNavMeshData(option);
            if(data==null){Debug.LogError("[길찾기] navmesh 생성이 비었습니다.");return;}

            Directory.CreateDirectory(Path.GetDirectoryName(DataPath));
            using(var writer=new BinaryWriter(File.Create(DataPath)))
                new DtMeshDataWriter().Write(writer,data,RcByteOrder.LITTLE_ENDIAN,false);
            AssetDatabase.ImportAsset(DataPath,ImportAssetOptions.ForceSynchronousImport);

            var host=FindOrCreateRoot(scene);
            var navigation=host.GetComponent<StationNavigation>();
            if(navigation==null)navigation=Undo.AddComponent<StationNavigation>(host);
            navigation.NavData=AssetDatabase.LoadAssetAtPath<TextAsset>(DataPath);
            using(var sha=SHA256.Create())
            {
                var source=string.Join(",",verts.Select(v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))
                           +"|"+string.Join(",",tris);
                navigation.GeometryDigest=BitConverter.ToString(
                    sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(source))).Replace("-","").ToLowerInvariant();
            }
            navigation.Reload();
            EditorUtility.SetDirty(navigation);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[길찾기] 구움 · 폴리곤 "+poly.npolys+" · "+DataPath
                      +" · 지문 "+navigation.GeometryDigest.Substring(0,12));
        }

        private static GameObject FindOrCreateRoot(UnityEngine.SceneManagement.Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())
                if(root!=null&&root.name==RootName)return root;
            var created=new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(created,"역사 길찾기 루트 생성");
            return created;
        }
    }
}
