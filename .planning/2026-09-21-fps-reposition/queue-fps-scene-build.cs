if(UnityEditor.EditorApplication.isCompiling||UnityEditor.EditorApplication.isUpdating)throw new System.InvalidOperationException("Wait for native readiness");
string status=".planning/2026-09-21-fps-reposition/fps-scene-build-status.txt";
System.IO.File.WriteAllText(status,"QUEUED "+System.DateTime.UtcNow.ToString("O"));
UnityEditor.EditorApplication.delayCall+=()=>{var watch=System.Diagnostics.Stopwatch.StartNew();try{System.IO.File.WriteAllText(status,"BUILDING "+System.DateTime.UtcNow.ToString("O"));ChooGuard.Editor.FpsStationSceneBuilder.BuildFromJson(".planning/2026-09-21-fps-reposition/fps-scene-build-spec.json");System.IO.File.WriteAllText(status,"SUCCESS elapsedSeconds="+watch.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));}catch(System.Exception e){System.IO.File.WriteAllText(status,"ERROR elapsedSeconds="+watch.Elapsed.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)+"\n"+e.ToString());}};
UnityEditor.EditorApplication.QueuePlayerLoopUpdate();UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
return new{queued=true,status};
