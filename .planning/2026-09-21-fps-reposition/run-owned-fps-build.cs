string status=".planning/2026-09-21-fps-reposition/fps-scene-build-status.txt";
UnityEditor.EditorApplication.CallbackFunction owned=null;
var list=UnityEditor.EditorApplication.delayCall;
if(list!=null)foreach(var d in list.GetInvocationList()){
if(d.Target==null)continue;foreach(var f in d.Target.GetType().GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)){
if(f.FieldType==typeof(string)&&(string)f.GetValue(d.Target)==status){owned=(UnityEditor.EditorApplication.CallbackFunction)d;UnityEditor.EditorApplication.delayCall-=owned;break;}}if(owned!=null)break;}
if(owned!=null)owned();
return new{invokedOwnedJob=owned!=null,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,status=System.IO.File.ReadAllText(status)};
