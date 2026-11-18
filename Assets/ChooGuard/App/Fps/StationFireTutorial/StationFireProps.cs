using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    /// <summary>Authored lesson props, with no claim of surveyed station inventory.</summary>
    public static class StationFireProps
    {
        public static GameObject CartonPile(Transform parent,Material cardboard)
        {
            var root=new GameObject("기둥 옆 적치 종이상자 · 일반 가연물");root.transform.SetParent(parent,false);
            // Thin cardboard walls/flaps and contents, not a practice burner or a wooden fuel stack.
            CartonPanel(root.transform,"상자 바닥",new Vector3(0,.012f,0),new Vector3(.72f,.024f,.52f),cardboard);
            CartonPanel(root.transform,"상자 앞벽",new Vector3(0,.21f,-.25f),new Vector3(.72f,.42f,.012f),cardboard);
            CartonPanel(root.transform,"상자 뒤벽",new Vector3(0,.21f,.25f),new Vector3(.72f,.42f,.012f),cardboard);
            CartonPanel(root.transform,"상자 옆벽",new Vector3(-.35f,.21f,0),new Vector3(.012f,.42f,.5f),cardboard);
            CartonPanel(root.transform,"상자 옆벽",new Vector3(.35f,.21f,0),new Vector3(.012f,.42f,.5f),cardboard);
            var left=CartonPanel(root.transform,"벌어진 덮개",new Vector3(-.4f,.43f,0),new Vector3(.19f,.009f,.5f),cardboard);
            left.localRotation=Quaternion.Euler(0,0,-22);
            var right=CartonPanel(root.transform,"접힌 덮개",new Vector3(.39f,.42f,0),new Vector3(.17f,.009f,.5f),cardboard);
            right.localRotation=Quaternion.Euler(0,0,28);
            for(int i=0;i<12;i++)
            {
                var sheet=new GameObject("상자 안 종이 "+i,typeof(MeshFilter),typeof(MeshRenderer));sheet.transform.SetParent(root.transform,false);
                sheet.transform.localPosition=new Vector3((i%3-1)*.17f,.35f+(i/3)*.012f,(i%2-.5f)*.2f);
                sheet.transform.localRotation=Quaternion.Euler(0,i*47,0);sheet.transform.localScale=new Vector3(2.1f,1,1.6f);
                sheet.GetComponent<MeshFilter>().sharedMesh=FoldedSheet();sheet.GetComponent<MeshRenderer>().sharedMaterial=cardboard;
            }
            return root;
        }
        static Transform CartonPanel(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(parent,false);
            part.transform.localPosition=position;part.transform.localScale=scale;part.GetComponent<Renderer>().sharedMaterial=material;return part.transform;
        }
        static Mesh FoldedSheet()
        {
            var vertices=new List<Vector3>();var faces=new List<int>();
            for(int side=0;side<2;side++)for(int x=0;x<5;x++)for(int z=0;z<2;z++)
                vertices.Add(new Vector3((x-2)*.025f,Mathf.Sin(x*1.5f)*.014f-side*.0008f,(z-.5f)*.12f));
            for(int x=0;x<4;x++)
            {
                int a=x*2,b=a+1,c=a+2,d=a+3;
                faces.AddRange(new[]{a,b,c,b,d,c,a+10,c+10,b+10,b+10,c+10,d+10});
                faces.AddRange(new[]{a,c,a+10,c,c+10,a+10,b,b+10,d,d,b+10,d+10});
            }
            faces.AddRange(new[]{0,10,1,1,10,11,8,9,18,9,19,18});
            var mesh=new Mesh{name="접힌 종이 · 얇은 닫힌 몸체"};mesh.SetVertices(vertices);mesh.SetTriangles(faces,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
