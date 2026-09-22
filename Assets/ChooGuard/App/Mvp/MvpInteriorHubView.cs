using UnityEngine;
namespace ChooGuard.App.Mvp
{
 // Camera-driven cutaway for a game-authored training fit-out at a verified building centroid.
 public sealed class MvpInteriorHubView : MonoBehaviour
 {
  public Camera ViewCamera;
  public Renderer[] ShellRenderers,InteriorRenderers;
  public string SourceId;
  public float RevealRadius=72,MaximumOrthographicSize=180;
  bool[] shellStates;
  void OnEnable(){if(ShellRenderers!=null){shellStates=new bool[ShellRenderers.Length];for(int i=0;i<ShellRenderers.Length;i++)shellStates[i]=ShellRenderers[i]!=null&&ShellRenderers[i].enabled;}}
  void LateUpdate()
  {
   if(ViewCamera==null)return;
   var plane=new Plane(Vector3.up,transform.position);var ray=ViewCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));float distance;
   bool reveal=ViewCamera.orthographic&&ViewCamera.orthographicSize<=MaximumOrthographicSize&&plane.Raycast(ray,out distance)&&Vector3.Distance(ray.GetPoint(distance),transform.position)<RevealRadius;
   if(ShellRenderers!=null)for(int i=0;i<ShellRenderers.Length;i++)if(ShellRenderers[i]!=null)ShellRenderers[i].enabled=!reveal&&(shellStates==null||i>=shellStates.Length||shellStates[i]);
   if(InteriorRenderers!=null)foreach(var renderer in InteriorRenderers)if(renderer!=null)renderer.enabled=reveal;
  }
  void OnDisable(){if(ShellRenderers!=null)for(int i=0;i<ShellRenderers.Length;i++)if(ShellRenderers[i]!=null)ShellRenderers[i].enabled=shellStates==null||i>=shellStates.Length||shellStates[i];}
 }
}
