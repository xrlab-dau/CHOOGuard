using UnityEngine;
namespace ChooGuard.App.Mvp
{
 // Explicit visual design dimensions; source footprints are metres, these fittings are not surveyed.
 public static class MvpSpatialMetrics
 {
  public const float UnitsPerMetre=1, CrewHeight=1.78f, CivilianHeight=1.72f, FloorHeight=5, DoorHeight=2.1f, SeatHeight=.45f, WalkingSpeed=1.4f;
  public static readonly Vector3 ReferenceOrigin=new Vector3(-52,5.05f,-48);
  public static Vector3 Reference(float x,float z)=>ReferenceOrigin+new Vector3(x,0,z);
  public static Vector3 Layout(float x,float y,float z)=>new Vector3(x*4,y,z*4);
 }
}
