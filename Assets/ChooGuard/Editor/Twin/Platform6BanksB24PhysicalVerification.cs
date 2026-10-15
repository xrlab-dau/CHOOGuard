#if UNITY_EDITOR
// Root callback only. Source-positive stock-body approaches supplement the separately sealed floor helper.
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
namespace ChooGuard.Editor.Twin
{
 public static class Platform6BanksB24PhysicalVerification
 {
  const string Contract="workers/twin/output/platform6-interior-object-audit-20261010/B2-B4-followup/B2-B4-stock-body-approach-contract.json";
  const string ContractSHA="e1c51a236598bb404a0f8f8f5689d52c222cdbca231593738a78a09d1ab73131";
  const string ScenePath="Assets/ChooGuard/Scenes/FpsStation.unity";
  static void Need(bool b,string why)=>Platform6BankB1Replay.Need(b,"B2/B4 stock approaches: "+why);
  static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
  static JArray PV(Vector3 p)=>new JArray(p.x,p.y,p.z);
  static JObject Contact(RaycastHit h)=>new JObject{["owner"]=Platform6BankB1Replay.PathOf(h.collider.transform),["triangle"]=h.triangleIndex,["point"]=PV(h.point),["normal"]=PV(h.normal),["asset"]=h.collider is MeshCollider mc?AssetDatabase.GetAssetPath(mc.sharedMesh):null};
  public static JObject PhysicalVerify()
  {
   Need(File.Exists(Contract)&&Platform6BankB1Replay.Hash(Contract)==ContractSHA,"sealed source approach contract changed");var contract=JObject.Parse(File.ReadAllText(Contract));foreach(string k in new[]{"source_plan","frame"})Need(Platform6BankB1Replay.Hash((string)contract[k])==(string)contract[k+"_sha256"],"source/frame pin changed");
   var exact=Platform6BanksB24PlacementReplay.VerifyInstalled();var scene=SceneManager.GetActiveScene();int handle=scene.handle;string saved=Platform6BankB1Replay.Hash(ScenePath);bool wasDirty=scene.isDirty;Need(scene.path==ScenePath&&!EditorApplication.isCompiling&&!EditorApplication.isPlayingOrWillChangePlaymode,"stable in-memory Edit Scene required");
   var trials=new JArray();bool all=true;bool oldBackfaces=Physics.queriesHitBackfaces;var probe=new GameObject("Temporary source B2 B4 full stock approaches QA"){hideFlags=HideFlags.HideAndDontSave};
   try
   {
    Physics.queriesHitBackfaces=false;var cc=probe.AddComponent<CharacterController>();cc.height=1.72f;cc.radius=.28f;cc.center=Vector3.up*.86f;cc.skinWidth=.025f;cc.stepOffset=.28f;cc.slopeLimit=45;cc.minMoveDistance=0;
    foreach(var route in contract["paths"])
    {
     var points=route["world_path"].Select(V).ToArray();Need(points.Length>=2,"complete source path required");foreach(bool reverse in new[]{false,true})
     {
      var path=reverse?points.Reverse().ToArray():points;cc.enabled=false;probe.transform.position=path[0];cc.enabled=true;Physics.SyncTransforms();bool pass=true;var samples=new JArray();int moves=0;
      for(int segment=1;segment<path.Length&&pass;segment++)
      {
       var begin=path[segment-1];var end=path[segment];int count=Mathf.CeilToInt(Vector3.Distance(begin,end)/.08f);
       for(int step=1;step<=count;step++)
       {
        var target=Vector3.Lerp(begin,end,(float)step/count);var delta=target-probe.transform.position;delta.y=-.03f;var flags=cc.Move(delta);moves++;var actual=probe.transform.position;float error=new Vector2(actual.x-target.x,actual.z-target.z).magnitude;
        var hits=Physics.RaycastAll(actual+Vector3.up*.3f,Vector3.down,.6f,~0,QueryTriggerInteraction.Ignore).Where(h=>h.collider!=cc&&Mathf.Abs(h.point.y-7)<.08f&&h.normal.y>.5f).ToArray();bool okay=error<.04f&&Mathf.Abs(actual.y-7)<.08f&&hits.Length>0;
        if(step==count||step%20==0||!okay)samples.Add(new JObject{["segment"]=segment,["step"]=step,["target"]=PV(target),["actual"]=PV(actual),["horizontal_error"]=error,["flags"]=flags.ToString(),["passed"]=okay,["support_contacts"]=new JArray(hits.Select(Contact))});
        if(!okay){pass=false;break;}
       }
      }
      all&=pass;trials.Add(new JObject{["route_id"]=route["id"].DeepClone(),["source_path"]=route["source_path"].DeepClone(),["reverse"]=reverse,["passed"]=pass,["actual_Move_calls"]=moves,["samples"]=samples});
     }
    }
   }finally{Physics.queriesHitBackfaces=oldBackfaces;UnityEngine.Object.DestroyImmediate(probe);Physics.SyncTransforms();}
   Need(SceneManager.GetActiveScene().handle==handle&&Platform6BankB1Replay.Hash(ScenePath)==saved&&scene.isDirty==wasDirty,"QA must preserve outer transaction Scene handle/saved bytes/dirty flag");
   var result=new JObject{["status"]=all?"PASS":"FAIL",["verification_kind"]="actual_PhysX_stock_CharacterController_Move",["contract_sha256"]=ContractSHA,["exact_complete_bank_bindings"]=exact,["trials"]=trials,["all_six_directional_trials_required"]=true,["height"]=1.72,["radius"]=.28,["skin_width"]=.025,["SceneSave_executed"]=false,["site99percent_verified"]=false};Need(all,"source-positive stock approaches FAIL: "+result.ToString(Newtonsoft.Json.Formatting.None));return result;
  }
 }
}
#endif
