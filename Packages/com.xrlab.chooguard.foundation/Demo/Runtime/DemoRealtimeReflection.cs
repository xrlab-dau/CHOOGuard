using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.Foundation.Demo
{
    [RequireComponent(typeof(ReflectionProbe))]
    public sealed class DemoRealtimeReflection : MonoBehaviour
    {
        // Serialized disabled by the builder, so Null graphics never schedules a cubemap render.
        // Unity 6000.3's built-in probe renderer can crash in -nographics even though normal cameras do not render.
        private void OnEnable()
        {
            GetComponent<ReflectionProbe>().enabled=SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
        }
        private void OnDisable()
        {
            var probe=GetComponent<ReflectionProbe>();
            if(probe!=null)probe.enabled=false;
        }
    }
}
