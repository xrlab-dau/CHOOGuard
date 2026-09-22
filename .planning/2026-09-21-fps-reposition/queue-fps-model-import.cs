if(UnityEditor.EditorApplication.isCompiling||UnityEditor.EditorApplication.isUpdating)throw new System.InvalidOperationException("Wait until native scripts/import ready");
string status=".planning/2026-09-21-fps-reposition/fps-model-import-status.txt";
System.IO.File.WriteAllText(status,"QUEUED "+System.DateTime.UtcNow.ToString("O"));
UnityEditor.EditorApplication.delayCall+=()=>{
var watch=System.Diagnostics.Stopwatch.StartNew();
try{
System.IO.File.WriteAllText(status,"IMPORTING_KTX "+System.DateTime.UtcNow.ToString("O"));
var k=ChooGuard.Editor.FpsSourceAssetImporter.Import(".planning/2026-09-21-fps-reposition/bve-import/exports/ktx-single-10car-left-open.fbx",".planning/2026-09-21-fps-reposition/bve-import/ktx-material-manifest.normalized.json","Assets/ChooGuard/Art/Fps/KTX");
System.IO.File.WriteAllText(status,"IMPORTING_METRO elapsedSeconds="+watch.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
var m=ChooGuard.Editor.FpsSourceAssetImporter.Import(".planning/2026-09-21-fps-reposition/metro-import/Busan113FPSSection.fbx",".planning/2026-09-21-fps-reposition/metro-import/fps-material-manifest.normalized.json","Assets/ChooGuard/Art/Fps/Metro113");
System.IO.File.WriteAllText(status,"SUCCESS elapsedSeconds="+watch.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+k+"\n"+m);
}catch(System.Exception e){System.IO.File.WriteAllText(status,"ERROR elapsedSeconds="+watch.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+e.ToString());}
};
return new{queued=true,status};
