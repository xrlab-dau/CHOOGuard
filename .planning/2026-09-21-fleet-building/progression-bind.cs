ChooGuard.Editor.MvpProgressionGraphBinder.BindExisting();
var w=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpWorkspace>();
var g=w.GetComponent<ChooGuard.App.Mvp.MvpProgressionGraph>();
var view=g.Snapshot("");
return new{ready=g.Ready,hash=g.GraphHash,nodes=view.Nodes.Length,edges=view.Edges.Length,idleTraceCount=view.Recent.Length,saved=!w.gameObject.scene.isDirty};
