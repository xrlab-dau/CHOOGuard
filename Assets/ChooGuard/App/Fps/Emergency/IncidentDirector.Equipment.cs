using System.Collections.Generic;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// Equipment families: the station's building services and fittings placed as real objects in the map — fire
    /// detection, suppression, compartment and surveillance equipment; electrical equipment and concourse/plaza fittings;
    /// kitchen and gas equipment. Each family implements the partial methods of its own group in its own file
    /// (IncidentDirector.FireSafety.cs, IncidentDirector.ElectricPlaza.cs, IncidentDirector.KitchenGas.cs): causes and
    /// developments built from the equipment present right now, per-frame rules, what the staff member notices, radio
    /// options and follow-ups. A group without a file contributes nothing (C# partial methods).
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private void BeginEquipment()
        {
            // 설비를 먼저 놓는다: 그룹의 Begin 과 원인 목록은 레지스트리에서 실제로 있는 설비를 찾는다.
            if (art.Equipment != null) EquipmentSpawner.Spawn(art.Equipment, transform);
            else Debug.LogWarning("[IncidentDirector] EmergencyArt 에 설비 카탈로그가 없습니다. ChooGuard/Emergency/Equipment 메뉴로 설비 배치를 만드세요.");
            BeginFireSafety();
            BeginElectricPlaza();
            BeginKitchenGas();
        }

        private void EndEquipment()
        {
            EndFireSafety();
            EndElectricPlaza();
            EndKitchenGas();
            EquipmentRegistry.Clear();
        }

        private void EquipmentTick(float dt)
        {
            FireSafetyTick(dt);
            ElectricPlazaTick(dt);
            KitchenGasTick(dt);
        }

        private void EquipmentOrigins(Pools pools, List<Transition> list)
        {
            FireSafetyOrigins(pools, list);
            ElectricPlazaOrigins(pools, list);
            KitchenGasOrigins(pools, list);
        }

        private void EquipmentDevelopments(List<Transition> list)
        {
            FireSafetyDevelopments(list);
            ElectricPlazaDevelopments(list);
            KitchenGasDevelopments(list);
        }

        private void LookAroundEquipment(Vector3 eye, Vector3 forward)
        {
            LookAroundFireSafety(eye, forward);
            LookAroundElectricPlaza(eye, forward);
            LookAroundKitchenGas(eye, forward);
        }

        private IEnumerable<EmergencySession.RadioOption> EquipmentRadio()
        {
            var options = new List<EmergencySession.RadioOption>();
            FireSafetyRadio(options);
            ElectricPlazaRadio(options);
            KitchenGasRadio(options);
            return options;
        }

        private void EquipmentReported(Hazard hazard)
        {
            FireSafetyReported(hazard);
            ElectricPlazaReported(hazard);
            KitchenGasReported(hazard);
        }

        private void EquipmentResolved(Hazard hazard)
        {
            FireSafetyResolved(hazard);
            ElectricPlazaResolved(hazard);
            KitchenGasResolved(hazard);
        }

        // ── 화재 감지·소화·방화구획·감시 설비 ──
        partial void BeginFireSafety();
        partial void EndFireSafety();
        partial void FireSafetyTick(float dt);
        partial void FireSafetyOrigins(Pools pools, List<Transition> list);
        partial void FireSafetyDevelopments(List<Transition> list);
        partial void LookAroundFireSafety(Vector3 eye, Vector3 forward);
        partial void FireSafetyRadio(List<EmergencySession.RadioOption> options);
        partial void FireSafetyReported(Hazard hazard);
        partial void FireSafetyResolved(Hazard hazard);

        // ── 전기 설비·광장/대합실 비품 ──
        partial void BeginElectricPlaza();
        partial void EndElectricPlaza();
        partial void ElectricPlazaTick(float dt);
        partial void ElectricPlazaOrigins(Pools pools, List<Transition> list);
        partial void ElectricPlazaDevelopments(List<Transition> list);
        partial void LookAroundElectricPlaza(Vector3 eye, Vector3 forward);
        partial void ElectricPlazaRadio(List<EmergencySession.RadioOption> options);
        partial void ElectricPlazaReported(Hazard hazard);
        partial void ElectricPlazaResolved(Hazard hazard);

        // ── 주방·가스 설비 ──
        partial void BeginKitchenGas();
        partial void EndKitchenGas();
        partial void KitchenGasTick(float dt);
        partial void KitchenGasOrigins(Pools pools, List<Transition> list);
        partial void KitchenGasDevelopments(List<Transition> list);
        partial void LookAroundKitchenGas(Vector3 eye, Vector3 forward);
        partial void KitchenGasRadio(List<EmergencySession.RadioOption> options);
        partial void KitchenGasReported(Hazard hazard);
        partial void KitchenGasResolved(Hazard hazard);
    }
}
