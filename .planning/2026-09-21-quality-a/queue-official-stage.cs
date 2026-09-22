string status=".planning/2026-09-21-quality-a/official-stage-job-status.txt";
if(UnityEditor.EditorApplication.isCompiling||UnityEditor.EditorApplication.isUpdating)throw new System.InvalidOperationException("Wait until import/compile is ready");
System.IO.File.WriteAllText(status,"QUEUED "+System.DateTime.UtcNow.ToString("O"));
UnityEditor.EditorApplication.delayCall+=()=>{
var sw=System.Diagnostics.Stopwatch.StartNew();
try{System.IO.File.WriteAllText(status,"RUNNING "+System.DateTime.UtcNow.ToString("O"));ChooGuard.Editor.MvpOfficialStationBinder.StageExisting(".planning/2026-09-21-quality-a/official-station-registration.json");System.IO.File.WriteAllText(status,"SUCCESS elapsedSeconds="+sw.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));}
catch(System.Exception e){System.IO.File.WriteAllText(status,"ERROR elapsedSeconds="+sw.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+e.ToString());}
};
return new{queued=true,status};
