using Ged.Application.Ports;

namespace Ged.Tests;

/// <summary>Mini-outil de test écrit à la main (aucun framework).</summary>
public static class Check
{
    public static void Equal<T>(T expected, T actual, string? what = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new TestFailure($"{what ?? "valeur"} : attendu <{expected}>, obtenu <{actual}>");
    }

    public static void True(bool condition, string message)
    {
        if (!condition) throw new TestFailure(message);
    }

    public static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T e) { return e; }
        catch (Exception e) { throw new TestFailure($"attendu {typeof(T).Name}, obtenu {e.GetType().Name} : {e.Message}"); }
        throw new TestFailure($"attendu {typeof(T).Name}, aucune exception levée");
    }
}

public sealed class TestFailure(string message) : Exception(message);

public sealed record TestCase(string Name, Action Body);

public sealed class FakeClock(DateTime start) : IClock
{
    public DateTime UtcNow { get; private set; } = start;
    public void Advance(TimeSpan duration) => UtcNow += duration;
}

public static class TestRunner
{
    public static (int Passed, int Failed) Run(string suite, IEnumerable<TestCase> tests)
    {
        Console.WriteLine($"\n== {suite}");
        int passed = 0, failed = 0;
        foreach (var test in tests)
        {
            try
            {
                test.Body();
                passed++;
                Console.WriteLine($"  ✔ {test.Name}");
            }
            catch (Exception e)
            {
                failed++;
                var message = e is TestFailure ? e.Message : $"{e.GetType().Name} : {e.Message}";
                Console.WriteLine($"  ✘ {test.Name}\n      {message}");
            }
        }
        return (passed, failed);
    }
}
