// Adapted installed Core RP17.3 LODRenderingUtils.cs:11-65 and GPUDriven InstanceCuller/InstanceCullerBurst.
// package 0bb36005e9ba; Unity Companion License (source-available, not OSS), full license in ThirdParty.
// Production source explicitly follows C++ LODGroupManager. Domain adaptation: non-crossfade renderer membership.
using UnityEngine;
namespace ChooGuard.App.Fps.TrainDeparture
{
    public static class VisualLodSelection
    {
        public static int Select(float[] heights,float worldSize,float distance,float fieldOfView,bool orthographic,float orthoSize,float lodBias,int maximumLod)
        {
            float metric=orthographic?2*orthoSize:2*Mathf.Tan(Mathf.Deg2Rad*fieldOfView*.5f);
            metric/=Mathf.Max(.00001f,lodBias);
            float scaledDistance=orthographic?metric:distance*metric;
            int start=Mathf.Clamp(maximumLod,0,heights.Length);
            for(int i=start;i<heights.Length;i++)
                if(heights[i]<=0 || scaledDistance<=worldSize/heights[i])return i;
            return -1;
        }
        public static float WorldSize(LODGroup group)
        {
            Vector3 scale=group.transform.lossyScale;
            float largest=Mathf.Abs(scale.x);largest=Mathf.Max(largest,Mathf.Abs(scale.y));largest=Mathf.Max(largest,Mathf.Abs(scale.z));
            return largest*group.size;
        }
    }
}
