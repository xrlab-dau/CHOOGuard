
using System;
using System.IO;
using System.Linq;
using System.Threading;
using ChooGuard.Seeded;
using ChooGuard.Seeded.Tests;
public static class NativeMenuProbe {
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 static NativeProviderControlReply Await(System.Threading.Tasks.Task<NativeProviderControlReply> task){return task.GetAwaiter().GetResult();}
 static int conflicts;
 static NativeProviderControlReply Completed(NativeProviderControlClient client,string flow,CancellationToken ct){
  try{return Await(client.PollLoginAsync(flow,ct));}
  catch(NativeProviderControlException e){if(e.Code!="provider_session_changed")throw;conflicts++;Await(client.RefreshAsync(ct));return Await(client.PollLoginAsync(flow,ct));}
 }
 public static int Main(string[] args){try{
  NativeProviderControlTests.Run();
  using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(60))){
   var ct=deadline.Token;var connection=SubscriptionConnection.ParseDescriptor(File.ReadAllText(args[0]));bool editable=true;
   var client=connection.CreateControlClient(()=>editable);var initial=Await(client.RefreshAsync(ct));
   Check(initial.State.Accounts.Count==0&&initial.State.Models.Count==0&&initial.State.Subscription.Selection==null,"Fresh native state without any account or selection");
   var first=Await(client.StartLoginAsync("openai-chatgpt",null,false,ct)).Flow;
   Check(first.Pending&&first.AuthorizationUrl.StartsWith("https://auth.openai.com/"),"External OAuth link only");
   Console.WriteLine("LOGIN_A_PENDING");Console.Out.Flush();Console.ReadLine();
   Check(Completed(client,first.FlowId,ct).Flow.Completed,"First login callback and stale-generation recovery");
   var second=Await(client.StartLoginAsync("mock-beta",null,false,ct)).Flow;
   Console.WriteLine("LOGIN_B_PENDING");Console.Out.Flush();Console.ReadLine();
   Check(Completed(client,second.FlowId,ct).Flow.Completed,"Second provider login completion");
   var catalog=Await(client.RefreshCatalogAsync(ct)).State;
   var twins=catalog.Models.Where(m=>m.ModelId=="second").ToArray();Check(twins.Length==2,"Union retains two providers with the same model ID");
   var a=twins.Single(m=>m.ProviderId=="openai-chatgpt");var b=twins.Single(m=>m.ProviderId=="mock-beta");
   var selectedA=Await(client.SelectModelAsync(a,ct)).State;Check(!selectedA.Subscription.CloudEnabled,"Selection never grants subscription consent");
   selectedA=Await(client.SetConsentAsync(true,null,null,ct)).State;var sessionA=connection.LockSelection(selectedA.Subscription);
   var selectedB=Await(client.SelectModelAsync(b,ct)).State;Check(!selectedB.Subscription.CloudEnabled&&!sessionA.IsCurrent,"Selecting another provider invalidates the previous locked model");
   selectedB=Await(client.SetConsentAsync(true,null,null,ct)).State;var sessionB=connection.LockSelection(selectedB.Subscription);
   Check(sessionA.Selection.ModelId==sessionB.Selection.ModelId&&sessionA.Selection.CacheVersion!=sessionB.Selection.CacheVersion,"Provider/account policy identity remains distinct");
   Check(!selectedB.InitialAllowed,"Regular consent does not grant initial-world generation");
   var armed=Await(client.SetConsentAsync(true,1000,128,ct)).State;Check(armed.InitialAllowed&&armed.Subscription.Requests==0,"Explicit one-shot permission does not infer");
   Check(Await(client.RefreshAsync(ct)).State.InitialAllowed&&Await(client.RefreshCatalogAsync(ct)).State.InitialAllowed,"Status and catalog refresh preserve one-shot allowance");
   editable=false;bool locked=false;try{Await(client.SelectModelAsync(a,ct));}catch(NativeProviderControlException e){locked=e.Code=="native_controls_locked_for_play";}Check(locked,"Playing blocks native selection mutation");editable=true;
   var pending=Await(client.StartLoginAsync("openai-chatgpt",null,false,ct)).Flow;
   Check(Await(client.CancelLoginAsync(pending.FlowId,ct)).Flow.Status=="cancelled","Native cancellation leaves no hidden pending login");
   var logout=Await(client.LogoutAsync(b.AccountId,ct));Check(logout.LocalSessionCleared==true,"Explicit account logout succeeds");
   Check(Await(client.ClearSelectionAsync(ct)).State.Subscription.Selection==null,"Selection can be cleared independently from remaining logins");
   Check(conflicts>=1,"The first browser callback exercised generation conflict recovery");
   Console.WriteLine("PASS_NATIVE_MENU_HTTP");return 0;
  }
 }catch(NativeProviderControlException e){Console.Error.WriteLine("NativeControl:"+e.Code);return 1;}
 catch(Exception e){Console.Error.WriteLine(e.GetType().Name+":"+e.Message);return 1;}}
}
