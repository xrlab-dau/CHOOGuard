using System.Collections.Generic;
using UnityEngine;
namespace ChooGuard.Editor
{
 // A small architectural kit placed by envelope containment and explicit circulation reservations.
 // Dimensions and zoning are visual design assumptions, not measured station rooms.
 public static class MvpStationZones
 {
  public sealed class Zone { public string Kind;public Rect Bounds;public Zone(string kind,float x,float z,float width,float depth){Kind=kind;Bounds=new Rect(x-width/2,z-depth/2,width,depth);} }
  public static List<Zone> Layout(List<Vector2> polygon,int floor)
  {
   var result=new List<Zone>();
   if(floor==0)
   {
    Add(result,polygon,floor,new Zone("진입 포털",-45,-74,8,3));Add(result,polygon,floor,new Zone("진입 포털",-48,-17,8,3));Add(result,polygon,floor,new Zone("진입 포털",-30,39,8,3));
    for(float x=-43;x<=73;x+=10)for(float z=-61;z<=49;z+=55)Add(result,polygon,floor,new Zone("외곽 상점",x,z,8,6));
   }
   else if(floor==1)
   {
    for(float x=-43;x<4;x+=10)Add(result,polygon,floor,new Zone(x<-25?"매표 공간":"역무 공간",x,-64,8,7));
    Add(result,polygon,floor,new Zone("역무 공간",-38,-17,8,7));
    Add(result,polygon,floor,new Zone("승강장 접근 포털",42,15,9,3));Add(result,polygon,floor,new Zone("승강장 접근 포털",67,37,9,3));
    Add(result,polygon,floor,new Zone("수직 동선 코어",-43,-76,9,10));Add(result,polygon,floor,new Zone("수직 동선 코어",8,-15,9,10));
   }
   else
   {
    Add(result,polygon,floor,new Zone("수직 동선 코어",8,-15,9,10));
    for(float x=-39;x<=41;x+=10)for(float z=-66;z<=-16;z+=25)Add(result,polygon,floor,new Zone("식당 전면",x,z,8,6));
   }
   int budget=floor==2?10:16;
   for(float z=-53;z<54&&budget>0;z+=13)for(float x=-43;x<80&&budget>0;x+=13)
    if(Add(result,polygon,floor,new Zone(floor==2?"식당 좌석군":"대합 좌석섬",x,z,8,8)))budget--;
   return result;
  }
  static bool Add(List<Zone> zones,List<Vector2> polygon,int floor,Zone candidate)
  {
   if(!Fits(polygon,candidate.Bounds)||Reserved(candidate.Bounds,floor))return false;
   var padded=candidate.Bounds;padded.xMin-=1;padded.xMax+=1;padded.yMin-=1;padded.yMax+=1;
   foreach(var other in zones)if(padded.Overlaps(other.Bounds))return false;
   zones.Add(candidate);return true;
  }
  public static bool Reserved(Rect r,int floor)
  {
   // Continuous main aisle, north/south circulation, and station team approach remain unfurnished.
   if(r.Overlaps(new Rect(-68,-7,185,15))||r.Overlaps(new Rect(-21,-85,10,155)))return true;
   if(floor==1&&(r.Overlaps(new Rect(-53,-49,32,22))||r.Overlaps(new Rect(-25,-49,10,53))))return true;
   return false;
  }
  public static bool Fits(List<Vector2> polygon,Rect r)
  {
   // Sample every metre along all four edges, plus interior; handles this concave floor envelope.
   for(float x=r.xMin;x<=r.xMax+.001f;x+=Mathf.Min(1,r.width))if(!Inside(polygon,new Vector2(x,r.yMin))||!Inside(polygon,new Vector2(x,r.yMax)))return false;
   for(float z=r.yMin;z<=r.yMax+.001f;z+=Mathf.Min(1,r.height))if(!Inside(polygon,new Vector2(r.xMin,z))||!Inside(polygon,new Vector2(r.xMax,z)))return false;
   var corners=new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)};
   for(int e=0;e<4;e++)for(int i=0;i<polygon.Count;i++)
   {
    var a=corners[e];var b=corners[(e+1)%4];var c=polygon[i];var d=polygon[(i+1)%polygon.Count];
    if(Cross(b-a,c-a)*Cross(b-a,d-a)<-.000001f&&Cross(d-c,a-c)*Cross(d-c,b-c)<-.000001f)return false;
   }
   return Inside(polygon,r.center)&&Inside(polygon,new Vector2(r.xMax,r.yMax));
  }
  static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
  static bool Inside(List<Vector2> p,Vector2 q){bool inside=false;for(int i=0,j=p.Count-1;i<p.Count;j=i++)if((p[i].y>q.y)!=(p[j].y>q.y)&&q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)inside=!inside;return inside;}
 }
}
