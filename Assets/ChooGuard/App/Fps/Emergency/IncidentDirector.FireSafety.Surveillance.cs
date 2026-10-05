using System;
using System.Collections.Generic;
using System.Linq;
using ChooGuard.App.Fps.Equipment;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>
    /// The station's CCTV cameras as real objects. The staff member asks the office over the radio to look at the picture of the spot, and the office answers with what the
    /// cameras covering it show: smoke and flames, water on the floor, a shutter down with people in front of it, or nothing unusual. A spot no camera covers
    /// is not on any monitor (the answer says so). Looking at the picture does not replace going there: a detector's lamp is only checked on the spot.
    /// </summary>
    public sealed partial class IncidentDirector
    {
        private readonly List<SurveillanceCamera> cameras = new List<SurveillanceCamera>();

        private void BindCameras()
        {
            foreach (var equipment in EquipmentRegistry.OfKind(SurveillanceCamera.CameraKind).OrderBy(e => e.Id, StringComparer.Ordinal))
            {
                var camera = equipment.GetComponent<SurveillanceCamera>();
                camera.Bind();
                cameras.Add(camera);
            }
        }

        /// <summary>The spot the staff member would ask the office to look at: the tripped detector, the fire, the faulty shutter or the leak they know of.</summary>
        private bool CctvSubject(out Vector3 point, out string where)
        {
            point = default;
            where = "";
            foreach (var fire in fires.Where(f => !f.Extinguished && known.Contains(f))) { point = fire.Position; where = fire.Where; return true; }
            if (shutterFault != null && shutterFault.Active && known.Contains(shutterFault)) { point = shutterFault.Position; where = shutterFault.Where; return true; }
            foreach (var leak in leaks.Where(l => l.Active && known.Contains(l))) { point = leak.Position; where = leak.Where; return true; }
            if (falseAlarm != null && falseAlarm.Active && falseAlarm.Detector != null) { point = falseAlarm.Detector.FloorPoint; where = falseAlarm.Where; return true; }
            var tripped = detectors.FirstOrDefault(d => d.Tripped);
            if (tripped != null) { point = tripped.FloorPoint; where = world.Describe(tripped.FloorPoint); return true; }
            return false;
        }

        private void CctvCheck(Vector3 point, string where)
        {
            var seeing = cameras.Where(c => c.Covers(point)).OrderBy(c => Vector3.Distance(c.transform.position, point)).ToList();
            if (seeing.Count == 0)
            {
                Office("역무실입니다. " + where + " 쪽은 CCTV에 잡히지 않습니다." + (Guided ? " 현장에서 확인해 주십시오." : ""));
                log.Add("역무실 CCTV 확인 · " + where + " · 비추는 카메라 없음");
                return;
            }
            var camera = seeing[0];
            var parts = new List<string>();
            var fire = fires.FirstOrDefault(f => !f.Extinguished && camera.Covers(f.Position));
            if (fire != null) parts.Add("연기와 불꽃이 보입니다" + (fire.Intensity > .6f ? ". 불길이 큽니다" : ""));
            foreach (var leak in leaks.Where(l => l.Active && camera.Covers(l.Position))) parts.Add("천장에서 물이 나와 바닥이 젖어 있습니다" + (leak.Level >= 3 ? ". 물이 넓게 번지고 있습니다" : ""));
            foreach (var shutter in shutters.Where(s => s.Blocking && camera.Covers(s.transform.position)))
            {
                int people = crowd.People.Count(p => Vector3.Distance(p.transform.position, shutter.transform.position) < 6f);
                parts.Add("방화셔터가 내려와 있고 " + (shutterFault != null && shutterFault.Active && shutterFault.Caught != null && !shutterFault.Freed ? "아래에 사람이 끼어 있는 것 같습니다" : people > 3 ? "앞에 사람들이 몰려 있습니다" : "앞은 한산합니다"));
            }
            if (parts.Count == 0) parts.Add("연기나 불꽃은 보이지 않고 승객들이 평소처럼 움직입니다");
            string seen = string.Join(". ", parts);
            Office("역무실입니다. CCTV(" + camera.View + ")로 확인했습니다. " + seen + "." + (!Guided ? "" : fire == null ? " 화재 여부는 현장에서 직접 확인하고 보고해 주십시오." : " 119에 신고하고 접근을 통제하십시오."));
            log.Add("역무실 CCTV 확인 · " + camera.Equipment.Label + " · " + seen);
        }
    }
}
