var station=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();
var roots=new[]{station.transform.Find("공식 자료 부산역 주변 공간"),station.WholeEnvelope.Find("공식 자료 부산역 역사")};
var samples=new[]{new UnityEngine.Vector2(-169.5003f,-40.647f),new UnityEngine.Vector2(-176.7731f,-37.3143f),new UnityEngine.Vector2(-175.315f,-34.132f),new UnityEngine.Vector2(-173.857f,-30.951f),new UnityEngine.Vector2(-170.9408f,-24.587f),new UnityEngine.Vector2(-209.621f,-126.4743f),new UnityEngine.Vector2(-216.8938f,-123.1416f),new UnityEngine.Vector2(-211.0615f,-110.4143f)};
var hits=new System.Collections.Generic.List<object>();
foreach(var root in roots)foreach(var mf in root.GetComponentsInChildren<UnityEngine.MeshFilter>(true)){
var vertices=mf.sharedMesh.vertices;var indices=mf.sharedMesh.triangles;var matrix=station.transform.worldToLocalMatrix*mf.transform.localToWorldMatrix;for(int v=0;v<vertices.Length;v++)vertices[v]=matrix.MultiplyPoint3x4(vertices[v]);
string path=mf.name;var parent=mf.transform.parent;while(parent!=root&&parent!=null){path=parent.name+"/"+path;parent=parent.parent;}
foreach(var q in samples){var values=new System.Collections.Generic.List<float>();var normals=new System.Collections.Generic.List<float>();
for(int i=0;i<indices.Length;i+=3){var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];if(q.x<UnityEngine.Mathf.Min(a.x,b.x,c.x)||q.x>UnityEngine.Mathf.Max(a.x,b.x,c.x)||q.y<UnityEngine.Mathf.Min(a.z,b.z,c.z)||q.y>UnityEngine.Mathf.Max(a.z,b.z,c.z))continue;float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(UnityEngine.Mathf.Abs(det)<.000001f)continue;float u=((b.z-c.z)*(q.x-c.x)+(c.x-b.x)*(q.y-c.z))/det;float v=((c.z-a.z)*(q.x-c.x)+(a.x-c.x)*(q.y-c.z))/det;float w=1-u-v;if(u<-.0001f||v<-.0001f||w<-.0001f)continue;float y=u*a.y+v*b.y+w*c.y;if(y < -8||y > 10)continue;bool duplicate=false;foreach(float old in values)if(UnityEngine.Mathf.Abs(old-y)<.01f)duplicate=true;if(!duplicate){values.Add(y);normals.Add(UnityEngine.Vector3.Cross(b-a,c-a).normalized.y);}}
if(values.Count>0)hits.Add(new{x=q.x,z=q.y,root=root.name,path,heights=values,upNormals=normals});}}
return hits;
