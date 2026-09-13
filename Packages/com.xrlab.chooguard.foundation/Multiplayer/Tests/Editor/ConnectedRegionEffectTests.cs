using NUnit.Framework;
using UnityEngine;

namespace ChooGuard.Foundation.Multiplayer.Tests
{
    public sealed class ConnectedRegionEffectTests
    {
        [Test] public void RequestedEquipmentDoesNotOverrideTheLastPhysicalLightingEffect()
        {
            var root=new GameObject("effect-fixture");
            try
            {
                var view=root.AddComponent<ConnectedRegionView>(); view.ControlsLighting=true;
                var equipment=new GameObject("equipment"); equipment.transform.SetParent(root.transform); view.Equipment=equipment.AddComponent<NetworkEntityAnchor>();
                var light=root.AddComponent<Light>(); light.enabled=false;
                view.Apply(new EntityState {Active=true},false); Assert.That(light.enabled,Is.False);
                view.Apply(null,false); Assert.That(light.enabled,Is.False);
                view.Apply(new EntityState {Active=true}); Assert.That(light.enabled,Is.True,"Legacy requested-light behavior is preserved.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
