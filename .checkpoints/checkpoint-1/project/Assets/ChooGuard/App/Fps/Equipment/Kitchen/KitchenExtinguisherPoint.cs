using ChooGuard.App.Fps.Emergency;
using UnityEngine;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// The K-class (주방화재용) extinguisher a shop kitchen must keep (NFTC 101 표 2.1.1.3 제1호 나목: at least one of the extinguishers
    /// added for a restaurant kitchen is K-class; 2.1.1.6: no higher than 1.5 m). It is the same tool as the twin's
    /// extinguishers (the staff member lifts it off the wall, pulls the pin and sprays) with wet chemical in it: only that
    /// cools cooking oil and seals it, powder knocks the flames down and they return, water makes it worse
    /// (<see cref="FireHazard.SuppressWith"/>). Placement data: <c>shop</c>, <c>top</c> (height of its top above the floor, m),
    /// <c>front</c> (the unit vector, world x,z, from the shop's back wall toward its open front: the layout's own frame of the
    /// room, which the kitchen photo tour needs to find the concourse).
    /// </summary>
    [RequireComponent(typeof(StationEquipment))]
    public sealed class KitchenExtinguisherPoint : MonoBehaviour
    {
        public const string Kind = "kitchen_k_extinguisher";

        public StationEquipment Equipment { get; private set; }
        public string Shop { get; private set; } = "";
        public Extinguisher Tool { get; private set; }

        private Renderer[] renderers;

        public void Bind(StaffHands hands)
        {
            Equipment = GetComponent<StationEquipment>();
            Shop = Equipment.Text("shop");
            Tool = gameObject.AddComponent<Extinguisher>();
            Tool.Type = ExtinguishAgent.WetChemical;
            Tool.Serial = "K-" + Shop.Replace("shop-", "");
            Tool.Hands = hands;
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        // 들고 다니는 동안 설비 컬링이 멀리 있다며 렌더러를 끄지 못하게 한다.
        private void LateUpdate()
        {
            if (Tool == null || !Tool.Held) return;
            foreach (var renderer in renderers) renderer.enabled = true;
        }
    }
}
