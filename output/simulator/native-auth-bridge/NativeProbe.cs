
using System;
using System.IO;
using System.Threading;
using ChooGuard.Seeded;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
public static class NativeProbe {
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 static void Pair(SubscriptionConnection.SubscriptionSession session,CancellationToken cancellation){
  var w=new World{Seed=42};var grid=new Entity{Id="grid",Kind="service-cabinet"};
  foreach(var tag in new[]{"semantic","electrical","thermal"})grid.Tags.Add(tag);
  grid.Set("power",1);grid.Set("wetness",.1);grid.Set("insulation",.8);grid.Set("temperature",20);w.Add(grid);
  var player=new Entity{Id="player",Kind="staff"};player.Tags.Add("npc");player.Tags.Add("staff");w.Add(player);
  var original=JsonConvert.DeserializeObject<World>(JsonConvert.SerializeObject(w));
  var composer=new EmergenceComposer(session.CreateEmergenceProvider());
  var record=composer.GenerateAndApplyAsync(w,false,cancellation:cancellation,authorized:true).GetAwaiter().GetResult();
  Check(record.Disposition=="accepted"&&w.Entities["grid"].Get("wetness")==.8,"C# selected initial-world HTTP request and atomic apply");
  string reason;Check(EmergenceComposer.ValidateAndApplyRecorded(original,record,false,out reason)&&original.Digest()==w.Digest(),"Recorded initial condition replay");
  var provider=session.CreateGenerator(128);
  var request=new PlanRequest{Id="native-probe",ActorId="player",Goal="isolate",ContextJson="{}"};
  request.Offers.Add(new OfferedAction{operation="isolate_power",actor_id="player",target_id="grid"});
  var reply=provider.Generate(request,cancellation).GetAwaiter().GetResult();
  Check(reply.Error==null&&reply.Steps.Count==1&&reply.InputTokens==8,"C# ordinary selected plan HTTP contract");
  var catalog=OperationCatalog.CreateDefault();var executor=new PlanExecutor(catalog);
  var plan=ActionPlan.Capture(w,"g",provider.Version,"v",OperationCatalog.Version,"n",reply.Steps);
  Check(executor.TryExecute(w,plan,0,"n",out reason)&&w.Entities["grid"].Get("power.isolated")==1,"HTTP plan executes supported intervention");
 }
 public static int Main(string[] args){try {
  using(var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30))) {
  var cancellation=deadline.Token;
  var connection=SubscriptionConnection.ParseDescriptor(File.ReadAllText(args[0]));
  var status=connection.Refresh(cancellation).GetAwaiter().GetResult();Check(status.Ready,"HTTP status contract");
  var first=connection.LockSelection(status);Pair(first,cancellation);
  Console.WriteLine("READY_FOR_SESSION_SWITCH");Console.Out.Flush();Console.ReadLine();
  var request=new PlanRequest{Id="stale",ActorId="player",Goal="isolate",ContextJson="{}"};
  var reply=first.CreateGenerator().Generate(request,cancellation).GetAwaiter().GetResult();
  Check(reply.Error=="generator_http_409","Actual HTTP stale-session call refused");
  Check(!first.IsCurrent,"HTTP409 invalidates the native locked selection");
  var staleInitial=first.CreateEmergenceProvider().Generate(new EmergenceRequest{Seed=42,Json="{}"},cancellation).GetAwaiter().GetResult();
  Check(staleInitial.Error.StartsWith("generator_http_409"),"Actual HTTP stale initial-session call refused");
  status=connection.Refresh(cancellation).GetAwaiter().GetResult();Check(status.Ready,"Second provider status ready");var second=connection.LockSelection(status);
  Check(first.Selection.ProviderId!=second.Selection.ProviderId&&first.Selection.ModelId==second.Selection.ModelId&&first.Selection.CacheVersion!=second.Selection.CacheVersion,"Two providers same model ID cannot share selection or cache");
  Pair(second,cancellation);
  Console.WriteLine("PASS_NATIVE_HTTP_BRIDGE");return 0;}
 }catch(Exception error){Console.Error.WriteLine(error.GetType().Name+":"+error.Message);return 1;}}
}
