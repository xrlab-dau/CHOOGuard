using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChooGuard.App.Fps.StationFireTutorial
{
    public sealed partial class StationFireTutorialSession
    {
        public Shader CharOverlayShader;
        Material InitializeBenchFire()
        {
            if(incidentBench==null||CharOverlayShader==null)throw new InvalidOperationException("실제 의자 화점/그을음 연결이 없습니다");
            var fire=new GameObject("기존 매장 목재 의자 화재");fire.transform.SetParent(transform,false);
            fire.transform.position=incidentFirePoint;FireTarget=fire.transform;
            var hit=fire.AddComponent<BoxCollider>();hit.center=Vector3.up*.18f;hit.size=new Vector3(.6f,.65f,.6f);hit.isTrigger=true;
            fire.AddComponent<ChooGuard.Seeded.Unity.SemanticBinding>().EntityId=FireId;
            var hazard=AddEntity(FireId,"station-bench-fire",incidentFirePoint,"thermal","training-model");
            hazard.Set("fire",.7);hazard.Set("smoke",.15);hazard.Set("mech.effect.radius",1.1);
            var material=new Material(CharOverlayShader){name="현장 목재 표면 그을음·분말 잔류"};material.SetVector("_FireCenter",incidentFirePoint);
            foreach(var source in incidentBench.GetComponentsInChildren<MeshFilter>())
            {
                if(source.sharedMesh==null)continue;
                var overlay=new GameObject("국소 그을음 · "+source.name,typeof(MeshFilter),typeof(MeshRenderer));overlay.transform.SetParent(transform,false);
                overlay.transform.SetPositionAndRotation(source.transform.position,source.transform.rotation);overlay.transform.localScale=source.transform.lossyScale;
                overlay.GetComponent<MeshFilter>().sharedMesh=source.sharedMesh;
                var renderer=overlay.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            return material;
        }
    }
}
