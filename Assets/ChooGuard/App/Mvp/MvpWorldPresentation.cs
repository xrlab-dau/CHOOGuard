using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace ChooGuard.App.Mvp
{
 // Owns an ephemeral Play-mode pipeline override; never writes the shared URP or project assets.
 [DefaultExecutionOrder(100)]
 public sealed class MvpWorldPresentation : MonoBehaviour
 {
  public MvpOpenWorldCamera Navigation;
  public Camera ViewCamera;
  RenderPipelineAsset previousQualityPipeline;
  UniversalRenderPipelineAsset runtimePipeline;
  bool previousCameraMsaa;
  void OnEnable()
  {
   if(!UnityEngine.Application.isPlaying||runtimePipeline!=null)return;
   var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
   if(source==null){Debug.LogWarning("MVP 장면 렌더 설정: 활성 URP가 없어 임시 설정을 적용하지 않습니다.");return;}
   previousQualityPipeline=QualitySettings.renderPipeline;
   runtimePipeline=Instantiate(source);runtimePipeline.name="MVP scene runtime presentation";runtimePipeline.hideFlags=HideFlags.HideAndDontSave;
   runtimePipeline.renderScale=1;runtimePipeline.msaaSampleCount=4;runtimePipeline.shadowCascadeCount=4;
   runtimePipeline.mainLightShadowmapResolution=4096;
   runtimePipeline.cascade4Split=new Vector3(.25f,.48f,.72f);
   // Installed URP exposes the main-light and soft-shadow setters only internally. Overwrite fields on this private clone only.
   JsonUtility.FromJsonOverwrite("{\"m_SoftShadowsSupported\":true,\"m_MainLightShadowsSupported\":true}",runtimePipeline);
   if(ViewCamera!=null){previousCameraMsaa=ViewCamera.allowMSAA;ViewCamera.allowMSAA=true;}
   UpdateRange();QualitySettings.renderPipeline=runtimePipeline;
  }
  void LateUpdate(){UpdateRange();}
  void UpdateRange()
  {
   if(runtimePipeline==null)return;
   float eye=Navigation!=null?Navigation.ViewDistance:750;
   float zoom=Navigation!=null?Navigation.Zoom:260;
   // Include the focus plane, full visible depth, and the observed visual-height envelope.
   runtimePipeline.shadowDistance=Mathf.Max(1050,eye+zoom*2+300);
  }
  void OnDisable()
  {
   if(runtimePipeline==null)return;
   if(QualitySettings.renderPipeline==runtimePipeline)QualitySettings.renderPipeline=previousQualityPipeline;
   if(ViewCamera!=null)ViewCamera.allowMSAA=previousCameraMsaa;
   Destroy(runtimePipeline);runtimePipeline=null;
  }
 }
}
