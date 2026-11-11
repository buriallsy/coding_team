using System;
using BorrowingSun.Tests;

internal static class Program
{
    private static int Main()
    {
        int failed = 0;
        foreach (var scenario in MarketBehaviorScenarios.Cases)
        {
            try
            {
                scenario.Run();
                Console.WriteLine("PASS " + scenario.Name);
            }
            catch (Exception error)
            {
                failed++;
                Console.WriteLine("FAIL " + scenario.Name + ": " + error.Message);
            }
        }
        Console.WriteLine("MARKET_DOMAIN " + (failed == 0 ? "PASSED" : "FAILED") + ": " +
            MarketBehaviorScenarios.Cases.Length + " scenarios, " + MarketBehaviorScenarios.AssertionCount +
            " assertions, " + failed + " failures.");
        Console.WriteLine("This runner checks production C# domain logic. It does not run Unity EditMode, PlayMode, scenes or input.");
        return failed == 0 ? 0 : 1;
    }
}
