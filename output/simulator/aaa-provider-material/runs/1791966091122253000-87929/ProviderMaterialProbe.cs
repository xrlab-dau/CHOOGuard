using System; using ChooGuard.Seeded.Tests;
public static class ProviderMaterialProbe { public static int Main() { try {
PlanningTests.Run(); SubscriptionTests.Run(); ProviderSelectionTests.Run(); NativeProviderControlTests.Run(); MaterialStoreTests.Run(); InitialWorldNativeTests.Run();
Console.WriteLine("PASS_PROVIDER_MATERIAL_CACHE_BUDGET_LEDGER");return 0;
} catch(Exception e) { Console.Error.WriteLine(e.ToString());return 1; } } }