string status=".planning/2026-09-21-quality-a/official-world-job-status.txt";
if(UnityEditor.EditorApplication.isCompiling||UnityEditor.EditorApplication.isUpdating)throw new System.InvalidOperationException("Wait for ready editor");
string inspected;using(var sha=System.Security.Cryptography.SHA256.Create())inspected=System.BitConverter.ToString(sha.ComputeHash(System.IO.File.ReadAllBytes(".planning/2026-09-21-quality-a/official-staging-native-receipt.json"))).Replace("-","").ToLowerInvariant();
System.IO.File.WriteAllText(status,"QUEUED "+System.DateTime.UtcNow.ToString("O"));
UnityEditor.EditorApplication.delayCall+=()=>{
var sw=System.Diagnostics.Stopwatch.StartNew();
try{System.IO.File.WriteAllText(status,"BUILDING_CLIPPED_REGIONAL_WORLD "+System.DateTime.UtcNow.ToString("O"));var s=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpStationView>();string registration=".planning/2026-09-21-quality-a/official-station-registration.json";ChooGuard.Editor.MvpCityBuilder.BuildOfficialUnder(s.transform,".planning/2026-09-21-quality-a/world-coverage-drafts/official-source-coverage.json",registration);System.IO.File.WriteAllText(status,"PUBLISHING_OFFICIAL_SOURCE_WORLD elapsedSeconds="+sw.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));ChooGuard.Editor.MvpOfficialStationBinder.PublishExisting(registration,inspected,"art/world/official-source-coverage-receipt.json",true);System.IO.File.WriteAllText(status,"SUCCESS elapsedSeconds="+sw.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));}
catch(System.Exception e){System.IO.File.WriteAllText(status,"ERROR elapsedSeconds="+sw.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+e.ToString());}
};
return new{queued=true,status};
