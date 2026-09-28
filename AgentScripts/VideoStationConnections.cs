using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// The filmed footbridge trajectory is relative SfM evidence. Ground/platform joins and widths
// are explicit inferred connections, not measured station construction documents.
public static class VideoStationConnections
{
    const string Art = "Assets/ChooGuard/Art/StationInterior/VideoIntegration";
    const string Base = "Assets/ChooGuard/Art/StationInterior/";
    const string Name = "영상복원 · 승강장 연결";
    static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
    static Transform root;
    static Material floor, metal, glass, frame, tactile;
    static Vector3 P(float u, float v, float y) => new Vector3(v, y, u);

    public static void Main(string[] args)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit mode required.");
        Directory.CreateDirectory(Art); AssetDatabase.Refresh();
        var old = GameObject.Find(Name); if (old != null) UnityEngine.Object.DestroyImmediate(old);
        root = new GameObject(Name).transform; root.rotation = Quaternion.Euler(0, 16.2f, 0);
        batches.Clear();
        floor = Material("BridgeStone", "Materials/PBR_Granite005A_2K.mat", new Color(.76f,.78f,.77f), .83f, .05f);
        metal = Material("BridgeSteel", "Materials/PBR_Metal032_2K.mat", new Color(.72f,.75f,.76f), .63f, .8f);
        frame = Material("BridgeDarkFrame", "Materials/PBR_Metal032_2K.mat", new Color(.13f,.15f,.16f), .4f, .7f);
        tactile = Material("BridgeTactile", "Materials/PBR_TactilePaving002_4K.mat", new Color(.95f,.76f,.12f), .28f, 0);
        glass = Material("BridgeGlass", null, new Color(.51f,.74f,.78f,.18f), .9f, .05f);
        glass.SetFloat("_Surface",1); glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
        glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha); glass.SetFloat("_ZWrite",0);
        glass.SetFloat("_Cull",0); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        glass.SetOverrideTag("RenderType","Transparent"); glass.renderQueue=3000; glass.SetShaderPassEnabled("ShadowCaster",false);
        var bridge = new[] {P(-91.69f,29.08f,7),P(-83.95f,35.20f,7),P(-73.57f,33.83f,7),P(-68.67f,27.59f,7)};
        PlatformHall(bridge);
        // End at a source deck point proven by a source-only physics ray: world(35,7,-60).
        Vector3 sourceDeck = Quaternion.Inverse(root.rotation)*new Vector3(35,7,-60);
        Corridor(P(-64.5f,39.65f,7),sourceDeck,4,"",true);
        SourcePortal(P(-64.5f,39.65f,7),sourceDeck);
        Stairs(-91.7f,16.2f,29.08f,0,7,2.2f,false);
        Stairs(-88.3f,16.2f,29.08f,0,7,1.15f,true);
        B("PlatformApron",floor,true).Box(P(-91.2f,13.9f,-.12f),new Vector3(5.2f,.24f,11));
        B("UpperLanding",floor,true).Box(P(-90.7f,30.58f,6.89f),new Vector3(3f,.22f,9));
        foreach(var batch in batches.Values) batch.Save();
        AssetDatabase.SaveAssets(); Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        File.WriteAllText(".planning/2026-09-23-video-twin/platform-connection-build.json",JsonConvert.SerializeObject(new {
            status="BUILT_REQUIRES_WALK_AND_VISUAL_VERIFICATION",surveyed=false,
            observed="Video41.5..78 relative camera path, fixed stair alongside escalator, enclosed glass/steel bridge and exit9 approach",
            inferred="Widths, roof structure, lower stair extrapolation to level0, platform apron and branch to existing source deck. Source deck world35,7,-60 was ray-confirmed before construction. Branch enters the source concourse through a4m x4.3m door cut by VideoStationIntegration (parapet+facade band probed by player walk); door size and portal frame are inferred.",
            bridgeUV=new[]{new[]{-91.69f,29.08f},new[]{-83.95f,35.2f},new[]{-73.57f,33.83f},new[]{-68.67f,27.59f}},
            stairBottomUV=new[]{-91.7f,16.2f},stairTopUV=new[]{-91.7f,29.08f},floorY=7,generatedMeshBatches=batches.Count
        },Formatting.Indented));
    }

    static Material Material(string name,string source,Color color,float smooth,float metallic)
    {
        string path=Art+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){var original=source==null?null:AssetDatabase.LoadAssetAtPath<Material>(Base+source);
            if(source!=null&&original==null)throw new InvalidOperationException("Missing existing finish: "+source);
            mat=original==null?new Material(Shader.Find("Universal Render Pipeline/Lit")):new Material(original); AssetDatabase.CreateAsset(mat,path);}
        mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);mat.SetFloat("_Metallic",metallic);EditorUtility.SetDirty(mat);return mat;
    }
    static Batch B(string name,Material material,bool collision=false)
    {if(!batches.TryGetValue(name,out var result)){result=new Batch(name,material,collision);batches.Add(name,result);}return result;}
    static void PlatformHall(Vector3[] route)
    {
        // The video bridge is a wide enclosed transfer hall, not a narrow outdoor gangway.
        // Width/extent remain inferred; the relative camera track and open stair throats constrain it.
        var deck=B("PlatformHallFloor",floor,true);
        deck.Box(P(-94.6f,35,6.9f),new Vector3(26,.2f,2.8f));
        deck.Box(P(-75.15f,35,6.9f),new Vector3(26,.2f,24.3f));
        deck.Box(P(-90.25f,39.25f,6.9f),new Vector3(17.5f,.2f,5.9f));
        for(int i=0;i<route.Length-1;i++)
        {
            Vector3 a=route[i],b=route[i+1],side=Vector3.Cross(Vector3.up,(b-a).normalized)*.17f;
            B("TactileGuidance",tactile).Quad(a-side+Vector3.up*.018f,b-side+Vector3.up*.018f,b+side+Vector3.up*.018f,a+side+Vector3.up*.018f);
        }
        var walls=B("PlatformHallGlass",glass,true);
        walls.Quad(P(-96,22,7.25f),P(-96,48,7.25f),P(-96,48,11.7f),P(-96,22,11.7f));
        walls.Quad(P(-63,37.5f,7.25f),P(-63,22,7.25f),P(-63,22,11.7f),P(-63,37.5f,11.7f));
        walls.Quad(P(-63,48,7.25f),P(-63,44,7.25f),P(-63,44,11.7f),P(-63,48,11.7f));
        walls.Quad(P(-75,22,7.25f),P(-96,22,7.25f),P(-96,22,11.7f),P(-75,22,11.7f));
        walls.Quad(P(-96,48,7.25f),P(-63,48,7.25f),P(-63,48,11.7f),P(-96,48,11.7f));
        var opaque=B("PlatformHallRoof",metal);
        opaque.Quad(P(-96,22,11.95f),P(-96,31,11.95f),P(-63,31,11.95f),P(-63,22,11.95f));
        opaque.Quad(P(-96,39,11.95f),P(-96,48,11.95f),P(-63,48,11.95f),P(-63,39,11.95f));
        B("PlatformHallSkylight",glass).Quad(P(-96,31,12.25f),P(-96,39,12.25f),P(-63,39,12.25f),P(-63,31,12.25f));
        var structure=B("PlatformHallSteel",metal,true);
        for(float u=-96;u<=-63;u+=3.3f)
        {
            structure.Tube(P(u,22,11.65f),P(u,48,11.65f),.115f);
            structure.Tube(P(u,31,10.8f),P(u,39,10.8f),.1f);
            structure.Tube(P(u,31,11.65f),P(u,35,10.8f),.065f);
            structure.Tube(P(u,35,10.8f),P(u,39,11.65f),.065f);
            structure.Tube(P(u,22,7),P(u,22,11.7f),.075f);
            structure.Tube(P(u,48,7),P(u,48,11.7f),.075f);
        }
        foreach(float v in new[]{22f,31f,39f,48f})structure.Tube(P(-96,v,11.7f),P(-63,v,11.7f),.14f);
        // Guard only the sides of the stairwell; both lower and upper entrances remain open.
        foreach(float u in new[]{-93.2f,-87.3f})
            B("PlatformWellGuard",metal,true).Tube(P(u,22,8.1f),P(u,29.08f,8.1f),.045f);
    }
    // Jambs and head on the probed concourse facade back line (t = 2.23 + 0.546 * across), framing the
    // source aperture cut by VideoStationIntegration. The oblique head follows the facade, not the corridor.
    static void SourcePortal(Vector3 start,Vector3 deck)
    {
        Vector3 along=Vector3.ProjectOnPlane(deck-start,Vector3.up).normalized,side=Vector3.Cross(Vector3.up,along);
        Vector3 left=deck+along*(2.23f-.546f*2)-side*2,right=deck+along*(2.23f+.546f*2)+side*2,head=Vector3.up*4.3f;
        var portal=B("SourceConcoursePortal",frame,true);
        portal.Tube(left,left+head,.09f);
        portal.Tube(right,right+head,.09f);
        portal.Tube(left+head,right+head,.11f);
    }
    static void Corridor(Vector3 a,Vector3 b,float width,string junction,bool branch)
    {
        Vector3 direction=(b-a).normalized,side=Vector3.Cross(Vector3.up,direction),half=side*width*.5f;
        var deck=B("BridgeDeck",floor,true);
        deck.Quad(a-half,b-half,b+half,a+half);
        deck.Quad(a+half-Vector3.up*.18f,b+half-Vector3.up*.18f,b-half-Vector3.up*.18f,a-half-Vector3.up*.18f);
        B("TactileGuidance",tactile).Quad(a-side*.17f+Vector3.up*.018f,b-side*.17f+Vector3.up*.018f,b+side*.17f+Vector3.up*.018f,a+side*.17f+Vector3.up*.018f);
        float length=Vector3.Distance(a,b);
        for(int sign=-1;sign<=1;sign+=2)
        {
            float begin=branch?4:0,end=length;
            if(sign==1&&junction=="start")begin=3.2f;
            if(sign==1&&junction=="end")end-=3.2f;
            if(end<=begin)continue;
            Vector3 left=a+direction*begin+half*sign,right=a+direction*end+half*sign;
            B("BridgeSideGlass",glass,true).Quad(left+Vector3.up*.22f,right+Vector3.up*.22f,right+Vector3.up*4.45f,left+Vector3.up*4.45f);
            B("BridgeHandrails",metal,true).Tube(left+Vector3.up*1.15f,right+Vector3.up*1.15f,.045f);
            B("BridgeFrames",frame).Tube(left+Vector3.up*4.5f,right+Vector3.up*4.5f,.10f);
            for(float d=begin;d<=end;d+=3.4f)
            {
                Vector3 p=a+direction*d+half*sign;
                B("BridgeFrames",frame).Tube(p,p+Vector3.up*4.5f,.075f);
            }
        }
        Vector3 ridgeA=a+Vector3.up*5.9f,ridgeB=b+Vector3.up*5.9f;
        var roof=B("BridgeRoofGlazing",glass);
        roof.Quad(a-half+Vector3.up*4.5f,ridgeA,ridgeB,b-half+Vector3.up*4.5f);
        roof.Quad(ridgeA,a+half+Vector3.up*4.5f,b+half+Vector3.up*4.5f,ridgeB);
        B("BridgeRoofSteel",frame).Tube(ridgeA,ridgeB,.13f);
        for(float d=0;d<=length;d+=3.4f)
        {
            Vector3 p=a+direction*d;
            var truss=B("BridgeRoofSteel",frame);
            truss.Tube(p-half+Vector3.up*4.5f,p+Vector3.up*5.9f,.10f);
            truss.Tube(p+Vector3.up*5.9f,p+half+Vector3.up*4.5f,.10f);
            truss.Tube(p-half+Vector3.up*4.5f,p+half+Vector3.up*4.5f,.08f);
            truss.Tube(p+Vector3.up*4.5f,p+Vector3.up*5.9f,.065f);
        }
    }
    static void Stairs(float u,float v0,float v1,float y0,float y1,float width,bool escalator)
    {
        int count=40;float rise=(y1-y0)/count,going=(v1-v0)/count;
        var treads=B(escalator?"PlatformEscalatorTreads":"PlatformStairTreads",metal,!escalator);
        for(int i=0;i<count;i++)
        {
            float v=v0+(i+.5f)*going,y=y0+(i+1)*rise;
            treads.Box(P(u,v,y-.09f),new Vector3(going,.18f,width));
            B("PlatformStepEdges",tactile).Box(P(u,v-going*.43f,y+.007f),new Vector3(.035f,.013f,width));
        }
        if(escalator)B("PlatformEscalatorSlope",metal,true).Quad(P(u-width*.5f,v0,y0),P(u+width*.5f,v0,y0),P(u+width*.5f,v1,y1),P(u-width*.5f,v1,y1));
        for(int sign=-1;sign<=1;sign+=2)
        {
            float side=u+sign*(width*.5f+.08f);
            B("PlatformCirculationRails",escalator?frame:metal,true).Tube(P(side,v0,y0+1.05f),P(side,v1,y1+1.05f),.05f);
            for(int i=0;i<=8;i++)
            {
                float t=i/8f,v=Mathf.Lerp(v0,v1,t),y=Mathf.Lerp(y0,y1,t);
                B("PlatformCirculationPosts",metal,true).Tube(P(side,v,y),P(side,v,y+1.05f),.027f);
            }
            if(escalator)B("PlatformEscalatorSkirts",metal,true).Quad(P(side,v0,y0-.3f),P(side,v1,y1-.3f),P(side,v1,y1+.1f),P(side,v0,y0+.1f));
        }
    }

    sealed class Batch
    {
        readonly string name; readonly Material material;readonly bool collision;
        readonly List<Vector3> vertices=new List<Vector3>();readonly List<int> indices=new List<int>();readonly List<Vector2> uv=new List<Vector2>();
        public Batch(string n,Material m,bool c){name=n;material=m;collision=c;}
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d});float x=Vector3.Distance(a,b),y=Vector3.Distance(b,c);
            uv.AddRange(new[]{Vector2.zero,new Vector2(x,0),new Vector2(x,y),new Vector2(0,y)});
            indices.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        public void Box(Vector3 p,Vector3 size)
        {
            var h=size*.5f;Vector3 a=p+new Vector3(-h.x,-h.y,-h.z),b=p+new Vector3(h.x,-h.y,-h.z),c=p+new Vector3(h.x,-h.y,h.z),d=p+new Vector3(-h.x,-h.y,h.z);Vector3 y=Vector3.up*size.y;
            Quad(a,b,c,d);Quad(a+y,d+y,c+y,b+y);Quad(a,a+y,b+y,b);Quad(b,b+y,c+y,c);Quad(c,c+y,d+y,d);Quad(d,d+y,a+y,a);
        }
        public void Tube(Vector3 a,Vector3 b,float radius)
        {
            var axis=(b-a).normalized;var x=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.right:Vector3.up).normalized;var y=Vector3.Cross(axis,x);
            for(int i=0;i<10;i++){float p=i*Mathf.PI*.2f,q=(i+1)*Mathf.PI*.2f;var c=(x*Mathf.Cos(p)+y*Mathf.Sin(p))*radius;var d=(x*Mathf.Cos(q)+y*Mathf.Sin(q))*radius;Quad(a+c,a+d,b+d,b+c);}
        }
        public void Save()
        {
            string path=Art+"/"+name+".asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool create=saved==null;
            if(create)saved=new Mesh();saved.Clear();saved.name=name;saved.indexFormat=IndexFormat.UInt32;
            saved.SetVertices(vertices);saved.SetUVs(0,uv);saved.SetTriangles(indices,0);saved.RecalculateNormals();saved.RecalculateBounds();saved.RecalculateTangents();saved.UploadMeshData(false);
            if(create)AssetDatabase.CreateAsset(saved,path);else EditorUtility.SetDirty(saved);
            var go=new GameObject(name);go.transform.SetParent(root,false);go.AddComponent<MeshFilter>().sharedMesh=saved;go.AddComponent<MeshRenderer>().sharedMaterial=material;if(collision)go.AddComponent<MeshCollider>().sharedMesh=saved;go.isStatic=true;
        }
    }
}
