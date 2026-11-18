using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChooGuard.App.Fps.Shell
{
    // A private runtime copy: authored station materials/pipeline assets stay intact.
    // UI remains at native resolution; only 3D pixels and distant shadows cost less.
    public static class PerformanceRendering
    {
        static UniversalRenderPipelineAsset runtime;
        static RenderPipelineAsset previous;
        static int previousVsync,previousTarget,previousMipLimit;
        public static void Apply(bool enabled)
        {
            if(!UnityEngine.Application.isPlaying)return;
            if(enabled)
            {
                if(runtime!=null)return;
                var source=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
                if(source==null)return;
                previous=QualitySettings.renderPipeline;
                previousVsync=QualitySettings.vSyncCount;previousTarget=UnityEngine.Application.targetFrameRate;
                previousMipLimit=QualitySettings.globalTextureMipmapLimit;
                runtime=Object.Instantiate(source);runtime.name="시연용 화면 설정";runtime.hideFlags=HideFlags.HideAndDontSave;
                runtime.renderScale=Mathf.Min(source.renderScale,.8f);
                runtime.shadowDistance=Mathf.Min(source.shadowDistance,35);
                runtime.shadowCascadeCount=1;runtime.msaaSampleCount=1;
                QualitySettings.renderPipeline=runtime;
                QualitySettings.globalTextureMipmapLimit=Mathf.Max(previousMipLimit,1);
                QualitySettings.vSyncCount=0;UnityEngine.Application.targetFrameRate=60;
            }
            else if(runtime!=null&&QualitySettings.renderPipeline==runtime)
            {
                QualitySettings.renderPipeline=previous;
                QualitySettings.vSyncCount=previousVsync;UnityEngine.Application.targetFrameRate=previousTarget;
                QualitySettings.globalTextureMipmapLimit=previousMipLimit;
                Object.Destroy(runtime);runtime=null;
            }
        }
    }
}
