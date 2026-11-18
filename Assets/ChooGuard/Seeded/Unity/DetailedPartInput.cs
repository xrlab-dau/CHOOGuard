using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChooGuard.Seeded.Unity
{
    public sealed partial class DetailedManipulationController
    {
        string partInputMessage;
        float partInputMessageUntil;

        /// <summary>Reports the actual selected/gripped component, not the lesson's expected target.</summary>
        public string PartInputFeedback
        {
            get
            {
                if(Time.unscaledTime<partInputMessageUntil)return partInputMessage;
                var selected=held!=null?held:aimed;
                if(selected==null||Mechanics==null||!poses.TryGetValue(selected.OwnerId+"|"+selected.PartId,out var pose))return null;
                if(pose.Definition.Carryable)
                    return held!=null?"장비 본체를 잡고 있습니다. 움직일 부품에 조준점을 맞춰 다시 잡으세요.":null;
                var name=PartName(selected.PartId);
                if(pose.Definition.Motion=="press")return held!=null?name+" 누르는 중 · 좌클릭을 놓으면 중지":name+" 조준됨 · 좌클릭 유지로 사용 · 놓으면 중지";
                if(held==null)return name+" 조준됨 · 좌클릭을 누른 채 "+PullDirection(pose)+" / 휠 위로";
                float amount=Mathf.InverseLerp((float)pose.Definition.Min,(float)pose.Definition.Max,
                    (float)Mechanics.Position(selected.OwnerId,selected.PartId));
                return name+" 잡힘 · "+Mathf.RoundToInt(amount*100)+"% · "+(amount>=.95f?"준비 완료 · 좌클릭 놓기":PullDirection(pose)+" / 휠 위로");
            }
        }
        static string PartName(string id)
        {
            switch(id){case "pin":return "안전핀";case "nozzle":return "노즐";case "lever":return "레버";case "ptt":return "PTT";default:return "부품";}
        }
        void ExplainPartInput(string message){partInputMessage=message;partInputMessageUntil=Time.unscaledTime+2;}
        void ClearPartInputFeedback(){partInputMessage=null;partInputMessageUntil=0;}
        void ObservePartInputResult(JObject result)
        {
            var selected=held!=null?held:aimed;
            if(selected==null||(string)result["OwnerId"]!=selected.OwnerId||(string)result["PartId"]!=selected.PartId)return;
            if((bool?)result["Accepted"]==true)
            {
                if((double?)result["After"]>(double?)result["Before"]||((string)result["Action"]=="grip"))ClearPartInputFeedback();
                return;
            }
            switch((string)result["Reason"])
            {
                case "grip_required":case "part_not_gripped":ExplainPartInput("부품 잡기가 풀렸습니다. 조준하고 다시 잡아 주세요.");break;
                case "out_of_reach":ExplainPartInput("부품이 손에 닿지 않는 거리입니다. 가까이 이동해 주세요.");break;
                case "movement_blocked":ExplainPartInput("부품이 주변 물체에 걸렸습니다. 여유 공간에서 조작하세요.");break;
                case "safety_pin_engaged":ExplainPartInput("안전핀이 걸려 있습니다. 안전핀을 먼저 잡아 빼세요.");break;
                case "nozzle_not_secured":ExplainPartInput("노즐이 아직 준비되지 않았습니다. 노즐을 먼저 꺼내세요.");break;
                case "supervised_safe_handoff_required":ExplainPartInput("분사 안전 조건을 확인해 주세요. 현장 안내를 확인하세요.");break;
                case "removed_part_cannot_reinsert":ExplainPartInput("분리한 부품은 되돌아가지 않습니다. 분리 방향으로 조작하세요.");break;
            }
        }

        static float WheelDetents(float value)
        {
            // Input System 1.20 defaults to normalized +/-1 on Windows too. Dividing that by120
            // made a single notch move only 0.067%, while the old synthetic test supplied120.
            bool native=InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange;
            return native&&(Application.platform==RuntimePlatform.WindowsPlayer||Application.platform==RuntimePlatform.WindowsEditor)?value/120f:value;
        }
        Vector2 ScreenTravel(PartPose pose)
        {
            var p=pose.Explicit;
            if(p==null||!p.DrivePosition||host?.View==null)return Vector2.right;
            var parent=p.Part.parent;
            var begin=parent!=null?parent.TransformPoint(p.MinLocalPosition):p.MinLocalPosition;
            var end=parent!=null?parent.TransformPoint(p.MaxLocalPosition):p.MaxLocalPosition;
            var a=host.View.WorldToScreenPoint(begin);var b=host.View.WorldToScreenPoint(end);
            var travel=new Vector2(b.x-a.x,b.y-a.y);
            // An almost head-on pull has no stable screen axis; retain an explicit rightward
            // fallback and the wheel rather than amplifying perspective noise.
            return a.z>.05f&&b.z>.05f&&travel.sqrMagnitude>=4?travel.normalized:Vector2.right;
        }
        double PointerStroke(PartPose pose,DetailedInputFrame frame)
            =>Vector2.Dot(new Vector2(frame.PointerDeltaX,frame.PointerDeltaY),ScreenTravel(pose));
        string PullDirection(PartPose pose)
        {
            var direction=ScreenTravel(pose);
            return Mathf.Abs(direction.x)>=Mathf.Abs(direction.y)?(direction.x>=0?"오른쪽으로 드래그":"왼쪽으로 드래그"):
                (direction.y>=0?"위로 드래그":"아래로 드래그");
        }

        DetailedPartBinding ResolvePartTarget(Ray ray,Collider directCollider)
        {
            var direct=directCollider!=null?directCollider.GetComponentInParent<DetailedPartBinding>():null;
            if(direct!=null&&poses.TryGetValue(direct.OwnerId+"|"+direct.PartId,out var exact)&&!exact.Definition.Carryable)return direct;
            // Screen-space picking tolerance does not enlarge physical colliders. Each candidate
            // must still have a real, unoccluded collider surface within the normal reach.
            var center=new Vector2(host.View.pixelRect.center.x,host.View.pixelRect.center.y);
            float padding=14*Mathf.Clamp(host.View.pixelHeight/1080f,.75f,2f),best=padding*padding;
            DetailedPartBinding chosen=direct;
            foreach(var pose in poses.Values)
            {
                if(pose.Explicit==null||pose.Definition.Carryable||!pose.Binding.gameObject.activeInHierarchy)continue;
                if(pose.Definition.OneWay&&Mechanics.Position(pose.Binding.OwnerId,pose.Binding.PartId)>=.95)continue;
                foreach(var collider in pose.Binding.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.GetComponentInParent<DetailedPartBinding>()==pose.Binding))
                {
                    var bounds=collider.bounds;var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);bool inFront=true;
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    {
                        var screen=host.View.WorldToScreenPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
                        if(screen.z<=host.View.nearClipPlane){inFront=false;continue;}
                        var point=new Vector2(screen.x,screen.y);min=Vector2.Min(min,point);max=Vector2.Max(max,point);
                    }
                    if(!inFront)continue;
                    var closest=new Vector2(Mathf.Clamp(center.x,min.x,max.x),Mathf.Clamp(center.y,min.y,max.y));
                    float distance=(center-closest).sqrMagnitude;if(distance>=best)continue;
                    Vector3 surface;
                    if(collider is MeshCollider mesh&&!mesh.convex)
                    {
                        // ClosestPoint is unsupported for concave meshes (and logs
                        // every frame). Raycast the real surface, never its AABB.
                        var direction=bounds.ClosestPoint(ray.origin)-ray.origin;
                        if(!collider.Raycast(new Ray(ray.origin,direction.normalized),out var contact,2.4f)&&
                           !collider.Raycast(new Ray(ray.origin,(bounds.center-ray.origin).normalized),out contact,2.4f))continue;
                        surface=contact.point;
                    }
                    else surface=collider.ClosestPoint(ray.origin);
                    var delta=surface-ray.origin;
                    if(delta.magnitude>2.4f||delta.sqrMagnitude<.000001f)continue;
                    var first=Physics.RaycastAll(ray.origin,delta.normalized,delta.magnitude+.015f,~(1<<2),QueryTriggerInteraction.Collide)
                        .Where(h=>!h.collider.transform.IsChildOf(host.Actor)).OrderBy(h=>h.distance).FirstOrDefault();
                    if(first.collider==null||first.collider.GetComponentInParent<DetailedPartBinding>()!=pose.Binding)continue;
                    chosen=pose.Binding;best=distance;
                }
            }
            return chosen;
        }
    }
}
