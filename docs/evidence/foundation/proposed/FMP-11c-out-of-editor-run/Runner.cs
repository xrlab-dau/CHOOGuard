using System;
using System.Linq;
using System.Reflection;

// Console runner over [Test] methods. THIS IS NOT THE UNITY TEST RUNNER and NOT NUnit's
// framework runner. No categories, no NUnit3 XML, no [OneTimeSetUp]/[OneTimeTearDown].
public static class Runner
{
    public static int Main(string[] args)
    {
        var only = args.Length > 0 ? args[0] : null;
        var type = Type.GetType("ChooGuard.Foundation.Multiplayer.Tests.FMP11cObservedRouteConsistencyTests");
        if (type == null)
        {
            Console.WriteLine("ERROR test class not found");
            return 2;
        }
        var tests = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null)
            .OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        var setUp = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<NUnit.Framework.SetUpAttribute>() != null);

        int pass = 0, fail = 0;
        foreach (var test in tests)
        {
            if (only != null && !test.Name.Contains(only)) continue;
            var instance = Activator.CreateInstance(type);
            try
            {
                setUp?.Invoke(instance, null);
                test.Invoke(instance, null);
                Console.WriteLine("PASS  " + test.Name);
                pass++;
            }
            catch (TargetInvocationException tie)
            {
                Console.WriteLine("FAIL  " + test.Name + "\n        " + tie.InnerException.GetType().Name + ": " + tie.InnerException.Message);
                fail++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR " + test.Name + "\n        " + ex);
                fail++;
            }
        }
        Console.WriteLine("\n== " + pass + " passed, " + fail + " failed, " + (pass + fail) + " total ==");
        return fail == 0 ? 0 : 1;
    }
}
