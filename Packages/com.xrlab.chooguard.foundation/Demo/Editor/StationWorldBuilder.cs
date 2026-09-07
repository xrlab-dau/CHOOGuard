using System;
using System.Linq;
using UnityEngine;
using static ChooGuard.Foundation.Demo.Editor.FoundationBlenderAssets;
namespace ChooGuard.Foundation.Demo.Editor
{
    public static class StationWorldBuilder
    {
        public static void Build(Transform root,Material[] materials,TextAsset profile,TextAsset roles)
        {
            var config=JsonUtility.FromJson<StationWorldConfig>(profile.text);config.Validate();
            var worldRoot=Group("StationWorld",root,Vector3.zero);var portals=new StationPortal[config.sites.Length];
            for(var i=0;i<portals.Length;i++)
            {
                var site=config.sites[i];var portalRoot=Group("Portal_"+site.id,worldRoot,Vector3.zero);
                var at=Group("Waypoint",portalRoot,site.portal);
                var obstacle=Box("UnavailablePassage",portalRoot,site.portal+Vector3.up*.9f,new Vector3(site.width,1.8f,.32f),materials[4]);
                for(var stripe=-2;stripe<=2;stripe++)Box("RestrictionStripe",obstacle.transform,new Vector3(stripe*.18f,0,-.505f),new Vector3(.08f,1,.02f),materials[5],false);
                var cue=Group("signal-"+site.id,portalRoot,site.cue);
                var model=Instantiate("HazardIndicator",cue,materials);model.localPosition=Vector3.down*1.1f;
                var lens=model.GetComponentsInChildren<Renderer>().Single(x=>x.name.StartsWith("Beacon_"));
                var cueCollider=cue.gameObject.AddComponent<BoxCollider>();cueCollider.center=Vector3.down*.2f;cueCollider.size=new Vector3(.8f,.9f,.6f);
                var interactable=cue.gameObject.AddComponent<DemoInteractable>();interactable.Configure("signal-"+site.id,site.label+" 현장 확인",0);interactable.ConfigureFeedback(new[]{lens});
                var light=Group("WarningLight",cue,new Vector3(0,.8f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.color=new Color(1,.3f,.1f);light.shadows=LightShadows.None;
                var sign=Instantiate("AssemblySign",portalRoot,materials);sign.localPosition=site.portal+Vector3.up*2.55f;
                FoundationEvacuationBuilder.Label(sign,site.id.ToUpper(),new Vector3(0,.12f,-.09f),.065f);
                portals[i]=portalRoot.gameObject.AddComponent<StationPortal>();portals[i].Configure(site.id,site.label,at,obstacle,cue,lens,sign.GetComponentsInChildren<Renderer>().Where(x=>x.sharedMaterials.Any(material=>material!=null&&material.name=="Green")).ToArray(),light);
                // Separate real objects for route choice; no hidden scenario picker or automatic next-button goal.
                var choice=Group("choose-"+site.id,worldRoot,new Vector3(-1.7f+i*1.7f,1.1f,-5.8f));
                choice.localRotation=Quaternion.Euler(0,180,0);
                Instantiate("DirectionSign",choice,materials);
                var collider=choice.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.8f,.7f,.35f);
                var target=choice.gameObject.AddComponent<DemoInteractable>();target.Configure("choose-"+site.id,site.label+" 선택",0);
                target.ConfigureFeedback(choice.GetComponentsInChildren<Renderer>().Where(x=>x.name.StartsWith("Status_")).ToArray());
                FoundationEvacuationBuilder.Label(choice,site.id.ToUpper(),new Vector3(0,.4f,-.2f),.08f);
            }
            var legacy=root.GetComponentInChildren<DemoGameController>();
            var world=legacy.gameObject.AddComponent<StationWorldController>();
            world.Configure(profile,roles,legacy,legacy.Player,root.GetComponentsInChildren<DemoEvacuee>(),portals,root.GetComponentInChildren<DemoWalkGraph>(),root.GetComponentInChildren<DemoWorldGuide>(),root.GetComponentsInChildren<DemoInteractable>(),legacy.AssemblyPoint,root.Find("Markers/Spawn"));
        }
    }
}
