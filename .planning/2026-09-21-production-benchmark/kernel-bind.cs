ChooGuard.Editor.MvpProgressionGraphBinder.BindExisting();
ChooGuard.Editor.MvpJevOperationalKernelBinder.BindExisting();
var w=UnityEngine.Object.FindFirstObjectByType<ChooGuard.App.Mvp.MvpWorkspace>();var g=w.GetComponent<ChooGuard.App.Mvp.MvpProgressionGraph>();
var view=g.Snapshot("");var zero=new ChooGuard.App.Mvp.ThirdParty.OpenRaCountdown(0);var timer=new ChooGuard.App.Mvp.ThirdParty.OpenRaCountdown(.01f);float initial=timer.RemainingSeconds;bool unchanged=!timer.AdvanceAcceptedSeconds(0)&&timer.RemainingSeconds==initial;bool before=!timer.AdvanceAcceptedSeconds(.003f);bool finished=timer.AdvanceAcceptedSeconds(.008f);var cancel=new ChooGuard.App.Mvp.ThirdParty.OpenRaCountdown(30);cancel.Cancel();
return new{ready=g.Ready,hash=g.GraphHash,nodes=view.Nodes.Length,edges=view.Edges.Length,kernel=w.GetComponent<ChooGuard.App.Mvp.MvpJevOperationalKernel>()!=null,zeroComplete=zero.Complete,unchanged,before,finished,cancelled=cancel.Complete,saved=!w.gameObject.scene.isDirty};
